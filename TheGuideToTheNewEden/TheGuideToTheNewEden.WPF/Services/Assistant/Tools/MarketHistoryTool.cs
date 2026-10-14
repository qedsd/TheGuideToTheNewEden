using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Business;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>市场历史统计：区间高/低/均价、现价分位、销量方向（走本地 TTL 缓存）。</summary>
public sealed class MarketHistoryTool : ITool
{
    public string Name => "market_history";

    public string DisplayNameKey => "AssistantTool_market_history";

    public string Description =>
        "查询某个物品在某星域的市场历史统计：区间最高/最低/均价、现价相对区间位置、近 N 天销量与前期对比。" +
        "参数 itemName 用游戏内物品名，regionName 用星域名（默认 伏尔戈），days 是统计区间（默认 90）。" +
        "适合「XX 最近走势怎么样」「XX 是涨是跌」。返回的统计数字是工具数据，禁止自行计算。";

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
        if (history is null)
        {
            return new ToolResult($"查询失败：{invType.TypeName} 的历史获取失败（ESI 不可用或限流）。");
        }

        var now = DateTime.UtcNow;
        var recent = history.Where(h => h.Date > now.AddDays(-days)).OrderBy(h => h.Date).ToList();
        if (recent.Count == 0)
        {
            return new ToolResult($"该星域在最近 {days} 天没有「{invType.TypeName}」的历史成交记录。");
        }

        var half = recent.Count / 2;
        var firstHalf = recent.Take(half).ToList();
        var secondHalf = half > 0 ? recent.Skip(half).ToList() : recent;
        var volumeDirection = firstHalf.Count > 0 && secondHalf.Count > 0
            ? (secondHalf.Average(h => h.Volume) >= firstHalf.Average(h => h.Volume) ? "升" : "降")
            : "—";

        var builder = new StringBuilder();
        builder.AppendLine($"{invType.TypeName} @ {region.RegionName} · 近 {days} 天 {recent.Count} 个统计日");
        builder.AppendLine($"最高 {ToolArgs.FormatIsk(recent.Max(h => h.Highest))} · 最低 {ToolArgs.FormatIsk(recent.Min(h => h.Lowest))} · 均价 {ToolArgs.FormatIsk(recent.Average(h => h.Average))}");
        var latest = recent[^1].Average;
        var min = recent.Min(h => h.Average);
        var max = recent.Max(h => h.Average);
        var percentile = max > min ? (int)Math.Round((latest - min) / (max - min) * 100) : 50;
        builder.AppendLine($"最新均价 {ToolArgs.FormatIsk(latest)} · 位于区间 {percentile}% 位置 · 成交量 {volumeDirection}");

        return new ToolResult(builder.ToString().TrimEnd(), ParamSummary: $"{invType.TypeName} @ {region.RegionName} {days}天");
    }
}
