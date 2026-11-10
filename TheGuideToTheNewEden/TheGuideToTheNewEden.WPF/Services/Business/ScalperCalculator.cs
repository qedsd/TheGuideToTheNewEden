using TheGuideToTheNewEden.Core.Models.Market;
using MarketOrder = TheGuideToTheNewEden.Core.Models.Market.Order;
using Statistic = TheGuideToTheNewEden.Core.Models.Market.Statistic;

namespace TheGuideToTheNewEden.WPF.Services.Business;

/// <summary>
/// 倒货分析计算引擎：由源/目的市场订单与历史统计算出推荐度、回报率、净利润、波动、饱和度、热力值等。
/// 公式与 <c>TheGuideToTheNewEden.WinUI/ViewModels/Business/ScalperViewModel</c> 中的 <c>Cal*</c> 系列逐一对应
/// （含原实现的取舍：买单取价沿用目的市场历史/销量、历史极值按去掉一个最值后求均值等）。
/// </summary>
public static class ScalperCalculator
{
    private delegate double CalPrice(List<MarketOrder>? orders, List<Statistic>? statistics, long sales, int day, bool removeExtremum);

    /// <summary>
    /// 由源、目的市场订单按物品分组生成待分析项（卖单按价升序、买单按价降序）。
    /// </summary>
    public static List<ScalperItem> BuildItems(List<MarketOrder> sourceOrders, List<MarketOrder> destinationOrders, HashSet<int> typeIds)
        => BuildItems(sourceOrders, destinationOrders, typeIds, null);

    /// <summary>
    /// 阶段 299：订单本净化——过滤剩余时间不足 <paramref name="staleOrderDays"/> 天的挂单、
    /// 价格偏离本侧中位数过大的异常单（&lt;1% 或 &gt;100 倍，防 1 ISK 墙/错价单）与 0 余量单。
    /// </summary>
    public static List<MarketOrder> SanitizeOrders(IEnumerable<MarketOrder>? orders, double staleOrderDays)
    {
        if (orders is null)
        {
            return [];
        }

        var list = orders.Where(p => p.VolumeRemain > 0).ToList();
        if (list.Count == 0)
        {
            return list;
        }

        var prices = list.Select(p => p.Price).OrderBy(p => p).ToList();
        var median = prices.Count % 2 == 1
            ? prices[prices.Count / 2]
            : (prices[prices.Count / 2 - 1] + prices[prices.Count / 2]) / 2;

        var minPrice = median * 0.01;
        var maxPrice = median * 100;
        var staleLimit = staleOrderDays > 0 ? TimeSpan.FromDays(staleOrderDays) : TimeSpan.MaxValue;
        return list.Where(p => p.Price >= minPrice && p.Price <= maxPrice && p.RemainTimeSpan >= staleLimit).ToList();
    }

    public static List<ScalperItem> BuildItems(List<MarketOrder> sourceOrders, List<MarketOrder> destinationOrders, HashSet<int> typeIds, double? staleOrderDays)
    {
        var stale = staleOrderDays ?? 2;
        sourceOrders = SanitizeOrders(sourceOrders, stale);
        destinationOrders = SanitizeOrders(destinationOrders, stale);
        var items = new List<ScalperItem>();
        foreach (var group in sourceOrders.GroupBy(p => p.TypeId))
        {
            var list = group.ToList();
            var invType = list[0].InvType;
            if (invType?.MarketGroupID is null || !typeIds.Contains(invType.TypeID))
            {
                continue;
            }

            items.Add(new ScalperItem
            {
                InvType = invType,
                SourceSellOrders = list.Where(p => !p.IsBuyOrder).OrderBy(p => p.Price).ToList(),
                SourceBuyOrders = list.Where(p => p.IsBuyOrder).OrderByDescending(p => p.Price).ToList(),
            });
        }

        var destinationGroups = destinationOrders.GroupBy(p => p.TypeId).ToDictionary(p => (int)p.Key);
        foreach (var item in items)
        {
            if (destinationGroups.TryGetValue(item.InvType.TypeID, out var group))
            {
                var list = group.ToList();
                item.DestinationSellOrders = list.Where(p => !p.IsBuyOrder).OrderBy(p => p.Price).ToList();
                item.DestinationBuyOrders = list.Where(p => p.IsBuyOrder).OrderByDescending(p => p.Price).ToList();
            }
        }

        return items;
    }

    /// <summary>取下单物品的历史统计挂到分析项上（缺历史的一侧保持为空，随后被过滤掉）。</summary>
    public static void SetHistory(List<ScalperItem> items, Dictionary<int, List<Statistic>> sourceHistory, Dictionary<int, List<Statistic>> destinationHistory)
    {
        foreach (var item in items)
        {
            if (sourceHistory.TryGetValue(item.InvType.TypeID, out var source))
            {
                item.SourceStatistics = source;
            }

            if (destinationHistory.TryGetValue(item.InvType.TypeID, out var destination))
            {
                item.DestinationStatistics = destination;
            }
        }
    }

    /// <summary>
    /// 执行完整分析流程，返回按推荐度降序排列的结果（已剔除目的市场无销量或无历史的物品）。
    /// </summary>
    public static List<ScalperItem> Calculate(List<ScalperItem> source, ScalperSetting setting)
    {
        foreach (var item in source)
        {
            item.Suggestion = 0;
            item.SourceSales = 0;
            item.DestinationSales = 0;
            item.SellPrice = 0;
            item.BuyPrice = 0;
            item.TargetSales = 0;
            item.NetProfit = 0;
            item.TargetNetProfit = 0;
            item.ROI = 0;
            item.HistoryPriceFluctuation = 0;
            item.NowPriceFluctuation = 0;
            item.Saturation = 0;
            item.HeatValue = 0;
            item.HeatRatio = 0;
            item.PriceTrend = 0;
            item.PriceCushion = 0;
            item.SalesCV = 0;
            item.Confidence = 0;
            item.Score = 0;
            item.ScoreDetail = string.Empty;
            item.BuyAvgPrice = 0;
            item.SellAvgPrice = 0;
            item.FillRatio = 0;
            item.CompetitionShare = 0;
            item.DaysToSell = 0;
            item.NetProfitAfterFees = 0;
            item.TargetNetProfitAfterFees = 0;
            item.ROIAfterFees = 0;
            item.ProfitPerHour = 0;
            item.RiskAdjustedProfit = 0;
            item.ProfitPerVolumeAfterFees = 0;
        }

        CalSales(source, setting);
        var items = source.Where(p => p.DestinationSales > 0).ToList();
        CalTargetSales(items, setting);
        CalSellPrice(items, setting);
        CalBuyPrice(items, setting);
        CalNetProfit(items);
        CalROI(items);
        CalPrincipal(items);
        CalHistoryPriceFluctuation(items, setting);
        CalNowPriceFluctuation(items, setting);
        CalSaturation(items, setting);
        CalHeatValue(items, setting);
        CalIskPerJump(items, setting);
        CalIskPerVolume(items);
        CalExecutionMetrics(items, setting);
        CalQualityMetrics(items, setting);
        CalSuggestion(items, setting);
        CalScore(items, setting);
        return items.OrderByDescending(p => p.Score).ToList();
    }

    /// <summary>市场日销量（源/目的各自按天数、去极值方式与最低/最高/平均口径取）。</summary>
    private static void CalSales(List<ScalperItem> items, ScalperSetting setting)
    {
        foreach (var item in items)
        {
            var history = item.SourceStatistics?.Where(p => p.Date > DateTime.Now.AddDays(-setting.SourceSalesDay)).ToList();
            if (history is { Count: > 0 })
            {
                history = history.OrderBy(p => p.Volume).ToList();
                if (history.Count > 3 && setting.SourceRemoveExtremum)
                {
                    history.RemoveAt(0);
                    history.RemoveAt(history.Count - 1);
                }

                item.SourceSales = setting.SourceSalesType switch
                {
                    ScalperSetting.SalesType.HistoryLowest => history[0].Volume,
                    ScalperSetting.SalesType.HistoryHighest => history[^1].Volume,
                    _ => (long)Math.Ceiling(history.Sum(p => p.Volume) / (decimal)history.Count),
                };
            }

            history = item.DestinationStatistics?.Where(p => p.Date > DateTime.Now.AddDays(-setting.DestinationSalesDay)).ToList();
            if (history is { Count: > 0 })
            {
                history = history.OrderBy(p => p.Volume).ToList();
                if (history.Count > 3 && setting.DestinationRemoveExtremum)
                {
                    history.RemoveAt(0);
                    history.RemoveAt(history.Count - 1);
                }

                item.DestinationSales = setting.DestinationSalesType switch
                {
                    ScalperSetting.SalesType.HistoryLowest => history[0].Volume,
                    ScalperSetting.SalesType.HistoryHighest => history[^1].Volume,
                    _ => (long)Math.Ceiling(history.Sum(p => p.Volume) / (decimal)history.Count),
                };
            }
        }
    }

    private static void CalSellPrice(List<ScalperItem> items, ScalperSetting setting)
    {
        var (calPrice, isBuyOrders) = ResolvePriceFunc(setting.SellPrice);
        foreach (var item in items)
        {
            try
            {
                item.SellPrice = calPrice(
                    isBuyOrders ? item.DestinationBuyOrders : item.DestinationSellOrders,
                    item.DestinationStatistics,
                    item.DestinationSales,
                    setting.SellHistoryDay + 2,
                    setting.SellPirceRemoveExtremum);
                if (item.SellPrice <= 0)
                {
                    item.SellPrice = GetDefaultPrice(item);
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                item.SellPrice = GetDefaultPrice(item);
            }

            item.SellPrice *= setting.SellPriceScale;
        }
    }

    private static void CalBuyPrice(List<ScalperItem> items, ScalperSetting setting)
    {
        var (calPrice, isBuyOrders) = ResolvePriceFunc(setting.BuyPrice);
        foreach (var item in items)
        {
            try
            {
                // 沿用原实现：历史口径的天数/去极值用"买入"设置，但历史数据仍取目的市场
                item.BuyPrice = calPrice(
                    isBuyOrders ? item.SourceBuyOrders : item.SourceSellOrders,
                    item.SourceStatistics,
                    item.DestinationSales,
                    setting.BuyHistoryDay + 2,
                    setting.BuyPirceRemoveExtremum);
                if (item.BuyPrice <= 0)
                {
                    item.BuyPrice = GetDefaultPrice(item);
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                item.BuyPrice = GetDefaultPrice(item);
            }

            item.BuyPrice *= setting.BuyPriceScale;
        }
    }

    private static (CalPrice Fun, bool IsBuyOrders) ResolvePriceFunc(ScalperSetting.PriceType type) => type switch
    {
        ScalperSetting.PriceType.SellTop5 => (CalPriceTop5, false),
        ScalperSetting.PriceType.SellAvailable => (CalPriceAvailable, false),
        ScalperSetting.PriceType.SellTop => (CalPriceTop, false),
        ScalperSetting.PriceType.BuyTop5 => (CalPriceTop5, true),
        ScalperSetting.PriceType.BuyAvailable => (CalPriceAvailable, true),
        ScalperSetting.PriceType.BuyTop => (CalPriceTop, true),
        ScalperSetting.PriceType.HistoryHighest => (CalPriceHistoryHighest, true),
        ScalperSetting.PriceType.HistoryAverage => (CalPriceHistoryAverage, true),
        ScalperSetting.PriceType.HistoryLowest => (CalPriceHistoryLowest, true),
        ScalperSetting.PriceType.HistoryMedian => (CalPriceHistoryMedian, true),
        _ => (CalPriceTop5, true),
    };

    /// <summary>
    /// 阶段 299：前 5% 改为按<b>成交量</b>加权——累计挂单量达到总量 5% 为止的 VWAP，
    /// 避免 1 件小单/1 ISK 墙按"订单条数"稀释均价。
    /// </summary>
    private static double CalPriceTop5(List<MarketOrder>? orders, List<Statistic>? statistics, long sales, int day, bool removeExtremum)
    {
        if (orders is not { Count: > 0 })
        {
            return -1;
        }

        var totalVolume = orders.Sum(p => (double)p.VolumeRemain);
        var targetVolume = totalVolume * 0.05;
        double value = 0;
        double filled = 0;
        foreach (var order in orders)
        {
            var volume = (double)order.VolumeRemain;
            value += volume * order.Price;
            filled += volume;
            if (filled >= targetVolume)
            {
                break;
            }
        }

        return filled > 0 ? value / filled : -1;
    }

    /// <summary>按日销量逐档吃单得到的均价。</summary>
    private static double CalPriceAvailable(List<MarketOrder>? orders, List<Statistic>? statistics, long sales, int day, bool removeExtremum)
    {
        if (orders is not { Count: > 0 })
        {
            return -1;
        }

        double price = 0;
        long count = 0;
        foreach (var order in orders)
        {
            var takeVolume = count + order.VolumeRemain > sales ? sales - count : order.VolumeRemain;
            count += takeVolume;
            price += takeVolume * order.Price;
            if (count == sales)
            {
                break;
            }
        }

        return count == 0 ? -1 : price / count;
    }

    /// <summary>最低/最高价（订单已排序，取首条）。</summary>
    private static double CalPriceTop(List<MarketOrder>? orders, List<Statistic>? statistics, long sales, int day, bool removeExtremum)
        => orders is { Count: > 0 } ? orders[0].Price : -1;

    private static double CalPriceHistoryHighest(List<MarketOrder>? orders, List<Statistic>? statistics, long sales, int day, bool removeExtremum)
    {
        var history = statistics?.Where(p => p.Date > DateTime.Now.AddDays(-day)).ToList();
        if (history is not { Count: > 0 })
        {
            return 0;
        }

        return removeExtremum && history.Count > 1
            ? (long)Math.Ceiling((history.Sum(p => p.Highest) - history.Max(p => p.Highest)) / (history.Count - 1))
            : (long)Math.Ceiling(history.Sum(p => p.Highest) / history.Count);
    }

    private static double CalPriceHistoryAverage(List<MarketOrder>? orders, List<Statistic>? statistics, long sales, int day, bool removeExtremum)
    {
        var history = statistics?.Where(p => p.Date > DateTime.Now.AddDays(-day)).ToList();
        if (history is not { Count: > 0 })
        {
            return 0;
        }

        return removeExtremum && history.Count > 1
            ? (long)Math.Ceiling((history.Sum(p => p.Average) - history.Max(p => p.Average)) / (history.Count - 1))
            : (long)Math.Ceiling(history.Sum(p => p.Average) / history.Count);
    }

    private static double CalPriceHistoryLowest(List<MarketOrder>? orders, List<Statistic>? statistics, long sales, int day, bool removeExtremum)
    {
        var history = statistics?.Where(p => p.Date > DateTime.Now.AddDays(-day)).ToList();
        if (history is not { Count: > 0 })
        {
            return 0;
        }

        return removeExtremum && history.Count > 1
            ? (long)Math.Ceiling((history.Sum(p => p.Lowest) - history.Min(p => p.Lowest)) / (history.Count - 1))
            : (long)Math.Ceiling(history.Sum(p => p.Lowest) / history.Count);
    }

    /// <summary>历史中位数：把区间内每日的 Lowest 与 Highest 合并排序后取中位。</summary>
    private static double CalPriceHistoryMedian(List<MarketOrder>? orders, List<Statistic>? statistics, long sales, int day, bool removeExtremum)
    {
        var history = statistics?.Where(p => p.Date > DateTime.Now.AddDays(-day)).ToList();
        if (history is not { Count: > 0 })
        {
            return 0;
        }

        var values = history.Select(p => p.Lowest).ToList();
        values.AddRange(history.Select(p => p.Highest));
        values = values.OrderBy(p => p).ToList();
        return (values[values.Count / 2 - 1] + values[values.Count / 2]) / 2;
    }

    /// <summary>算不出设定口径价格时的兜底：目的市场最近一天均价。</summary>
    private static double GetDefaultPrice(ScalperItem item)
        => item.DestinationStatistics is { Count: > 0 } ? item.DestinationStatistics[^1].Average : 0;

    /// <summary>目标销量 = 目的市场日销量 × 拟卖出百分比。</summary>
    private static void CalTargetSales(List<ScalperItem> items, ScalperSetting setting)
    {
        foreach (var item in items)
        {
            item.TargetSales = (long)Math.Ceiling(item.DestinationSales / 100f * setting.SalesPercent);
        }
    }

    private static void CalNetProfit(List<ScalperItem> items)
    {
        foreach (var item in items)
        {
            item.NetProfit = item.SellPrice - item.BuyPrice;
            item.TargetNetProfit = item.NetProfit * item.TargetSales;
        }
    }

    private static void CalROI(List<ScalperItem> items)
    {
        foreach (var item in items)
        {
            item.ROI = item.BuyPrice == 0 ? 0 : item.NetProfit / item.BuyPrice * 100;
        }
    }

    private static void CalPrincipal(List<ScalperItem> items)
    {
        foreach (var item in items)
        {
            item.Principal = item.BuyPrice * item.TargetSales;
        }
    }

    /// <summary>历史价格波动 = 标准差 / 平均值。</summary>
    private static void CalHistoryPriceFluctuation(List<ScalperItem> items, ScalperSetting setting)
    {
        foreach (var item in items)
        {
            var history = item.DestinationStatistics?.Where(p => p.Date > DateTime.Now.AddDays(-setting.HistoryPriceFluctuationDay - 2)).ToList();
            if (history is not { Count: > 0 })
            {
                continue;
            }

            var avg = history.Average(p => p.Average);
            var sum = history.Sum(p => Math.Pow(p.Average - avg, 2));
            var std = Math.Sqrt(sum / history.Count);
            item.HistoryPriceFluctuation = avg == 0 ? 0 : std / avg;
        }
    }

    /// <summary>当前价格波动 = |卖出价 - 历史均价| / 历史均价。</summary>
    private static void CalNowPriceFluctuation(List<ScalperItem> items, ScalperSetting setting)
    {
        foreach (var item in items)
        {
            var history = item.DestinationStatistics?.Where(p => p.Date > DateTime.Now.AddDays(-setting.NowPriceFluctuationDay - 2)).ToList();
            if (history is not { Count: > 0 })
            {
                continue;
            }

            var avg = history.Average(p => p.Average);
            item.NowPriceFluctuation = avg == 0 ? 0 : Math.Abs(item.SellPrice - avg) / avg;
        }
    }

    /// <summary>订单饱和度 = 有效价格范围内的挂单量 / 日销量。</summary>
    private static void CalSaturation(List<ScalperItem> items, ScalperSetting setting)
    {
        foreach (var item in items)
        {
            if (item.DestinationSales > 0 && item.DestinationSellOrders is { Count: > 0 })
            {
                var validPrice = item.SellPrice * (1 + setting.SaturationFluctuation / 100);
                var validVolume = item.DestinationSellOrders.Where(p => p.Price < validPrice).Sum(p => (long)p.VolumeRemain);
                item.Saturation = (double)validVolume / item.DestinationSales;
            }
        }
    }

    /// <summary>
    /// 加热度（阶段 298）：按阈值模式判定“热天”，强度加权后归一到 0~100%。
    /// 绝对模式：销量 ≥ HeatValueThreshold；相对模式：销量 ≥ 物品日均销量 × HeatRelativeFactor。
    /// 每个热天强度 = min(销量/阈值, 3)，冷天 0；有数据天数不足窗口一半时按比例打折（数据置信度）。
    /// </summary>
    private static void CalHeatValue(List<ScalperItem> items, ScalperSetting setting)
    {
        foreach (var item in items)
        {
            var history = item.DestinationStatistics?.Where(p => p.Date > DateTime.Now.AddDays(-setting.HeatValueDay - 2)).ToList();
            if (history is not { Count: > 0 })
            {
                continue;
            }

            double averageVolume = 0;
            if (setting.HeatThresholdMode == ScalperSetting.HeatThresholdKind.RelativeToAverage)
            {
                averageVolume = history.Average(p => (double)p.Volume);
            }

            var threshold = setting.HeatThresholdMode == ScalperSetting.HeatThresholdKind.RelativeToAverage
                ? averageVolume * Math.Max(setting.HeatRelativeFactor, 0.01)
                : (double)Math.Max(setting.HeatValueThreshold, 1);

            // 窗口期望天数：ESI 数据可能不满窗口，这里以“设置的天数”为分母，同时用数据天数做置信度。
            var expectedDays = Math.Max(setting.HeatValueDay, 1);
            var weighted = 0.0;
            foreach (var day in history)
            {
                var strength = Math.Min((double)day.Volume / threshold, 3);   // 超卖封顶 3 倍，防单日异常值
                weighted += strength;
            }

            var confidence = Math.Clamp(history.Count / (double)expectedDays, 0.25, 1);
            item.HeatRatio = Math.Clamp(weighted / expectedDays * confidence, 0, 1);
        }
    }

    /// <summary>源市场与目的市场的最短星门跳数，及其上平摊的目标净利润。</summary>
    private static void CalIskPerJump(List<ScalperItem> items, ScalperSetting setting)
    {
        var source = setting.SourceMarketLocation;
        var destination = setting.DestinationMarketLocation;
        if (source is null || destination is null)
        {
            return;
        }

        var path = Core.EVEHelpers.ShortestPathHelper.CalStargatePath(source.SolarSystemId, destination.SolarSystemId, null, null);
        if (path is { Count: > 0 })
        {
            var jump = path.Count;
            foreach (var item in items)
            {
                item.Jumps = jump;
                item.IskPerJump = item.TargetNetProfit / jump;
            }
        }
        else
        {
            Core.Log.Warn($"Can not found path between {source.Name} and {destination.Name}");
        }
    }

    private static void CalIskPerVolume(List<ScalperItem> items)
    {
        foreach (var item in items)
        {
            var volume = item.TargetVolume;
            item.IskPerVolume = volume == 0 ? 0 : item.TargetNetProfit / volume;
        }
    }
    /// <summary>阶段 296：按目标数量走订单簿并扣销售税/经纪费，算税后利润、覆盖率、竞争、卖完时间与风险指标。</summary>
    private static void CalExecutionMetrics(List<ScalperItem> items, ScalperSetting setting)
    {
        var buyHistory = setting.IsHistoryBuyPrice;
        var sellHistory = setting.IsHistorySellPrice;
        var (_, buyIsBuyOrders) = ResolvePriceFunc(setting.BuyPrice);
        var (_, sellIsBuyOrders) = ResolvePriceFunc(setting.SellPrice);
        var tax = Math.Clamp(setting.SalesTaxRate, 0, 100) / 100;
        var broker = Math.Clamp(setting.BrokerFeeRate, 0, 100) / 100;
        var band = Math.Clamp(setting.CompetitionBandPercent, 0, 100);

        foreach (var item in items)
        {
            var target = item.TargetSales > 0 ? item.TargetSales : 1;

            // 买入：吃源市场卖单（价升序）；历史口径或挂买单口径沿用算出的买价
            long buyFilled = target;
            double buyValue = item.BuyPrice * target;
            if (!buyHistory)
            {
                var (filled, value) = WalkBook(
                    buyIsBuyOrders ? item.SourceBuyOrders : item.SourceSellOrders, target, !buyIsBuyOrders);
                if (filled > 0)
                {
                    buyFilled = filled;
                    buyValue = value;
                }
            }

            // 卖出：吃目的市场买单（价降序）；挂卖单/历史口径沿用算出的卖价
            long sellFilled = target;
            double sellValue = item.SellPrice * target;
            if (!sellHistory && sellIsBuyOrders)
            {
                var (filled, value) = WalkBook(item.DestinationBuyOrders, target, false);
                if (filled > 0)
                {
                    sellFilled = filled;
                    sellValue = value;
                }
            }

            var executed = Math.Max(1, Math.Min(target, Math.Min(buyFilled, sellFilled)));
            item.FillRatio = (double)executed / target;
            // 阶段 299：走簿缩放一致——成交均价同样乘"价格缩放系数"，与挂单/历史口径可比
            item.BuyAvgPrice = buyFilled > 0 ? buyValue / buyFilled * setting.BuyPriceScale : item.BuyPrice;
            item.SellAvgPrice = sellFilled > 0 ? sellValue / sellFilled * setting.SellPriceScale : item.SellPrice;

            var buyUnit = item.BuyAvgPrice * (1 + broker);
            var sellUnit = item.SellAvgPrice * (1 - tax - broker);
            item.NetProfitAfterFees = sellUnit - buyUnit;
            item.TargetNetProfitAfterFees = item.NetProfitAfterFees * executed;
            item.ROIAfterFees = buyUnit > 0 ? item.NetProfitAfterFees / buyUnit * 100 : 0;

            double competing = 0;
            if (item.DestinationSellOrders is { Count: > 0 })
            {
                var ceiling = item.SellAvgPrice * (1 + band / 100);
                competing = item.DestinationSellOrders.Where(p => p.Price <= ceiling).Sum(p => (long)p.VolumeRemain);
            }

            item.CompetitionShare = competing <= 0
                ? 1
                : Math.Clamp(item.TargetSales / (item.TargetSales + competing), 0, 1);

            // 阶段 299：挂卖单/历史卖价策略——可成交量受"目的市场日销量 × 我的份额 × 可接受天数"截断；
            // 吃买单策略是即时成交，不受该截断（买单本身就是需求）。
            if (!sellHistory && sellIsBuyOrders)
            {
                var dailyShare = item.DestinationSales * 1.0;
                item.DaysToSell = dailyShare > 0 ? executed / dailyShare : 3650;
            }
            else
            {
                var maxSellDays = Math.Clamp(setting.MaxSellDays, 1, 3650);
                var dailyShare = item.DestinationSales * item.CompetitionShare;
                var sellable = dailyShare > 0 ? Math.Min(target, dailyShare * maxSellDays) : 0;
                executed = Math.Max(1, Math.Min(executed, (long)Math.Round(sellable)));
                item.FillRatio = (double)executed / target;
                item.TargetNetProfitAfterFees = item.NetProfitAfterFees * executed;
                item.DaysToSell = dailyShare > 0 ? Math.Min(3650, executed / dailyShare) : 3650;
            }
            item.ProfitPerHour = item.TargetNetProfitAfterFees / Math.Max(item.DaysToSell * 24, 1);
            item.RiskAdjustedProfit = item.TargetNetProfitAfterFees
                - Math.Abs(item.HistoryPriceFluctuation) * buyUnit * executed * Math.Clamp(setting.RiskAversion, 0, 100);
            item.ProfitPerVolumeAfterFees = item.TargetVolume > 0 ? item.TargetNetProfitAfterFees / item.TargetVolume : 0;
        }
    }

    /// <summary>
    /// 阶段 299：静态预筛——不取历史，只用两侧订单簿判断"该物品是否值得继续分析"。
    /// 仅当买/卖价格口径<b>都不</b>是历史口径时可按订单簿估算；否则返回 <c>null</c>（调用方应全量拉历史）。
    /// 过滤条件：两侧订单簿存在、按口径可得正的税后首档价差，且卖侧深度非空。
    /// </summary>
    public static List<ScalperItem>? Prescreen(List<ScalperItem> items, ScalperSetting setting)
    {
        var (_, buyIsBuyOrders) = ResolvePriceFunc(setting.BuyPrice);
        var (_, sellIsBuyOrders) = ResolvePriceFunc(setting.SellPrice);
        if (setting.IsHistoryBuyPrice || setting.IsHistorySellPrice)
        {
            return null;
        }

        var tax = Math.Clamp(setting.SalesTaxRate, 0, 100) / 100;
        var broker = Math.Clamp(setting.BrokerFeeRate, 0, 100) / 100;
        var kept = new List<ScalperItem>();
        foreach (var item in items)
        {
            var buyOrders = buyIsBuyOrders ? item.SourceBuyOrders : item.SourceSellOrders;
            var sellOrders = sellIsBuyOrders ? item.DestinationBuyOrders : item.DestinationSellOrders;
            if (buyOrders is not { Count: > 0 } || sellOrders is not { Count: > 0 })
            {
                continue;
            }

            var buy = buyIsBuyOrders ? buyOrders.Max(p => p.Price) : buyOrders.Min(p => p.Price);
            var sell = sellIsBuyOrders ? sellOrders.Max(p => p.Price) : sellOrders.Min(p => p.Price);
            if (sell * (1 - tax - broker) - buy * (1 + broker) <= 0)
            {
                continue;
            }

            kept.Add(item);
        }

        return kept;
    }

    /// <summary>按目标数量走订单簿：升序从最低价开始（卖单侧），降序从最高价开始（买单侧）。</summary>
    private static (long Filled, double Value) WalkBook(List<MarketOrder>? orders, long quantity, bool ascending)
    {
        if (orders is not { Count: > 0 } || quantity <= 0)
        {
            return (0, 0);
        }

        long filled = 0;
        double value = 0;
        foreach (var order in ascending ? orders.OrderBy(p => p.Price) : orders.OrderByDescending(p => p.Price))
        {
            var remain = (long)order.VolumeRemain;
            var take = Math.Min(remain, quantity - filled);
            if (take <= 0)
            {
                continue;
            }

            filled += take;
            value += take * order.Price;
            if (filled >= quantity)
            {
                break;
            }
        }

        return (filled, value);
    }


    /// <summary>
    /// 阶段 299：数据质量与风险质量指标——趋势（目的市场历史均价线性回归）、
    /// 当前卖价相对历史均价的安全垫、目的市场日销量变异系数、数据置信度。
    /// </summary>
    private static void CalQualityMetrics(List<ScalperItem> items, ScalperSetting setting)
    {
        var window = Math.Max(setting.HistoryPriceFluctuationDay, 1);
        var salesWindow = Math.Max(setting.DestinationSalesDay, 1);
        foreach (var item in items)
        {
            var history = item.DestinationStatistics?
                .Where(p => p.Date > DateTime.Now.AddDays(-window))
                .OrderBy(p => p.Date)
                .ToList();
            if (history is { Count: > 0 })
            {
                // 线性回归斜率：x=天序号，y=均价 → 每日变化 / 均值
                var n = history.Count;
                var meanX = (n - 1) / 2.0;
                var meanY = history.Average(p => (double)p.Average);
                double sxy = 0, sxx = 0;
                for (var i = 0; i < n; i++)
                {
                    sxy += (i - meanX) * (history[i].Average - meanY);
                    sxx += (i - meanX) * (i - meanX);
                }

                item.PriceTrend = meanY > 0 && sxx > 0 ? sxy / sxx / meanY * 100 : 0;

                // 当前卖价相对该窗口均价的带方向偏离（正 = 高于历史均价）
                item.PriceCushion = meanY > 0 && item.SellAvgPrice > 0
                    ? (item.SellAvgPrice - meanY) / meanY * 100
                    : 0;
            }

            var salesHistory = item.DestinationStatistics?
                .Where(p => p.Date > DateTime.Now.AddDays(-salesWindow))
                .ToList();
            if (salesHistory is { Count: > 0 })
            {
                var mean = salesHistory.Average(p => (double)p.Volume);
                var variance = salesHistory.Average(p => Math.Pow(p.Volume - mean, 2));
                item.SalesCV = mean > 0 ? Math.Sqrt(variance) / mean : 0;
            }

            var confidenceDays = item.DestinationStatistics?.Count ?? 0;
            item.Confidence = Math.Clamp(confidenceDays / (double)Math.Max(setting.DestinationSalesDay, 1), 0, 1);
        }
    }

    /// <summary>
    /// 阶段 299：可解释推荐度 <c>Score</c>（0~100 绝对分，跨批可比）。
    /// 每个有权重的指标做稳健归一（中位数/MAD 的 z 分，截断 ±3），按方向加权平均；
    /// <c>ScoreDetail</c> 给出正/负贡献最大的几项，供悬浮提示。
    /// </summary>
    private static void CalScore(List<ScalperItem> items, ScalperSetting setting)
    {
        if (items.Count == 0)
        {
            return;
        }

        var metrics = new (Func<ScalperItem, double> Value, double Weight, int Sign, string LabelKey)[]
        {
            (p => p.ROI,                             setting.SuggestionROI,                             1, "BusinessPage_ROIPercent"),
            (p => p.TargetNetProfit,                 setting.SuggestionNetProfit,                       1, "BusinessPage_TargetSalesNetProfit"),
            (p => p.Principal,                       setting.SuggestionPrincipal,                      -1, "BusinessPage_Principal"),
            (p => p.DestinationSales,                setting.SuggestionSales,                           1, "BusinessPage_DaySales"),
            (p => p.HistoryPriceFluctuation,         setting.SuggestionHistoryPriceFluctuation,        -1, "BusinessPage_DestinationHistoryPriceFluctuation"),
            (p => Math.Max(0, p.PriceCushion),       setting.SuggestionNowPriceFluctuation,             1, "BusinessPage_DestinationNowPriceFluctuationDay"),
            (p => p.Saturation,                      setting.SuggestionSaturation,                     -1, "BusinessPage_SellOrderSaturation"),
            (p => p.HeatRatio,                       setting.SuggestionHeatValue,                       1, "BusinessPage_HeatValue"),
            (p => p.IskPerVolume,                    setting.SuggestionIskPerVolume,                    1, "BusinessPage_IskPerVolume"),
            (p => p.NetProfitAfterFees,              setting.SuggestionNetProfitAfterFees,              1, "BusinessPage_NetProfitAfterFees"),
            (p => p.ROIAfterFees,                    setting.SuggestionROIAfterFees,                    1, "BusinessPage_ROIAfterFees"),
            (p => p.FillRatio * 100,                 setting.SuggestionFillRatio,                       1, "BusinessPage_FillRatio"),
            (p => p.CompetitionShare * 100,          setting.SuggestionCompetitionShare,                1, "BusinessPage_CompetitionShare"),
            (p => p.DaysToSell,                      setting.SuggestionDaysToSell,                     -1, "BusinessPage_DaysToSell"),
            (p => p.ProfitPerHour,                   setting.SuggestionProfitPerHour,                   1, "BusinessPage_ProfitPerHour"),
            (p => p.RiskAdjustedProfit,              setting.SuggestionRiskAdjustedProfit,              1, "BusinessPage_RiskAdjustedProfit"),
            (p => p.ProfitPerVolumeAfterFees,        setting.SuggestionProfitPerVolumeAfterFees,        1, "BusinessPage_ProfitPerVolumeAfterFees"),
            (p => p.PriceTrend,                      setting.SuggestionPriceTrend,                      1, "BusinessPage_PriceTrend"),
            (p => Math.Max(0, p.PriceCushion),       setting.SuggestionPriceCushion,                    1, "BusinessPage_PriceCushion"),
            (p => p.SalesCV * 100,                   setting.SuggestionSalesStability,                 -1, "BusinessPage_SalesStability"),
            (p => p.Confidence * 100,                setting.SuggestionConfidence,                      1, "BusinessPage_Confidence"),
        };

        var active = metrics.Where(p => p.Weight > 0).ToArray();
        if (active.Length == 0)
        {
            return;
        }

        var totalWeight = active.Sum(p => p.Weight);
        var contributions = new double[items.Count, active.Length];
        var details = new string[items.Count];

        for (var m = 0; m < active.Length; m++)
        {
            var values = items.Select(active[m].Value).ToArray();
            var sorted = values.OrderBy(p => p).ToArray();
            var median = sorted.Length % 2 == 1
                ? sorted[sorted.Length / 2]
                : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
            var abs = sorted.Select(p => Math.Abs(p - median)).OrderBy(p => p).ToArray();
            var mad = abs.Length % 2 == 1 ? abs[abs.Length / 2] : (abs[abs.Length / 2 - 1] + abs[abs.Length / 2]) / 2;
            var scale = mad * 1.4826;
            if (scale <= 0)
            {
                var range = sorted[^1] - sorted[0];
                scale = range > 0 ? range / 4 : 1;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var z = (values[i] - median) / scale;
                contributions[i, m] = Math.Clamp((z * active[m].Sign) / 3, -1, 1);
            }
        }

        for (var i = 0; i < items.Count; i++)
        {
            double score = 0, best = -1, worst = 1;
            string worstKey = string.Empty;
            string bestKey = string.Empty;
            for (var m = 0; m < active.Length; m++)
            {
                var c = contributions[i, m];
                score += c * active[m].Weight;
                if (c > best)
                {
                    best = c;
                    bestKey = active[m].LabelKey;
                }

                if (c < worst)
                {
                    worst = c;
                    worstKey = active[m].LabelKey;
                }
            }

            items[i].Score = Math.Clamp(score / totalWeight * 100 + 50, 0, 100);
            var bestPts = Math.Round(best * active.FirstOrDefault(p => p.LabelKey == bestKey).Weight / totalWeight * 100, 1);
            var worstPts = Math.Round(worst * active.FirstOrDefault(p => p.LabelKey == worstKey).Weight / totalWeight * 100, 1);
            details[i] = $"+{LanguageService.Get(bestKey)} {bestPts:+0.#;-0.#} / {LanguageService.Get(worstKey)} {worstPts:+0.#;-0.#}";
        }

        for (var i = 0; i < items.Count; i++)
        {
            items[i].ScoreDetail = details[i];
        }
    }

    /// <summary>推荐度：各指标按排名加权累加后再归一到 0~100（与原实现一致）。</summary>
    private static void CalSuggestion(List<ScalperItem> items, ScalperSetting setting)
    {
        if (items.Count == 0)
        {
            return;
        }

        var count = (double)items.Count;

        Accumulate(items.OrderBy(p => p.ROI), item => item.ROI > 0, setting.SuggestionROI, count);
        Accumulate(items.OrderBy(p => p.TargetNetProfit), item => item.TargetNetProfit > 0, setting.SuggestionNetProfit, count);
        Accumulate(items.OrderBy(p => p.Principal), _ => true, setting.SuggestionPrincipal, count);
        Accumulate(items.OrderBy(p => p.DestinationSales), _ => true, setting.SuggestionSales, count);
        Accumulate(items.OrderByDescending(p => p.HistoryPriceFluctuation), _ => true, setting.SuggestionHistoryPriceFluctuation, count);
        Accumulate(items.OrderByDescending(p => p.NowPriceFluctuation), _ => true, setting.SuggestionNowPriceFluctuation, count);
        Accumulate(items.OrderByDescending(p => p.Saturation), item => item.Saturation > 0, setting.SuggestionSaturation, count);

        // 加热度占比按"值分组"排名（同值同分）；阶段 298 起用归一化的 HeatRatio
        var groups = items.GroupBy(p => p.HeatRatio).OrderBy(p => p.Key).ToList();
        var rank = 1;
        foreach (var group in groups)
        {
            var score = setting.SuggestionHeatValue * rank / groups.Count;
            foreach (var item in group)
            {
                item.Suggestion += score;
            }

            rank++;
        }

        // ISK/单位体积：正收益按升序加分，负收益按降序扣分
        var index = 1;
        foreach (var item in items.Where(p => p.IskPerVolume > 0).OrderBy(p => p.IskPerVolume))
        {
            item.Suggestion += setting.SuggestionIskPerVolume * index / count;
            index++;
        }

        index = 1;
        foreach (var item in items.Where(p => p.IskPerVolume <= 0).OrderByDescending(p => p.IskPerVolume))
        {
            item.Suggestion -= setting.SuggestionIskPerVolume * index / count;
            index++;
        }


        // 阶段 296：新增指标（权重由玩家在设置里调整）
        Accumulate(items.OrderBy(p => p.NetProfitAfterFees), p => p.NetProfitAfterFees > 0, setting.SuggestionNetProfitAfterFees, count);
        Accumulate(items.OrderBy(p => p.ROIAfterFees), p => p.ROIAfterFees > 0, setting.SuggestionROIAfterFees, count);
        Accumulate(items.OrderBy(p => p.FillRatio), _ => true, setting.SuggestionFillRatio, count);
        Accumulate(items.OrderBy(p => p.CompetitionShare), _ => true, setting.SuggestionCompetitionShare, count);
        // 卖完天数越短越好：天数大的排在前面（得分低）
        Accumulate(items.OrderByDescending(p => p.DaysToSell), _ => true, setting.SuggestionDaysToSell, count);
        Accumulate(items.OrderBy(p => p.ProfitPerHour), _ => true, setting.SuggestionProfitPerHour, count);
        Accumulate(items.OrderBy(p => p.RiskAdjustedProfit), p => p.RiskAdjustedProfit > 0, setting.SuggestionRiskAdjustedProfit, count);
        Accumulate(items.OrderBy(p => p.ProfitPerVolumeAfterFees), p => p.ProfitPerVolumeAfterFees > 0, setting.SuggestionProfitPerVolumeAfterFees, count);
        Accumulate(items.OrderBy(p => p.PriceTrend), _ => true, setting.SuggestionPriceTrend, count);
        Accumulate(items.OrderBy(p => p.PriceCushion), _ => true, setting.SuggestionPriceCushion, count);
        Accumulate(items.OrderByDescending(p => p.SalesCV), _ => true, setting.SuggestionSalesStability, count);
        Accumulate(items.OrderBy(p => p.Confidence), _ => true, setting.SuggestionConfidence, count);

        // 转成百分比
        index = 1;
        foreach (var item in items.OrderBy(p => p.Suggestion))
        {
            item.Suggestion = index / count * 100;
            index++;
        }
    }

    private static void Accumulate(IEnumerable<ScalperItem> ordered, Func<ScalperItem, bool> predicate, double weight, double count)
    {
        var index = 1;
        foreach (var item in ordered)
        {
            if (predicate(item))
            {
                item.Suggestion += weight * index / count;
            }

            index++;
        }
    }
}
