using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>角色概览：出生/SP/未分配/LP/安等/钱包/军团联盟/技能队列。</summary>
public sealed class CharacterOverviewTool : ITool
{
    public string Name => "character_overview";

    public string DisplayNameKey => "AssistantTool_character_overview";

    public string Description =>
        "查询某个角色的概览：出生日期、技能点（总/未分配）、忠诚点、安全等级、个人/公司钱包、军团（联盟）与技能队列状态。" +
        "参数 characterName 用角色全名；省略用默认角色。适合「我的资产」「X 角色什么情况」。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["characterName"] = new JsonObject { ["type"] = "string", ["description"] = "角色全名（省略 = 默认角色）" },
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

        var overview = await CharacterOverviewService.GetAsync(characterCtx!, false);
        var skills = await CharacterSkillService.GetAsync(characterCtx!, false);
        if (overview is null)
        {
            return new ToolResult("角色概览获取失败（ESI 不可用或限流）。");
        }

        var builder = new StringBuilder();
        builder.Append(overview.CharacterName);
        if (overview.Birthday is { } birthday)
        {
            builder.Append($" · 出生 {birthday:yyyy.MM.dd}");
        }

        builder.AppendLine();
        builder.AppendLine($"SP {skills?.TotalSkillPoints.ToString("N0") ?? "—"}（未分配 {skills?.UnallocatedSkillPoints.ToString("N0") ?? "—"}）· LP {overview.LoyaltyPoints:N0}");
        builder.AppendLine($"安等 {overview.SecurityStatus:F1} · 个人钱包 {ToolArgs.FormatIsk(overview.WalletBalance)}" +
            (overview.CorporationWalletBalance is { } corp ? $" · 公司钱包 {ToolArgs.FormatIsk(corp)}" : string.Empty));
        builder.AppendLine($"军团 {overview.CorporationName}" + (string.IsNullOrWhiteSpace(overview.AllianceName) ? string.Empty : $"（{overview.AllianceName}）"));
        if (skills?.Queue is { Count: > 0 } queue)
        {
            builder.AppendLine($"技能队列 {queue.Count} 条");
        }

        return new ToolResult(builder.ToString().TrimEnd(), ParamSummary: overview.CharacterName);
    }
}
