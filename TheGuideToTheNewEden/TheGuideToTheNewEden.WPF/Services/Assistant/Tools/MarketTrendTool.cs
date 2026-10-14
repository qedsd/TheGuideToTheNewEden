using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Business;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>价格走势分析：当前实时最低卖/最高买 + 历史区间统计（分位/销量方向），供 AI 解读成走势结论。</summary>
public sealed class MarketTrendTool : ITool
{
    public string Name => "market_trend";

    public string DisplayNameKey => "AssistantTool_market_trend";

    public string Description =>
        "分析某个物品在某星域的价格走势：当前实时最低卖价/最高买价、历史区间最高/最低/均价、现价相对区间位置（分位）、" +
        "销量近 N 天与前期对比。参数 itemName 用游戏内物品名，regionName 用星域名（默认 伏尔戈），days 统计区间（默认 90）。" +
        "适合「XX 走势怎么样」「现在该买还是该等」。返回的统计数字是工具数据，禁止自行计算或估算。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["itemName"] = new JsonObject { ["type"] = "string", ["description"] = "物品名（游戏内名称，中英文均可）" },
            ["regionName"] = new JsonObject { ["type"] = "string", ["description"] = "星域名（默认 伏尔戈）" },
            ["days"] = new JsonObject { ["type"] = "integer", ["description"] = "统计区间天数（默认 90）" },
        },
        ["required"] = new JsonArray { "itemName" },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var itemName = ToolArgs.ReadString(args, "itemName");
        var regionName = ToolArgs.ReadString(args, "regionName") ?? "伏尔戈";
        var days = Math.Clamp(ToolArgs.ReadInt(args, "days", 90), 1, 365);

        if (string.IsNullOrWhiteSpace(itemName))
        {
            return new ToolResult("参数 itemName 不能为空。");
        }

        var invType = ToolNameResolver.ResolveItem(itemName);
        if (invType is null)
        {
            return new ToolResult($"未找到物品「{itemName}」，请用游戏内的完整名称（中英文均可）。");
        }

        var region = ToolNameResolver.ResolveRegion(regionName);
        if (region is null)
        {
            return new ToolResult($"未找到星域「{regionName}」。");
        }

        var history = await MarketOrderService.Current.GetHistoryAsync(invType.TypeID, region.RegionID);
        var orders = await MarketOrderService.Current.GetRegionOrdersAsync(invType.TypeID, region.RegionID, ct);

        var builder = new StringBuilder();
        builder.AppendLine($"{invType.TypeName} @ {region.RegionName}");

        if (orders is { Count: > 0 })
        {
            var sells = orders.Where(o => !o.IsBuyOrder).ToList();
            var buys = orders.Where(o => o.IsBuyOrder).ToList();
            var minSell = sells.Count > 0 ? sells.Min(o => o.Price) : (double?)null;
            var maxBuy = buys.Count > 0 ? buys.Max(o => o.Price) : (double?)null;
            builder.AppendLine($"当前：最低卖价 {Format(minSell)} · 最高买价 {Format(maxBuy)}");
        }
        else
        {
            builder.AppendLine("当前：无实时订单");
        }

        if (history is not { Count: > 0 })
        {
            builder.AppendLine("历史：该星域暂无此物品的成交记录");
            return new ToolResult(builder.ToString().TrimEnd(), ParamSummary: $"{invType.TypeName} @ {region.RegionName}");
        }

        var now = DateTime.UtcNow;
        var recent = history.Where(h => h.Date > now.AddDays(-days)).OrderBy(h => h.Date).ToList();
        if (recent.Count == 0)
        {
            builder.AppendLine($"历史：最近 {days} 天无成交记录");
            return new ToolResult(builder.ToString().TrimEnd(), ParamSummary: $"{invType.TypeName} @ {region.RegionName}");
        }

        var min = recent.Min(h => h.Average);
        var max = recent.Max(h => h.Average);
        var latest = recent[^1].Average;
        var percentile = max > min ? (int)Math.Round((latest - min) / (max - min) * 100) : 50;
        var half = recent.Count / 2;
        var firstHalf = recent.Take(half).ToList();
        var secondHalf = half > 0 ? recent.Skip(half).ToList() : recent;
        var direction = firstHalf.Count > 0 && secondHalf.Count > 0
            ? (secondHalf.Average(h => h.Volume) >= firstHalf.Average(h => h.Volume) ? "升" : "降")
            : "—";

        builder.AppendLine($"历史 {recent.Count} 个统计日：区间高 {ToolArgs.FormatIsk(max)} · 低 {ToolArgs.FormatIsk(min)} · 均价 {ToolArgs.FormatIsk(recent.Average(h => h.Average))}");
        builder.AppendLine($"最新均价 {ToolArgs.FormatIsk(latest)} · 位于区间 {percentile}% 位置 · 成交量趋势 {direction}");

        return new ToolResult(builder.ToString().TrimEnd(), ParamSummary: $"{invType.TypeName} @ {region.RegionName} {days}天");
    }

    private static string Format(double? value) => value is { } v ? ToolArgs.FormatIsk(v) : "—";
}
