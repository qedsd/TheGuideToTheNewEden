using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 角色声望（**只读**）：NPC 军团 / 势力 / 代理人对该角色的声望值。
/// <para>
/// 这是"你的个人态度/关系数据"，只在用户明确询问时才有意义；工具描述里注明了这一点，不做主动翻查。
/// 实体名走本地 IdName 库（查不到按 ID 显示，不额外打 ESI）。
/// </para>
/// </summary>
public sealed class CharacterStandingsTool : ITool
{
    public string Name => "character_standings";

    public string DisplayNameKey => "AssistantTool_character_standings";

    public string Description =>
        "查询某个角色对各 NPC 军团/势力的声望值（-10 到 10，含代理人）。" +
        "参数 characterName 用角色全名（省略 = 默认角色）；type 过滤 npc_corp（军团）/ faction（势力）/ agent（代理人）；" +
        "keyword 按名称过滤；top 控制条数（默认 15，上限 40）。适合「我对天使的声望多少」「哪些势力跟我敌对」。" +
        "注意：这是用户的私人关系数据，只在用户明确问到时查询，不要主动罗列。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["characterName"] = new JsonObject { ["type"] = "string", ["description"] = "角色全名（省略 = 默认角色）" },
            ["type"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "npc_corp", "faction", "agent" }, ["description"] = "只看某类实体" },
            ["keyword"] = new JsonObject { ["type"] = "string", ["description"] = "按实体名过滤" },
            ["top"] = new JsonObject { ["type"] = "integer", ["description"] = "条数（默认 15，上限 40）" },
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

        var type = ToolArgs.ReadString(args, "type")?.Trim();
        var keyword = ToolArgs.ReadString(args, "keyword")?.Trim();
        var top = Math.Clamp(ToolArgs.ReadInt(args, "top", 15), 1, 40);

        var standings = await CharacterEsiExtraService.GetStandingsAsync(context);
        if (standings is null)
        {
            return new ToolResult($"读取 {context.Name} 的声望失败（令牌失效或 ESI 不可用）。需要 esi-characters.read_standings.v1 权限。");
        }

        if (standings.Count == 0)
        {
            return new ToolResult($"{context.Name} 没有声望记录（或该角色的声望权限未授权）。");
        }

        var query = standings.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(p => string.Equals(p.FromType, type, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(p => (p.FromName ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        var visible = query.OrderByDescending(p => p.Standing).ToList();
        if (visible.Count == 0)
        {
            return new ToolResult($"{context.Name} 有 {standings.Count} 条声望，但没有满足当前过滤条件的" +
                (string.IsNullOrWhiteSpace(keyword) ? "。" : $"（关键词「{keyword}」）。"));
        }

        var builder = new StringBuilder();
        var friendly = standings.Count(p => p.Standing > 0);
        var hostile = standings.Count(p => p.Standing < 0);
        builder.AppendLine($"{context.Name} 的声望：共 {standings.Count} 条（正 {friendly} · 负 {hostile}），显示 {Math.Min(top, visible.Count)} 条（按声望降序）");

        foreach (var row in visible.Take(top))
        {
            var typeText = row.FromType switch
            {
                "npc_corp" => "军团",
                "faction" => "势力",
                "agent" => "代理人",
                _ => row.FromType ?? "实体",
            };
            builder.AppendLine($"- {row.FromName}（{typeText}）：{row.Standing:+0.0;-0.0;0.0}");
        }

        if (visible.Count > top)
        {
            builder.AppendLine($"（另有 {visible.Count - top} 条未列出）");
        }

        return new ToolResult(builder.ToString().TrimEnd(),
            ParamSummary: $"{context.Name} · {visible.Count} 条声望");
    }
}
