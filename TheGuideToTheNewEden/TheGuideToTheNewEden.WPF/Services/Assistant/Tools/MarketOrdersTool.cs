using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MarketOrder = TheGuideToTheNewEden.Core.Models.Market.Order;
using TheGuideToTheNewEden.WPF.Services.Business;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>查某个物品在某星域的市场订单：卖单最低/买单最高前 N 条 + 5% 均价/均价/条数（口径对齐市场页）。</summary>
public sealed class MarketOrdersTool : ITool
{
    public string Name => "market_orders";

    public string DisplayNameKey => "AssistantTool_market_orders";

    public string Description =>
        "查询物品在星域的市场订单：卖单最低价、买单最高价各前几条（价格/数量/位置/星系），" +
        "以及两侧的 5% 均价、均价与订单条数。适合「物品 X 在 Y 多少钱」。只读，不含合同与历史。" +
        "价格为工具数据，禁止自行计算或改写。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["itemName"] = new JsonObject { ["type"] = "string", ["description"] = "物品名（游戏内名称，中英文均可）" },
            ["regionName"] = new JsonObject { ["type"] = "string", ["description"] = "星域名（默认 伏尔戈）" },
            ["limit"] = new JsonObject { ["type"] = "integer", ["description"] = "每侧返回条数（默认 5，上限 10）" },
        },
        ["required"] = new JsonArray { "itemName" },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var itemName = ToolArgs.ReadString(args, "itemName");
        var regionName = ToolArgs.ReadString(args, "regionName") ?? "伏尔戈";
        var limit = Math.Clamp(ToolArgs.ReadInt(args, "limit", 5), 1, 10);

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
            return new ToolResult($"未找到星域「{regionName}」，请用星域全名（如 伏尔戈 / The Forge）。");
        }

        var orders = await MarketOrderService.Current.GetRegionOrdersAsync(invType.TypeID, region.RegionID, ct);
        if (orders is null)
        {
            return new ToolResult($"查询失败：{invType.TypeName} 在 {region.RegionName} 的订单获取失败（ESI 不可用或限流）。");
        }

        await MarketOrderService.EnrichOrdersAsync(orders, ct);

        var sells = orders.Where(o => !o.IsBuyOrder).OrderBy(o => o.Price).ToList();
        var buys = orders.Where(o => o.IsBuyOrder).OrderByDescending(o => o.Price).ToList();

        var builder = new StringBuilder();
        builder.AppendLine($"{invType.TypeName} @ {region.RegionName}");
        builder.AppendLine(FormatSide("卖单", sells, limit));
        builder.AppendLine(FormatSide("买单", buys, limit));
        builder.AppendLine($"卖 5%均价={ToolArgs.FormatIsk(FivePercentAverage(sells))} 卖均价={ToolArgs.FormatIsk(Average(sells))} 卖单数={sells.Count}");
        builder.AppendLine($"买 5%均价={ToolArgs.FormatIsk(FivePercentAverage(buys))} 买均价={ToolArgs.FormatIsk(Average(buys))} 买单数={buys.Count}");

        return new ToolResult(
            builder.ToString().TrimEnd(),
            ParamSummary: $"{invType.TypeName} @ {region.RegionName}");
    }

    /// <summary>一侧订单摘要：`卖单(前5/共1414): 1,300,000 ×12 位置(星系); …`</summary>
    private static string FormatSide(string label, List<MarketOrder> orders, int limit)
    {
        if (orders.Count == 0)
        {
            return $"{label}: 无";
        }

        var builder = new StringBuilder();
        builder.Append($"{label}(前{Math.Min(limit, orders.Count)}/共{orders.Count}): ");
        var rows = new List<string>();
        foreach (var order in orders.Take(limit))
        {
            var location = string.IsNullOrWhiteSpace(order.LocationName) ? order.LocationId.ToString() : order.LocationName;
            var system = order.SolarSystem?.SolarSystemName;
            var at = string.IsNullOrWhiteSpace(system) ? string.Empty : $"({system})";
            rows.Add($"{ToolArgs.FormatIsk(order.Price)} ×{order.VolumeRemain} {location}{at}");
        }

        builder.Append(string.Join("; ", rows));
        return builder.ToString();
    }

    /// <summary>5% 均价：按排序取前 ceil(5%×条数) 条均价（与市场页口径一致；空列表返回 0）。</summary>
    private static double FivePercentAverage(List<MarketOrder> orders)
    {
        if (orders.Count == 0)
        {
            return 0;
        }

        var count = Math.Max(1, (int)Math.Ceiling(orders.Count * 0.05));
        return orders.Take(count).Average(o => o.Price);
    }

    private static double Average(List<MarketOrder> orders)
        => orders.Count == 0 ? 0 : orders.Average(o => o.Price);
}
