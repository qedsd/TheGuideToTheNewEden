using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>角色勋章（**只读**）：军团颁发给该角色的勋章与获奖原因。</summary>
public sealed class CharacterMedalsTool : ITool
{
    public string Name => "character_medals";

    public string DisplayNameKey => "AssistantTool_character_medals";

    public string Description =>
        "查询某个角色获得的勋章：名称、颁发军团/颁发人、时间、状态与获奖原因。" +
        "参数 characterName 用角色全名（省略 = 默认角色）；keyword 按勋章名或原因过滤；" +
        "top 控制条数（默认 20，上限 50，按时间倒序）。适合「我拿过什么勋章」「这枚勋章谁发的」。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["characterName"] = new JsonObject { ["type"] = "string", ["description"] = "角色全名（省略 = 默认角色）" },
            ["keyword"] = new JsonObject { ["type"] = "string", ["description"] = "按勋章名/原因过滤" },
            ["top"] = new JsonObject { ["type"] = "integer", ["description"] = "条数（默认 20，上限 50）" },
        },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var (context, error) = await CharacterToolHelper.ResolveAsync(ctx, ToolArgs.ReadString(args, "characterName"), ct);
        if (context is null)
        {
            return new ToolResult(error!);
        }

        var keyword = ToolArgs.ReadString(args, "keyword")?.Trim();
        var top = Math.Clamp(ToolArgs.ReadInt(args, "top", 20), 1, 50);

        var medals = await CharacterEsiExtraService.GetMedalsAsync(context);
        if (medals is null)
        {
            return new ToolResult($"读取 {context.Name} 的勋章失败（令牌失效或 ESI 不可用）。需要 esi-characters.read_medals.v1 权限。");
        }

        if (medals.Count == 0)
        {
            return new ToolResult($"{context.Name} 没有勋章（或该角色的勋章权限未授权）。");
        }

        var visible = medals
            .Where(p => string.IsNullOrWhiteSpace(keyword)
                || (p.Title ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (p.Reason ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.Date)
            .ToList();

        if (visible.Count == 0)
        {
            return new ToolResult($"{context.Name} 有 {medals.Count} 枚勋章，但没有名称或原因含「{keyword}」的。");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"{context.Name} 的勋章：共 {medals.Count} 枚，显示 {Math.Min(top, visible.Count)} 枚（按时间倒序）");

        foreach (var row in visible.Take(top))
        {
            var issuer = string.IsNullOrWhiteSpace(row.IssuerName) ? string.Empty : $" · 颁发人 {row.IssuerName}";
            var reason = string.IsNullOrWhiteSpace(row.Reason) ? string.Empty : $"｜原因：{ToolArgs.Truncate(row.Reason!, 80)}";
            builder.AppendLine($"- {row.Date:yyyy-MM-dd}｜{row.Title}（{row.CorporationName}）{issuer}{reason}");
        }

        if (visible.Count > top)
        {
            builder.AppendLine($"（另有 {visible.Count - top} 枚未列出）");
        }

        return new ToolResult(builder.ToString().TrimEnd(),
            ParamSummary: $"{context.Name} · {visible.Count} 枚勋章");
    }
}
