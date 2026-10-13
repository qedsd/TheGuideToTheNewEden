using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.Services.Settings;
using MarketOrder = TheGuideToTheNewEden.Core.Models.Market.Order;

namespace TheGuideToTheNewEden.WPF.Services.Business;

/// <summary>
/// 比价引擎：N 个市场（星域 / 星系 / 建筑）× 多物品 的行情取数与全部计算。
/// <para>
/// 与倒货（<see cref="ScalperCalculator"/> + ScalperPageViewModel）的三点关键差异：
/// ① 市场是"多对多"而不是"源 → 目的"；② 物品集合在取数前已知，因此**按物品调单物品接口**而不是整星域全量
/// （建筑除外——结构接口没有"按物品"参数，只能整建筑全量后按类型分组）；③ 只算现价，不取历史统计。
/// </para>
/// <para>
/// 取价口径（与 <see cref="ScalperCalculator"/> 的 CalPrice* 系列一致）：
/// 卖单侧取最低价侧（价升序 → 前 5% 均价 / 首条 / 按数量吃单均价），买单侧取最高价侧（价降序 → 同三种）。
/// **卖出税费只作用于"卖出到手价"**：矩阵里显示的是市场原始参考价，而"最优单价 / 买卖价差 / 集中度 / 利差"用税后价。
/// </para>
/// <para>
/// 并发模型：**市场串行、物品并发**（跨市场并发会让总并发 = 市场数 × 物品数，容易触发 ESI 限流）。
/// 单个市场整体失败不中断整体（记入 <see cref="CompareMarketResult.Failures"/>）。
/// </para>
/// </summary>
public static class CompareMarketService
{
    // 服务层不做本地化：失败原因只给可排查的固定标记，展示文案由 VM 拼
    private const string ReasonRequestFailed = "item requests failed";
    private const string ReasonStructureUnavailable = "structure orders unavailable";
    private const string ReasonRegionUnknown = "region unknown";

    /// <summary>
    /// 执行比价：取数 → 逐格算两侧参考价 → 每行最优/优势/价差 → 汇总 → 集中度 → 利差机会。
    /// 取消时返回空结果（由 VM 判定为"已取消"，不展示）。
    /// </summary>
    /// <param name="setting">比价设置（市场列表、口径、成交方式、税费、阈值、利差口径）。</param>
    /// <param name="items">清单（用户挑选或粘贴得到的物品；同 TypeId 只应出现一次）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="progress">进度回调（可能来自后台线程，调用方自行封送到 UI 线程）。</param>
    public static async Task<CompareMarketResult> RunAsync(
        CompareMarketSetting setting,
        IReadOnlyList<CompareMarketInputItem> items,
        CancellationToken cancellationToken = default,
        Action<CompareProgress>? progress = null)
    {
        var markets = (setting.Markets ?? []).Where(p => p is not null && p.Id > 0).ToList();
        var inputs = (items ?? []).Where(p => p is not null && p.TypeId > 0).ToList();

        var typeInfos = await LoadTypeInfosAsync(inputs);
        var failures = new List<CompareMarketFailure>();
        var marketsFailed = new HashSet<int>();
        var marketOrders = new List<Dictionary<int, List<MarketOrder>>>(markets.Count);

        // ---------- 一、逐市场取数（市场串行，市场内按物品并发） ----------
        for (var i = 0; i < markets.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new CompareMarketResult { Markets = markets, Failures = failures };
            }

            var (orders, failed, reason) = await FetchMarketAsync(markets[i], inputs, i, markets.Count, cancellationToken, progress);
            marketOrders.Add(orders);
            if (failed)
            {
                marketsFailed.Add(i);
                failures.Add(new CompareMarketFailure
                {
                    MarketIndex = i,
                    MarketName = markets[i].Name ?? string.Empty,
                    Reason = reason,
                });
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new CompareMarketResult { Markets = markets, Failures = failures };
        }

        // ---------- 二、逐格算价 ----------
        var rows = new List<CompareMarketRow>(inputs.Count);
        foreach (var input in inputs)
        {
            typeInfos.TryGetValue(input.TypeId, out var type);
            var cells = new List<CompareMarketCell>(markets.Count);
            for (var i = 0; i < markets.Count; i++)
            {
                var orders = marketOrders[i].TryGetValue(input.TypeId, out var list) ? list : null;
                cells.Add(BuildCell(orders, input.Amount, setting, marketsFailed.Contains(i)));
            }

            rows.Add(BuildRow(input, type?.TypeName ?? input.Name, VolumeOf(type), cells, markets, setting));
        }

        return Compose(rows, markets, failures, setting);
    }

    /// <summary>
    /// 用**已有行情**重算（不重新取数）：改成交方式 / 税费 / 分地阈值后调用。
    /// <para>
    /// 注意：价格口径（前 5% 均价 / 最低价 / 按数量吃单）改变会改变每格的价格，必须重新取数——
    /// 那一路由 VM 标记"需重新比价"，不走本方法。
    /// </para>
    /// </summary>
    public static CompareMarketResult Recalculate(
        IReadOnlyList<CompareMarketRow> rows,
        List<MarketLocation> markets,
        List<CompareMarketFailure> failures,
        CompareMarketSetting setting)
    {
        var rebuilt = rows
            .Select(row => BuildRow(
                new CompareMarketInputItem { TypeId = row.TypeId, Name = row.Name, Amount = row.Amount, Group = row.Group },
                row.Name,
                row.UnitVolume,
                row.Cells,
                markets,
                setting))
            .ToList();

        return Compose(rebuilt, markets, failures, setting);
    }

    // ==================================================================
    //  取数
    // ==================================================================

    /// <summary>
    /// 取一个市场对全部物品的行情。返回 <c>(typeId → 订单, 是否整体失败, 失败原因)</c>。
    /// <para>
    /// "整体失败"的判定：建筑接口返回 null（无权限/超时），或**全部物品**的单物品请求都返回 null（网络/限流）。
    /// 部分物品返回 null 视为"该物品在此市场没有报价"（订单接口正常返回空列表才是真的没有订单）。
    /// </para>
    /// </summary>
    private static async Task<(Dictionary<int, List<MarketOrder>> Orders, bool Failed, string Reason)> FetchMarketAsync(
        MarketLocation market,
        List<CompareMarketInputItem> inputs,
        int marketIndex,
        int marketCount,
        CancellationToken cancellationToken,
        Action<CompareProgress>? progress)
    {
        var result = new Dictionary<int, List<MarketOrder>>();

        // 同一物品可能同时出现在卖出组与买入组（两组是独立清单）：订单数据与分组无关，
        // 取数前按 TypeId 去重，避免同一物品被请求两次；算价阶段仍按各自的行（数量可能不同）分别计算。
        inputs = inputs.GroupBy(p => p.TypeId).Select(p => p.First()).ToList();

        if (inputs.Count == 0)
        {
            return (result, false, string.Empty);
        }

        var done = 0;

        void Report()
            => progress?.Invoke(new CompareProgress
            {
                MarketIndex = marketIndex + 1,
                MarketCount = marketCount,
                MarketName = market.Name ?? string.Empty,
                Done = Volatile.Read(ref done),
                Total = inputs.Count,
            });

        try
        {
            // 建筑：结构接口没有"按物品"参数，只能整建筑全量拉一次（有 TTL 缓存、需角色授权）
            if (market.Type == MarketLocationType.Structure)
            {
                var all = await MarketOrderService.Current.GetStructureOrdersAsync(market.Id, cancellationToken);
                if (all is null)
                {
                    return (result, true, ReasonStructureUnavailable);
                }

                var wanted = inputs.Select(p => (long)p.TypeId).ToHashSet();
                foreach (var group in all.Where(p => wanted.Contains(p.TypeId)).GroupBy(p => (int)p.TypeId))
                {
                    result[group.Key] = group.ToList();
                }

                done = inputs.Count;
                Report();
                return (result, false, string.Empty);
            }

            var regionId = await ResolveRegionIdAsync(market);
            if (regionId <= 0)
            {
                return (result, true, ReasonRegionUnknown);
            }

            var isSolarSystem = market.Type == MarketLocationType.SolarSystem;
            var failedRequests = 0;

            var pairs = await Core.Helpers.ThreadHelper.RunAsync(inputs, Threads, async input =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return (input.TypeId, (List<MarketOrder>?)null);
                }

                // 单物品星域订单（星域接口本身已含该星域内空间站与建筑的卖单；PLEX 由服务内部切全球星域）
                var orders = await MarketOrderService.Current.GetRegionOrdersAsync(input.TypeId, regionId, cancellationToken);
                if (orders is { Count: > 0 } && isSolarSystem)
                {
                    orders = orders.Where(p => p.SystemId == market.SolarSystemId).ToList();
                }

                Interlocked.Increment(ref done);
                if (orders is null)
                {
                    Interlocked.Increment(ref failedRequests);
                }

                Report();
                return (input.TypeId, orders);
            }, cancellationToken);

            foreach (var (typeId, orders) in pairs)
            {
                if (orders is { Count: > 0 })
                {
                    result[typeId] = orders;
                }
            }

            var failed = Volatile.Read(ref failedRequests) >= inputs.Count;
            return (result, failed, failed ? ReasonRequestFailed : string.Empty);
        }
        catch (OperationCanceledException)
        {
            return (result, false, string.Empty);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return (result, true, ReasonRequestFailed);
        }
    }

    /// <summary>解析市场所在星域（历史口径与"按星系过滤"都依赖它）。0 = 无法确定。</summary>
    private static async Task<int> ResolveRegionIdAsync(MarketLocation market)
    {
        if (market.Type == MarketLocationType.Region)
        {
            return (int)market.Id;
        }

        if (market.RegionId > 0)
        {
            return market.RegionId;
        }

        var systemId = market.SolarSystemId > 0
            ? market.SolarSystemId
            : market.Type == MarketLocationType.SolarSystem ? (int)market.Id : 0;
        if (systemId <= 0)
        {
            return 0;
        }

        try
        {
            var system = await Core.Services.DB.MapSolarSystemService.QueryAsync(systemId);
            return system?.RegionID ?? 0;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return 0;
        }
    }

    private static async Task<Dictionary<int, InvType>> LoadTypeInfosAsync(List<CompareMarketInputItem> inputs)
    {
        var result = new Dictionary<int, InvType>();
        var ids = inputs.Select(p => (long)p.TypeId).Distinct().ToList();
        if (ids.Count == 0)
        {
            return result;
        }

        try
        {
            var types = await Core.Services.DB.InvTypeService.QueryTypesAsync(ids);
            foreach (var type in types)
            {
                result[type.TypeID] = type;
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        return result;
    }

    /// <summary>取数线程数（至少 1，与倒货/估价共用市场设置里的线程数）。</summary>
    private static int Threads => Math.Max(1, MarketOrderSettingService.ThreadValue);

    // ==================================================================
    //  算价
    // ==================================================================

    /// <summary>一个市场对一件物品的两侧参考价与挂单量统计（无订单时两侧为 0）。</summary>
    private static CompareMarketCell BuildCell(
        List<MarketOrder>? orders,
        double amount,
        CompareMarketSetting setting,
        bool marketFailed)
    {
        if (orders is not { Count: > 0 })
        {
            return new CompareMarketCell { MarketFailed = marketFailed };
        }

        var sells = new List<MarketOrder>();
        var buys = new List<MarketOrder>();
        foreach (var order in orders)
        {
            if (order.IsBuyOrder)
            {
                buys.Add(order);
            }
            else
            {
                sells.Add(order);
            }
        }

        sells.Sort((a, b) => a.Price.CompareTo(b.Price));   // 卖单：价升序（最低在前）
        buys.Sort((a, b) => b.Price.CompareTo(a.Price));    // 买单：价降序（最高在前）

        return new CompareMarketCell
        {
            SellPrice = CalSidePrice(sells, setting.SellPriceType, amount),
            BuyPrice = CalSidePrice(buys, setting.BuyPriceType, amount),
            SellOrderCount = sells.Count,
            SellVolumeRemain = sells.Sum(p => (long)p.VolumeRemain),
            BuyOrderCount = buys.Count,
            BuyVolumeRemain = buys.Sum(p => (long)p.VolumeRemain),
        };
    }

    /// <summary>按口径算某一侧的参考价（订单已排序；无订单或口径不适用返回 0）。</summary>
    private static double CalSidePrice(List<MarketOrder> sortedOrders, ScalperSetting.PriceType type, double amount)
    {
        if (sortedOrders.Count == 0)
        {
            return 0;
        }

        return type switch
        {
            ScalperSetting.PriceType.SellTop5 or ScalperSetting.PriceType.BuyTop5 => Top5Average(sortedOrders),
            ScalperSetting.PriceType.SellAvailable or ScalperSetting.PriceType.BuyAvailable => AvailableAverage(sortedOrders, amount),
            ScalperSetting.PriceType.SellTop or ScalperSetting.PriceType.BuyTop => sortedOrders[0].Price,
            _ => 0,
        };
    }

    /// <summary>最优价订单前 5% 的均价；不足 2 条时取首条（与倒货/估价一致）。</summary>
    private static double Top5Average(List<MarketOrder> sortedOrders)
    {
        var top5 = (int)(sortedOrders.Count * 0.05);
        return top5 > 1 ? sortedOrders.Take(top5).Average(p => p.Price) : sortedOrders[0].Price;
    }

    /// <summary>按数量逐档吃单的加权均价；一条都没吃到返回 0（与估价页口径一致）。</summary>
    private static double AvailableAverage(List<MarketOrder> sortedOrders, double amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        var target = (long)Math.Ceiling(amount);
        double price = 0;
        long count = 0;
        foreach (var order in sortedOrders)
        {
            var takeVolume = count + order.VolumeRemain > target ? target - count : order.VolumeRemain;
            count += takeVolume;
            price += takeVolume * order.Price;
            if (count >= target)
            {
                break;
            }
        }

        return count == 0 ? 0 : price / count;
    }

    /// <summary>卖出到手价用的那一侧（挂卖单 → 卖单价；卖给买单 → 买单价）。</summary>
    private static double PickSellPrice(CompareMarketCell cell, CompareTradeMode mode)
        => mode == CompareTradeMode.MakeOrder ? cell.SellPrice : cell.BuyPrice;

    /// <summary>买入成本价用的那一侧（从卖单买 → 卖单价；挂买单 → 买单价）。</summary>
    private static double PickBuyPrice(CompareMarketCell cell, CompareTradeMode mode)
        => mode == CompareTradeMode.TakeOrder ? cell.SellPrice : cell.BuyPrice;

    private static double TaxRate(CompareMarketSetting setting)
        => Math.Clamp(setting.SellTaxPercent, 0, 100) / 100d;

    private static double VolumeOf(InvType? type)
        => type is null ? 0 : type.PackagedVolume > 0 ? type.PackagedVolume : type.Volume;

    /// <summary>组装一行：最优买入/卖出市场、较次优优势、买卖价差、按角色单价。</summary>
    private static CompareMarketRow BuildRow(
        CompareMarketInputItem input,
        string name,
        double unitVolume,
        List<CompareMarketCell> cells,
        List<MarketLocation> markets,
        CompareMarketSetting setting)
    {
        var tax = TaxRate(setting);
        var sellPrices = new double[cells.Count];
        var buyPrices = new double[cells.Count];
        for (var i = 0; i < cells.Count; i++)
        {
            sellPrices[i] = PickSellPrice(cells[i], setting.SellMode) * (1 - tax);
            buyPrices[i] = PickBuyPrice(cells[i], setting.BuyMode);
        }

        var sellIndex = ArgMax(sellPrices);
        var buyIndex = ArgMin(buyPrices);
        var isSellGroup = input.Group == CompareItemGroup.Sell;

        // 回填每个市场的"角色单价"：矩阵里每个市场只显示这一列（卖出组看到手价、买入组看成本价）
        for (var i = 0; i < cells.Count; i++)
        {
            cells[i].UnitPrice = isSellGroup ? sellPrices[i] : buyPrices[i];
        }
        var effective = isSellGroup
            ? sellIndex >= 0 ? sellPrices[sellIndex] : 0
            : buyIndex >= 0 ? buyPrices[buyIndex] : 0;

        // 较次优优势：卖出组比"次高"高多少、买入组比"次低"便宜多少（都为"比次优点好多少"）
        double? advantage = null;
        if (isSellGroup && sellIndex >= 0)
        {
            var best = sellPrices[sellIndex];
            var second = SecondBest(sellPrices, sellIndex);
            if (best > 0 && second > 0)
            {
                advantage = (best - second) / best * 100;
            }
        }
        else if (!isSellGroup && buyIndex >= 0)
        {
            var best = buyPrices[buyIndex];
            var second = SecondLowest(buyPrices, buyIndex);
            if (best > 0 && second > 0)
            {
                advantage = (second - best) / best * 100;
            }
        }

        var sellUnit = sellIndex >= 0 ? sellPrices[sellIndex] : 0;
        var buyUnit = buyIndex >= 0 ? buyPrices[buyIndex] : 0;

        return new CompareMarketRow
        {
            TypeId = input.TypeId,
            Name = name,
            UnitVolume = unitVolume,
            Cells = cells,
            Group = input.Group,
            Amount = input.Amount,
            BuyMarketIndex = buyIndex,
            SellMarketIndex = sellIndex,
            BuyUnitPrice = buyUnit,
            SellUnitPrice = sellUnit,
            BestBuyMarketName = MarketName(markets, buyIndex),
            BestSellMarketName = MarketName(markets, sellIndex),
            EffectiveUnitPrice = effective,
            AdvantagePercent = advantage,
            HasNoQuote = effective <= 0,
        };
    }

    /// <summary>最大值下标（只认 &gt; 0 的值；没有则 -1）。</summary>
    private static int ArgMax(double[] values)
    {
        var index = -1;
        var best = 0d;
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] > best)
            {
                best = values[i];
                index = i;
            }
        }

        return index;
    }

    /// <summary>最小值下标（只认 &gt; 0 的值；没有则 -1）。</summary>
    private static int ArgMin(double[] values)
    {
        var index = -1;
        var best = double.MaxValue;
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] > 0 && values[i] < best)
            {
                best = values[i];
                index = i;
            }
        }

        return index;
    }

    /// <summary>排除指定下标后的最高价（无则 0）。</summary>
    private static double SecondBest(double[] values, int excluded)
    {
        var best = 0d;
        for (var i = 0; i < values.Length; i++)
        {
            if (i != excluded && values[i] > best)
            {
                best = values[i];
            }
        }

        return best;
    }

    /// <summary>排除指定下标后的最低价（无则 0）。</summary>
    private static double SecondLowest(double[] values, int excluded)
    {
        var best = 0d;
        for (var i = 0; i < values.Length; i++)
        {
            if (i == excluded || values[i] <= 0)
            {
                continue;
            }

            if (best <= 0 || values[i] < best)
            {
                best = values[i];
            }
        }

        return best;
    }

    // ==================================================================
    //  汇总 / 集中度 / 利差
    // ==================================================================

    /// <summary>排序（卖出组在前）+ 汇总 + 集中度 + 利差，组装最终结果。</summary>
    private static CompareMarketResult Compose(
        List<CompareMarketRow> rows,
        List<MarketLocation> markets,
        List<CompareMarketFailure> failures,
        CompareMarketSetting setting)
    {
        var ordered = rows
            .OrderBy(p => p.Group == CompareItemGroup.Sell ? 0 : 1)
            .ThenByDescending(p => p.Subtotal)
            .ToList();

        return new CompareMarketResult
        {
            Markets = markets,
            Rows = ordered,
            Failures = failures,
            TotalSellIncome = ordered.Where(p => p.Group == CompareItemGroup.Sell).Sum(p => p.Subtotal),
            TotalBuyExpense = ordered.Where(p => p.Group == CompareItemGroup.Buy).Sum(p => p.Subtotal),
            TotalVolume = ordered.Sum(p => p.Volume),
            NoQuoteCount = ordered.Count(p => p.HasNoQuote),
            SellConcentration = AnalyzeConcentration(ordered, markets, setting, sellSide: true),
            BuyConcentration = AnalyzeConcentration(ordered, markets, setting, sellSide: false),
        };
    }

    /// <summary>
    /// 集中度分析：回答"值不值得把货分散到多个市场"。
    /// <para>
    /// 口径（**同集合自洽**）：候选市场只与"它自己有报价的物品"比较——避免把"不能在 J 卖"与"在 J 卖得便宜"
    /// 混成一件事。返回损失率最小的市场（即最适合"集中一地"的市场），以及"分散比集中好多少"。
    /// </para>
    /// </summary>
    private static ConcentrationResult? AnalyzeConcentration(
        List<CompareMarketRow> rows,
        List<MarketLocation> markets,
        CompareMarketSetting setting,
        bool sellSide)
    {
        var group = sellSide ? CompareItemGroup.Sell : CompareItemGroup.Buy;
        var relevant = rows.Where(p => p.Group == group && p.Cells.Count > 0 && p.Amount > 0).ToList();
        if (relevant.Count == 0)
        {
            return null;
        }

        var marketCount = relevant.Max(p => p.Cells.Count);
        var tax = TaxRate(setting);

        double PriceOf(CompareMarketCell cell)
            => sellSide
                ? PickSellPrice(cell, setting.SellMode) * (1 - tax)
                : PickBuyPrice(cell, setting.BuyMode);

        // 每件物品"各自选最优"的价（卖出取最高、买入取最低；<= 0 视为不可用）
        var bestOfItem = new double[relevant.Count];
        for (var i = 0; i < relevant.Count; i++)
        {
            var best = 0d;
            foreach (var cell in relevant[i].Cells)
            {
                var price = PriceOf(cell);
                if (price <= 0)
                {
                    continue;
                }

                if (best <= 0 || (sellSide ? price > best : price < best))
                {
                    best = price;
                }
            }

            bestOfItem[i] = best;
        }

        var bestIndex = -1;
        var bestPercent = double.MaxValue;
        var bestGain = 0d;
        var bestPerItemTotal = 0d;
        var bestSingleTotal = 0d;
        var bestCovered = 0;
        var bestCoversAll = false;

        for (var j = 0; j < marketCount; j++)
        {
            var perItemTotal = 0d;
            var singleTotal = 0d;
            var covered = 0;

            for (var i = 0; i < relevant.Count; i++)
            {
                if (j >= relevant[i].Cells.Count || bestOfItem[i] <= 0)
                {
                    continue;
                }

                var price = PriceOf(relevant[i].Cells[j]);
                if (price <= 0)
                {
                    continue;
                }

                var amount = relevant[i].Amount;
                perItemTotal += bestOfItem[i] * amount;
                singleTotal += price * amount;
                covered++;
            }

            if (covered == 0 || perItemTotal <= 0)
            {
                continue;
            }

            // 卖出：分散比集中多赚（单市场总额更低）；买入：集中比分散多花（单市场总额更高）
            var gain = sellSide ? perItemTotal - singleTotal : singleTotal - perItemTotal;
            var percent = Math.Max(0, gain) / perItemTotal * 100;

            // 覆盖优先：结论文案是"在「X」一地处理即可"，前提是 X 能处理**全部**物品；
            // 因此只要存在覆盖全部的市场，就只在它们之间比损失；覆盖不全的市场仅在"没有任何市场覆盖全部"时才参选
            // （那种情况下覆盖不足会由文案附加说明，避免把"不能在 X 处理"当成"在 X 处理得便宜"）。
            var coversAll = covered >= relevant.Count;
            var better = bestIndex < 0
                || (coversAll && !bestCoversAll)
                || (coversAll == bestCoversAll && percent < bestPercent);

            if (better)
            {
                bestPercent = percent;
                bestIndex = j;
                bestGain = Math.Max(0, gain);
                bestPerItemTotal = perItemTotal;
                bestSingleTotal = singleTotal;
                bestCovered = covered;
                bestCoversAll = coversAll;
            }
        }

        if (bestIndex < 0)
        {
            return null;
        }

        return new ConcentrationResult
        {
            MarketIndex = bestIndex,
            MarketName = MarketName(markets, bestIndex),
            BestPerItemTotal = bestPerItemTotal,
            SingleMarketTotal = bestSingleTotal,
            Gain = bestGain,
            GainPercent = bestPercent,
            CoveredCount = bestCovered,
            TotalCount = relevant.Count,
        };
    }

    private static string MarketName(List<MarketLocation> markets, int index)
        => index >= 0 && index < markets.Count ? markets[index].Name ?? string.Empty : string.Empty;
}
