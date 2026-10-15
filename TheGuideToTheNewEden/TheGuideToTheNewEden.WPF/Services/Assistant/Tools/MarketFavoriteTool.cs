using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Business;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 市场收藏写入（**写操作，需确认**）：把物品加入/移出「市场」页的收藏列表（与 WinUI 共用
/// <c>Configs/StaredMarketInvType.json</c>）。
/// <para>
/// 收藏是用户自己维护的清单，助手只在用户明确说「收藏一下 X」时写；与页面同一个服务
/// （<see cref="MarketStarService"/>），页面上的收藏列表会立即同步（服务发变更事件）。
/// </para>
/// </summary>
public sealed class MarketFavoriteTool : ITool
{
    public string Name => "market_favorite";

    public string DisplayNameKey => "AssistantTool_market_favorite";

    public string Description =>
        "把物品加入或移出「市场」页的收藏列表（收藏会出现在市场页左侧的「收藏」页签里）。" +
        "参数 itemName 用游戏内物品名（中英文均可，必须是唯一命中的名称）；action 选 add（默认，收藏）或 remove（取消收藏）。" +
        "写操作需要用户确认；适合用户明确说「收藏一下 XX」「把 XX 从收藏里去掉」的场景。" +
        "已有的收藏可用 market_orders / market_trend 查行情，不需要先收藏。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["itemName"] = new JsonObject { ["type"] = "string", ["description"] = "物品名（游戏内名称，中英文均可）" },
            ["action"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "add", "remove" }, ["description"] = "add（默认）= 收藏；remove = 取消收藏" },
        },
        ["required"] = new JsonArray { "itemName" },
    };

    public bool RequiresConfirmation => true;

    public bool IsAction => false;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var name = ToolArgs.ReadString(args, "itemName")?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult(new ToolResult("参数 itemName（物品名）不能为空。"));
        }

        var action = (ToolArgs.ReadString(args, "action") ?? "add").Trim().ToLowerInvariant();

        var invType = ToolNameResolver.ResolveItem(name);
        if (invType is null)
        {
            return Task.FromResult(new ToolResult($"未找到物品「{name}」（或名称不唯一）。请用完整的游戏内物品名。"));
        }

        var service = MarketStarService.Current;
        if (action == "remove")
        {
            if (!service.IsStared(invType.TypeID))
            {
                return Task.FromResult(new ToolResult($"「{invType.TypeName}」本来就不在收藏里，无需取消。"));
            }

            service.Remove(invType.TypeID);
            return Task.FromResult(new ToolResult(
                $"已取消收藏：{invType.TypeName}（TypeId {invType.TypeID}）。",
                ParamSummary: $"取消收藏 {invType.TypeName}"));
        }

        if (service.IsStared(invType.TypeID))
        {
            return Task.FromResult(new ToolResult($"「{invType.TypeName}」已经在收藏里了（TypeId {invType.TypeID}）。"));
        }

        service.Add(invType.TypeID);
        return Task.FromResult(new ToolResult(
            $"已收藏：{invType.TypeName}（TypeId {invType.TypeID}）。市场页左侧「收藏」页签里可以看到它。",
            ParamSummary: $"收藏 {invType.TypeName}"));
    }
}
