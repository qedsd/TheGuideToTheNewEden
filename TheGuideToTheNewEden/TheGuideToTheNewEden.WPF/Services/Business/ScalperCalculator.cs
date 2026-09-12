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
    {
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
                item.DestinationBuyOrders = list.Where(p => p.IsBuyOrder).OrderBy(p => p.Price).ToList();
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
        CalSuggestion(items, setting);
        return items.OrderByDescending(p => p.Suggestion).ToList();
    }

    /// <summary>市场日销量（源/目的各自按天数、去极值方式与最低/最高/平均口径取）。</summary>
    private static void CalSales(List<ScalperItem> items, ScalperSetting setting)
    {
        foreach (var item in items)
        {
            var history = item.SourceStatistics?.Where(p => p.Date > DateTime.Now.AddDays(-setting.SourceSalesDay - 2)).ToList();
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

            history = item.DestinationStatistics?.Where(p => p.Date > DateTime.Now.AddDays(-setting.DestinationSalesDay - 2)).ToList();
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
                    item.DestinationStatistics,
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

    /// <summary>最低/最高价格订单的前 5% 平均价；不足 2 条时取首条。</summary>
    private static double CalPriceTop5(List<MarketOrder>? orders, List<Statistic>? statistics, long sales, int day, bool removeExtremum)
    {
        if (orders is not { Count: > 0 })
        {
            return -1;
        }

        var top5P = (int)(orders.Count * 0.05);
        return top5P > 1 ? orders.Take(top5P).Average(p => p.Price) : orders[0].Price;
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

        return removeExtremum
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

        return removeExtremum
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

        return removeExtremum
            ? (long)Math.Ceiling((history.Sum(p => p.Lowest) - history.Max(p => p.Lowest)) / (history.Count - 1))
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

    /// <summary>热力值 = 统计区间内 每日销量 ≥ 阈值 +1 否则 -1 的累计。</summary>
    private static void CalHeatValue(List<ScalperItem> items, ScalperSetting setting)
    {
        foreach (var item in items)
        {
            var history = item.DestinationStatistics?.Where(p => p.Date > DateTime.Now.AddDays(-setting.HeatValueDay - 2)).ToList();
            if (history is null)
            {
                continue;
            }

            foreach (var day in history)
            {
                item.HeatValue += day.Volume >= setting.HeatValueThreshold ? 1 : -1;
            }
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

        // 热力值按"值分组"排名（同值同分）
        var groups = items.GroupBy(p => p.HeatValue).OrderBy(p => p.Key).ToList();
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
