using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.Services.Settings;
using MarketOrder = TheGuideToTheNewEden.Core.Models.Market.Order;
using Statistic = TheGuideToTheNewEden.Core.Models.Market.Statistic;

namespace TheGuideToTheNewEden.WPF.Services.Business;

/// <summary>
/// 估价结果里的一行物品（同类型的多行输入已按类型聚合数量）。
/// </summary>
public sealed class AppraisalItem : INotifyPropertyChanged
{
    private static readonly HttpClient Http = new();

    public int TypeId { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>数量（同类型多行求和）。</summary>
    public double Amount { get; init; }

    /// <summary>单位体积（打包体积，缺省回落普通体积）。</summary>
    public double UnitVolume { get; init; }

    public double Volume => UnitVolume * Amount;

    /// <summary>买入估价单价（口径价 × 百分比）。</summary>
    public double UnitBuyPrice { get; init; }

    /// <summary>卖出估价单价（口径价 × 百分比）。</summary>
    public double UnitSellPrice { get; init; }

    public double BuyPrice => UnitBuyPrice * Amount;

    public double SellPrice => UnitSellPrice * Amount;

    /// <summary>该物品在所选市场没有任何报价（单价为 0）。</summary>
    public bool HasNoPrice => UnitBuyPrice <= 0 && UnitSellPrice <= 0;

    private BitmapImage? _icon;

    /// <summary>物品图标（异步加载后回填，见 <see cref="LoadIconAsync"/>）。</summary>
    public BitmapImage? Icon
    {
        get => _icon;
        private set => Set(ref _icon, value);
    }

    /// <summary>下载物品图标并回填（失败保持 null，不影响结果）。</summary>
    public async Task LoadIconAsync()
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync($"https://images.evetech.net/types/{TypeId}/icon?size=64").ConfigureAwait(false);
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            Icon = bitmap;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

/// <summary>
/// 估价结果：物品明细 + 汇总 + 未识别行。
/// </summary>
public sealed class AppraisalResult
{
    /// <summary>市场名称（所选星域）。</summary>
    public string MarketName { get; init; } = string.Empty;

    public List<AppraisalItem> Items { get; init; } = [];

    /// <summary>未能映射到物品类型的输入行。</summary>
    public List<AppraisalParsedLine> UnresolvedLines { get; init; } = [];

    /// <summary>建筑来源取订单失败（角色可能没有该建筑的市场访问权），结果仅供参考。</summary>
    public bool StructureOrdersFailed { get; init; }

    /// <summary>在所选市场没有任何报价的物品。</summary>
    public List<AppraisalItem> NoPriceItems => Items.Where(p => p.HasNoPrice).ToList();

    public double TotalVolume => Items.Sum(p => p.Volume);

    public double TotalBuyPrice => Items.Sum(p => p.BuyPrice);

    public double TotalSellPrice => Items.Sum(p => p.SellPrice);

    /// <summary>中间价 =（总买价 + 总卖价）/ 2。</summary>
    public double TotalSplitPrice => (TotalBuyPrice + TotalSellPrice) / 2;
}

/// <summary>
/// 物品估价：把游戏复制出来的物品清单（合同/货柜/资产等）解析成 物品 + 数量，
/// 再直接用 ESI 公开市场数据（星域订单 + 历史统计）按设定的口径与百分比算出买/卖估价。
/// 不依赖 WinUI 版估价用的第三方 API（Janice）。
/// <para>
/// 价格口径沿用倒货的 <see cref="ScalperSetting.PriceType"/>：
/// 订单口径（最低卖价/前5%均价/按数量吃单、最高买价/前5%均价/按数量吃单）参考
/// <see cref="ScalperCalculator"/> 的 CalPrice* 系列；历史口径（最高/平均/最低/中位数）同倒货
/// （去极值 = 去掉一个最值后求均值）。口径价算不出（市场无订单）时回落历史均价。
/// </para>
/// </summary>
public static class AppraisalService
{
    private static int MaxThread => Math.Max(1, MarketOrderSettingService.ThreadValue);

    /// <summary>
    /// 执行估价。<paramref name="progress"/> 为行情获取进度 (已完成数, 总数)。
    /// 输入里没有任何可解析的物品名时返回 null。
    /// </summary>
    public static async Task<AppraisalResult?> GetEstimateAsync(
        string input,
        AppraisalSetting setting,
        CancellationToken cancellationToken = default,
        Action<int, int>? progress = null)
    {
        var parsedLines = AppraisalTextParser.Parse(input);
        if (parsedLines.Count == 0)
        {
            return null;
        }

        var location = setting.Location;
        if (location is null || location.Id <= 0)
        {
            return null; // 未选择价格来源（页面已先行校验）
        }

        // 旧缓存的位置可能缺 RegionId（历史统计与星系过滤都依赖它），从所在星系/结构补齐
        if (location.RegionId <= 0)
        {
            var structure = location.Type == MarketLocationType.Structure ? StructureService.GetStructure(location.Id) : null;
            var systemId = location.SolarSystemId > 0 ? location.SolarSystemId : structure?.SolarSystemId ?? 0;
            var system = systemId > 0 ? await Core.Services.DB.MapSolarSystemService.QueryAsync(systemId) : null;
            location.RegionId = structure is { RegionId: > 0 } ? structure.RegionId : system?.RegionID ?? 0;
        }

        // 名称 → 类型（本地 SDE；中文名走本地化库）
        var names = parsedLines.Select(p => p.Name).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var types = await Task.Run(() => ResolveTypes(names), cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        var unresolvedLines = new List<AppraisalParsedLine>();
        // 同类型聚合数量（Janice 同口径：重复物品合并）
        var amounts = new Dictionary<int, double>();
        foreach (var line in parsedLines)
        {
            if (!types.TryGetValue(line.Name, out var type) || type is null)
            {
                unresolvedLines.Add(line);
                continue;
            }

            amounts[type.TypeID] = amounts.TryGetValue(type.TypeID, out var current) ? current + line.Amount : line.Amount;
        }

        if (amounts.Count == 0)
        {
            return new AppraisalResult { MarketName = location.Name, UnresolvedLines = unresolvedLines };
        }

        var typeIds = amounts.Keys.ToList();

        // 按估价口径决定要取哪些行情：订单口径取市场订单（买卖同一份数据），历史口径批量取历史统计
        var needOrders = IsOrderType(setting.SellPriceType) || IsOrderType(setting.BuyPriceType);
        var needHistory = IsHistoryType(setting.SellPriceType) || IsHistoryType(setting.BuyPriceType)
            // 订单口径算不出价时的兜底（回落历史均价）也需要历史
            || needOrders;

        Dictionary<int, List<MarketOrder>> ordersByType = [];
        var structureOrdersFailed = false;
        if (needOrders)
        {
            (ordersByType, structureOrdersFailed) = await FetchOrdersAsync(location, amounts, cancellationToken, progress);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        // 历史统计是"星域级"的：星系/建筑来源都用其所在星域（与倒货/市场页一致）
        Dictionary<int, List<Statistic>> historyByType = [];
        if (needHistory)
        {
            historyByType = await MarketOrderService.Current.GetHistoryBatchAsync(
                typeIds,
                location.RegionId,
                cancellationToken,
                needOrders ? null : progress); // 订单阶段已报过进度的场合，历史走缓存、不再重复报
        }

        // 计算每个类型的单价（口径价 × 百分比）
        // 不同输入名可能解析到同一类型（如同一物品的中英文名混用），按 TypeID 去重
        var typeById = new Dictionary<int, Core.DBModels.InvType>();
        foreach (var resolved in types.Values)
        {
            if (resolved is not null && !typeById.ContainsKey(resolved.TypeID))
            {
                typeById[resolved.TypeID] = resolved;
            }
        }

        var historyDay = Math.Max(1, setting.HistoryDay) + 2;
        var items = new List<AppraisalItem>();
        foreach (var (typeId, amount) in amounts)
        {
            ordersByType.TryGetValue(typeId, out var orders);
            historyByType.TryGetValue(typeId, out var history);

            var sellUnit = CalUnitPrice(orders, history, setting.SellPriceType, setting.SellPercent, historyDay, setting.RemoveExtremum, (long)Math.Ceiling(amount));
            var buyUnit = CalUnitPrice(orders, history, setting.BuyPriceType, setting.BuyPercent, historyDay, setting.RemoveExtremum, (long)Math.Ceiling(amount));
            typeById.TryGetValue(typeId, out var type);

            items.Add(new AppraisalItem
            {
                TypeId = typeId,
                Name = type?.TypeName ?? typeId.ToString(),
                Amount = amount,
                UnitVolume = type is null ? 0 : type.PackagedVolume > 0 ? type.PackagedVolume : type.Volume,
                UnitSellPrice = sellUnit,
                UnitBuyPrice = buyUnit,
            });
        }

        return new AppraisalResult
        {
            MarketName = location.Name,
            Items = items.OrderByDescending(p => p.SellPrice).ToList(),
            UnresolvedLines = unresolvedLines,
            StructureOrdersFailed = structureOrdersFailed,
        };
    }

    /// <summary>
    /// 按价格来源取订单，返回 <c>(typeId → 订单, 建筑取数失败)</c>（字典只含估价清单里出现的类型）：
    /// <list type="bullet">
    ///   <item>星域：按类型并发取星域订单（与市场页同口径）；</item>
    ///   <item>星系：按类型并发取星域订单后按星系过滤（每物品仍只需该物品的星域页，不必整星域全量）；</item>
    ///   <item>建筑：整建筑全量拉一次（<see cref="MarketOrderService.GetStructureOrdersAsync"/>，按 TTL 缓存、需角色授权）后按类型分组；
    ///     拉取失败（无权限等）返回 <c>Failed = true</c>，与"该建筑确实没有这些物品的订单"区分开。</item>
    /// </list>
    /// </summary>
    private static async Task<(Dictionary<int, List<MarketOrder>> Orders, bool Failed)> FetchOrdersAsync(
        MarketLocation location,
        Dictionary<int, double> amounts,
        CancellationToken cancellationToken,
        Action<int, int>? progress)
    {
        var ordersByType = new Dictionary<int, List<MarketOrder>>();
        if (location.Type == MarketLocationType.Structure)
        {
            // 整建筑全量一次（并发按类型逐个调用会同时击穿缓存、重复拉取）
            var structureOrders = await MarketOrderService.Current.GetStructureOrdersAsync(location.Id, cancellationToken);
            if (structureOrders is null)
            {
                return (ordersByType, true);
            }

            foreach (var group in structureOrders.GroupBy(p => (int)p.TypeId))
            {
                if (amounts.ContainsKey(group.Key))
                {
                    ordersByType[group.Key] = group.ToList();
                }
            }

            return (ordersByType, false);
        }

        var isSolarSystem = location.Type == MarketLocationType.SolarSystem;
        var done = 0;
        var pairs = await ThreadHelper.RunAsync(amounts.Keys.ToList(), MaxThread, async typeId =>
        {
            var orders = await MarketOrderService.Current.GetRegionOrdersAsync(typeId, location.RegionId, cancellationToken);
            if (isSolarSystem && orders is { Count: > 0 })
            {
                orders = orders.Where(p => p.SystemId == location.SolarSystemId).ToList();
            }

            var finished = Interlocked.Increment(ref done);
            progress?.Invoke(finished, amounts.Count);
            return (typeId, orders);
        }, cancellationToken);
        foreach (var (typeId, orders) in pairs)
        {
            if (orders is { Count: > 0 })
            {
                ordersByType[typeId] = orders;
            }
        }

        return (ordersByType, false);
    }

    /// <summary>按估价口径计算单价；订单口径算不出时回落历史均价。</summary>
    private static double CalUnitPrice(
        List<MarketOrder>? orders,
        List<Statistic>? history,
        ScalperSetting.PriceType priceType,
        double percent,
        int historyDay,
        bool removeExtremum,
        long amount)
    {
        double price;
        if (IsOrderType(priceType))
        {
            var isBuy = priceType is ScalperSetting.PriceType.BuyTop
                or ScalperSetting.PriceType.BuyTop5
                or ScalperSetting.PriceType.BuyAvailable;
            price = CalOrderPrice(orders, isBuy, priceType, amount);
            if (price <= 0)
            {
                price = CalHistoryPrice(history, ScalperSetting.PriceType.HistoryAverage, historyDay, removeExtremum);
            }
        }
        else
        {
            price = CalHistoryPrice(history, priceType, historyDay, removeExtremum);
        }

        return price * (percent / 100d);
    }

    /// <summary>订单口径：最低卖价/最高买价、前 5% 均价、按数量逐档吃单均价（与倒货一致）。</summary>
    private static double CalOrderPrice(List<MarketOrder>? orders, bool isBuy, ScalperSetting.PriceType priceType, long amount)
    {
        if (orders is not { Count: > 0 })
        {
            return -1;
        }

        // 卖单价升序（最低在前）、买单价降序（最高在前）
        var sorted = orders.Where(p => p.IsBuyOrder == isBuy)
            .OrderByDescending(p => isBuy ? p.Price : -p.Price)
            .ToList();
        if (sorted.Count == 0)
        {
            return -1;
        }

        return priceType switch
        {
            ScalperSetting.PriceType.SellTop => sorted[0].Price,
            ScalperSetting.PriceType.BuyTop => sorted[0].Price,
            ScalperSetting.PriceType.SellTop5 or ScalperSetting.PriceType.BuyTop5 => Top5Average(sorted),
            ScalperSetting.PriceType.SellAvailable or ScalperSetting.PriceType.BuyAvailable => AvailableAverage(sorted, amount),
            _ => -1,
        };
    }

    /// <summary>最优价订单前 5% 的均价；不足 2 条时取首条（与倒货一致）。</summary>
    private static double Top5Average(List<MarketOrder> sorted)
    {
        var top5 = (int)(sorted.Count * 0.05);
        return top5 > 1 ? sorted.Take(top5).Average(p => p.Price) : sorted[0].Price;
    }

    /// <summary>按估价数量逐档吃单的加权均价（"卖单实际数量的相应价格"，与倒货一致）。</summary>
    private static double AvailableAverage(List<MarketOrder> sorted, long amount)
    {
        if (amount <= 0)
        {
            return -1;
        }

        double price = 0;
        long count = 0;
        foreach (var order in sorted)
        {
            var takeVolume = count + order.VolumeRemain > amount ? amount - count : order.VolumeRemain;
            count += takeVolume;
            price += takeVolume * order.Price;
            if (count >= amount)
            {
                break;
            }
        }

        return count == 0 ? -1 : price / count;
    }

    /// <summary>历史口径：近 N 天的最高/平均/最低/中位数；去极值 = 去掉一个最值后求均值（与倒货一致）。</summary>
    private static double CalHistoryPrice(List<Statistic>? statistics, ScalperSetting.PriceType priceType, int day, bool removeExtremum)
    {
        if (priceType is not (ScalperSetting.PriceType.HistoryHighest
            or ScalperSetting.PriceType.HistoryAverage
            or ScalperSetting.PriceType.HistoryLowest
            or ScalperSetting.PriceType.HistoryMedian))
        {
            return -1;
        }

        var history = statistics?.Where(p => p.Date > DateTime.Now.AddDays(-day)).ToList();
        if (history is not { Count: > 0 })
        {
            return 0;
        }

        var canRemoveExtremum = removeExtremum && history.Count > 1;
        return priceType switch
        {
            ScalperSetting.PriceType.HistoryHighest => canRemoveExtremum
                ? (long)Math.Ceiling((history.Sum(p => p.Highest) - history.Max(p => p.Highest)) / (history.Count - 1))
                : (long)Math.Ceiling(history.Average(p => p.Highest)),
            ScalperSetting.PriceType.HistoryAverage => canRemoveExtremum
                ? (long)Math.Ceiling((history.Sum(p => p.Average) - history.Max(p => p.Average)) / (history.Count - 1))
                : (long)Math.Ceiling(history.Average(p => p.Average)),
            ScalperSetting.PriceType.HistoryLowest => canRemoveExtremum
                ? (long)Math.Ceiling((history.Sum(p => p.Lowest) - history.Min(p => p.Lowest)) / (history.Count - 1))
                : (long)Math.Ceiling(history.Average(p => p.Lowest)),
            ScalperSetting.PriceType.HistoryMedian => CalHistoryMedian(history),
            _ => -1,
        };
    }

    /// <summary>历史中位数：区间内每日最低/最高价合并排序后取中位（与倒货一致）。</summary>
    private static double CalHistoryMedian(List<Statistic> history)
    {
        var values = history.Select(p => p.Lowest).ToList();
        values.AddRange(history.Select(p => p.Highest));
        values = values.OrderBy(p => p).ToList();
        return values.Count % 2 == 1
            ? values[values.Count / 2]
            : (values[values.Count / 2 - 1] + values[values.Count / 2]) / 2;
    }

    private static bool IsOrderType(ScalperSetting.PriceType type)
        => type is ScalperSetting.PriceType.SellTop
            or ScalperSetting.PriceType.SellTop5
            or ScalperSetting.PriceType.SellAvailable
            or ScalperSetting.PriceType.BuyTop
            or ScalperSetting.PriceType.BuyTop5
            or ScalperSetting.PriceType.BuyAvailable;

    private static bool IsHistoryType(ScalperSetting.PriceType type)
        => !IsOrderType(type);

    // ==================================================================
    //  名称 → 类型（本地 SDE）
    // ==================================================================

    /// <summary>
    /// 把物品名解析成本地 SDE 类型。粘贴文本可能是英文名（国际服）或中文名（国服/本地化客户端）：
    /// ① 主库精确（英文 SDE 原名）→ ② 本地化库精确（中文名）→ ③ 主库模糊（唯一命中才采用）
    /// → ④ 本地化库模糊（中文模糊，同样只接受唯一命中或忽略大小写恰好相等），避免张冠李戴。
    /// </summary>
    private static Dictionary<string, Core.DBModels.InvType?> ResolveTypes(List<string> names)
    {
        var result = new Dictionary<string, Core.DBModels.InvType?>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            if (result.ContainsKey(name))
            {
                continue;
            }

            Core.DBModels.InvType? type = null;
            try
            {
                // 1) 主库精确（SDE 原名，一般是英文）
                var types = Core.Services.DB.InvTypeService.QueryByName(name, isLike: false);
                if (types is { Count: > 0 })
                {
                    type = types.FirstOrDefault(p => string.Equals(p.TypeName, name, StringComparison.OrdinalIgnoreCase)) ?? types[0];
                }
                // 2) 本地化库精确（中文客户端复制的中文名）
                else if (Core.Config.NeedLocalization)
                {
                    var local = Core.Services.DB.LocalDbService.QueryInvTypes(name).FirstOrDefault();
                    if (local is not null)
                    {
                        type = Core.Services.DB.InvTypeService.QueryType(local.TypeID);
                    }
                }

                // 3) 模糊兜底：主库唯一命中或忽略大小写恰好相等才采用
                if (type is null)
                {
                    types = Core.Services.DB.InvTypeService.QueryByName(name, isLike: true);
                    if (types is { Count: 1 })
                    {
                        type = types[0];
                    }
                    else if (types is { Count: > 1 })
                    {
                        type = types.FirstOrDefault(p => string.Equals(p.TypeName, name, StringComparison.OrdinalIgnoreCase));
                    }
                }

                // 4) 本地化库模糊兜底：主库按英文名 Contains 查中文输入恒为空，中文模糊走本地化库
                if (type is null && Core.Config.NeedLocalization)
                {
                    var locals = Core.Services.DB.LocalDbService.SearchInvType(name);
                    if (locals is { Count: 1 })
                    {
                        type = Core.Services.DB.InvTypeService.QueryType(locals[0].TypeID);
                    }
                    else if (locals is { Count: > 1 })
                    {
                        var exactLocal = locals.FirstOrDefault(p => string.Equals(p.TypeName, name, StringComparison.OrdinalIgnoreCase));
                        if (exactLocal is not null)
                        {
                            type = Core.Services.DB.InvTypeService.QueryType(exactLocal.TypeID);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }

            result[name] = type;
        }

        return result;
    }
}
