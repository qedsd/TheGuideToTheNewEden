using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>跳到市场页并选中指定物品（动作；唯一命中才跳）。</summary>
public sealed class MarketNavigateTool : ITool
{
    public string Name => "market_navigate";

    public string DisplayNameKey => "AssistantTool_market_navigate";

    public string Description =>
        "打开软件的市场页并选中指定物品（配合「去看看 X 的市场」类问题使用）。参数 itemName 用游戏内物品名（中英文均可）。" +
        "找不到或名称有歧义时返回原因、不跳转。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["itemName"] = new JsonObject { ["type"] = "string", ["description"] = "物品名（游戏内名称，中英文均可）" },
        },
        ["required"] = new JsonArray { "itemName" },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => true;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var itemName = ToolArgs.ReadString(args, "itemName");
        if (string.IsNullOrWhiteSpace(itemName))
        {
            return Task.FromResult(new ToolResult("参数 itemName 不能为空。"));
        }

        var invType = ToolNameResolver.ResolveItem(itemName);
        if (invType is null)
        {
            return Task.FromResult(new ToolResult($"未找到物品「{itemName}」，请用游戏内的完整名称（中英文均可）。"));
        }

        var typeId = invType.TypeID;
        return Task.FromResult(new ToolResult(
            $"已打开市场页并选中 {invType.TypeName}（TypeID {typeId}）。",
            NavigateAction: () => Navigation.NavigateToMarket(typeId),
            ParamSummary: invType.TypeName));
    }
}
