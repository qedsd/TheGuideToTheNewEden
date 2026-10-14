using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>角色钱包：余额 + 近期流水摘要。</summary>
public sealed class CharacterWalletTool : ITool
{
    public string Name => "character_wallet";

    public string DisplayNameKey => "AssistantTool_character_wallet";

    public string Description =>
        "查询某个角色的钱包余额与近期流水（时间/描述/类型/金额正负）。参数 characterName 用角色全名；省略用默认角色。" +
        "top 控制流水条数（默认 10，上限 30）。适合「我钱包里有多少钱」「最近收支」。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["characterName"] = new JsonObject { ["type"] = "string", ["description"] = "角色全名（省略 = 默认角色）" },
            ["top"] = new JsonObject { ["type"] = "integer", ["description"] = "流水条数（默认 10，上限 30）" },
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

        var top = Math.Clamp(ToolArgs.ReadInt(args, "top", 10), 1, 30);
        var balance = await CharacterWalletService.GetBalanceAsync(characterCtx!, false);
        var journal = await CharacterWalletService.GetCharacterJournalAsync(characterCtx!, 1, false);
        if (journal is null)
        {
            return new ToolResult("钱包流水获取失败（ESI 不可用或限流）。");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"余额 {ToolArgs.FormatIsk(balance)}");
        foreach (var row in journal.Items.Take(top))
        {
            var sign = row.Amount >= 0 ? "+" : string.Empty;
            builder.AppendLine($"{row.Date:MM-dd HH:mm} {row.RefType ?? "—"} {sign}{ToolArgs.FormatIsk(row.Amount)}");
        }

        return new ToolResult(builder.ToString().TrimEnd(), ParamSummary: $"余额 {ToolArgs.FormatIsk(balance)}");
    }
}
