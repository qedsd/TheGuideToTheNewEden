using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>角色技能：总 SP/未分配 + 队列 + 按组/关键字过滤。</summary>
public sealed class CharacterSkillsTool : ITool
{
    public string Name => "character_skills";

    public string DisplayNameKey => "AssistantTool_character_skills";

    public string Description =>
        "查询某个角色的技能：技能点（总/未分配）、训练队列，以及按技能组或技能名关键字过滤的明细。" +
        "角色全名（省略 = 默认角色）；可按技能组或技能名关键字过滤。适合「我技能点多少」「学没学 XX」。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["characterName"] = new JsonObject { ["type"] = "string", ["description"] = "角色全名（省略 = 默认角色）" },
            ["query"] = new JsonObject { ["type"] = "string", ["description"] = "技能组名或技能名关键字（可选）" },
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

        var data = await CharacterSkillService.GetAsync(characterCtx!, false);
        if (data is null)
        {
            return new ToolResult("技能数据获取失败（ESI 不可用或限流）。");
        }

        var query = (ToolArgs.ReadString(args, "query") ?? string.Empty).Trim();
        var builder = new StringBuilder();
        builder.AppendLine($"总 SP {data.TotalSkillPoints:N0} · 未分配 {data.UnallocatedSkillPoints:N0}");
        if (data.Queue is { Count: > 0 } queue)
        {
            builder.AppendLine($"训练队列 {queue.Count} 条");
        }

        var matchedGroups = string.IsNullOrEmpty(query)
            ? data.Groups
            : data.Groups.Where(g =>
                g.GroupName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || g.Skills.Any(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();
        foreach (var group in matchedGroups.Take(5))
        {
            builder.AppendLine($"· {group.GroupName}: {group.Skills.Count} 项技能");
            foreach (var skill in group.Skills.Where(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(10))
            {
                builder.AppendLine($"  - {skill.Name} Lv{skill.Level}（{skill.SkillPoints:N0} SP）");
            }
        }

        if (string.IsNullOrEmpty(query) && matchedGroups.Count == 0)
        {
            builder.AppendLine("（无技能组数据）");
        }

        return new ToolResult(builder.ToString().TrimEnd(), ParamSummary: query.Length == 0 ? "全部技能" : query);
    }
}
