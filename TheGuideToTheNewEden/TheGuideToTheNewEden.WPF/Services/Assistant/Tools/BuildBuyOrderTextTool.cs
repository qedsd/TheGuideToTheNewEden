using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Business;
using TheGuideToTheNewEden.WPF.ViewModels.Business;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>生成游戏内"批量购买"文本（与订单页「复制为游戏批量购买」同口径：只取被压单行，每行 <c>物品名 1</c>）。</summary>
public sealed class BuildBuyOrderTextTool : ITool
{
    public string Name => "build_buy_order_text";

    public string DisplayNameKey => "AssistantTool_build_buy_order_text";

    public string Description =>
        "把某角色被压单的未结订单，转换成 EVE 客户端里可粘贴的批量购买文本（每行「物品名 1」），与订单页的" +
        "「复制为游戏批量购买」完全同口径。参数 characterName 用角色全名（省略 = 默认角色）；corporation=true 用军团订单；" +
        "limit 控制最多多少行（默认 30，上限 100）。适合「帮我生成要补货的购买清单」。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["characterName"] = new JsonObject { ["type"] = "string", ["description"] = "角色全名（省略 = 默认角色）" },
            ["corporation"] = new JsonObject { ["type"] = "boolean", ["description"] = "true = 用军团订单（默认 false = 个人）" },
            ["limit"] = new JsonObject { ["type"] = "integer", ["description"] = "最多行数（默认 30，上限 100）" },
        },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var (characterCtx, error) = await CharacterToolHelper.ResolveAsync(ctx, ToolArgs.ReadString(args, "characterName"), ct);
        if (error is not null)
        {
            return new ToolResult(error);
        }

        var corporation = ToolArgs.ReadBool(args, "corporation", false);
        var limit = Math.Clamp(ToolArgs.ReadInt(args, "limit", 30), 1, 100);

        var orders = corporation
            ? await CharacterOrderService.GetCorpOrdersAsync(characterCtx!, ct)
            : await CharacterOrderService.GetCharacterOrdersAsync(characterCtx!, ct);
        if (orders is null)
        {
            return new ToolResult("订单获取失败（ESI 不可用、令牌失效或限流）。");
        }

        if (orders.Count == 0)
        {
            return new ToolResult("没有未结订单，生不出购买文本。");
        }

        var statuses = await CharacterOrderService.BuildStatusAsync(orders, ct);

        // 复用订单页的同一份逻辑，保证与界面复制出来的文本逐字一致
        var text = OrderPageViewModel.BuildGameOrderText(statuses);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new ToolResult($"共 {statuses.Count} 条未结订单，但**没有**被压单的行，无需补货文本。");
        }

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var builder = new StringBuilder();
        builder.AppendLine($"被压单 {lines.Length} 项，批量购买文本（可直接粘贴进游戏）：");
        builder.AppendLine("```");
        foreach (var line in lines.Take(limit))
        {
            builder.AppendLine(line);
        }

        builder.Append("```");
        if (lines.Length > limit)
        {
            builder.AppendLine();
            builder.Append($"（只显示前 {limit} 行，共 {lines.Length} 行）");
        }

        return new ToolResult(builder.ToString(), ParamSummary: $"{lines.Length} 行购买文本");
    }
}
