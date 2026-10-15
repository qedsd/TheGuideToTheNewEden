using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>角色日历（**只读**）：近期军团/NPC 活动与自己的回复状态。</summary>
public sealed class CharacterCalendarTool : ITool
{
    public string Name => "character_calendar";

    public string DisplayNameKey => "AssistantTool_character_calendar";

    public string Description =>
        "查询某个角色的日历事件（军团行动、NPC 活动等）：标题、时间、重要性、自己的回复状态。" +
        "参数 characterName 用角色全名（省略 = 默认角色）；daysAhead 往后看多少天（默认 30，上限 365）；" +
        "includePast=true 时把已过期的也算进来（默认只给未来的）；response 可按状态过滤（accepted/declined/tentative/not_responded）；" +
        "top 控制条数（默认 20，上限 50）。适合「最近有什么活动」「我还没回复哪些日程」。" +
        "注意：本工具只给事件摘要（标题/时间/状态），事件的详细描述需要额外的日历详情接口。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["characterName"] = new JsonObject { ["type"] = "string", ["description"] = "角色全名（省略 = 默认角色）" },
            ["daysAhead"] = new JsonObject { ["type"] = "integer", ["description"] = "往后看多少天（默认 30，上限 365）" },
            ["includePast"] = new JsonObject { ["type"] = "boolean", ["description"] = "true = 连已过期的也列出来" },
            ["response"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "accepted", "declined", "tentative", "not_responded" }, ["description"] = "只看某种回复状态" },
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

        var daysAhead = Math.Clamp(ToolArgs.ReadInt(args, "daysAhead", 30), 1, 365);
        var includePast = ToolArgs.ReadBool(args, "includePast", false);
        var response = ToolArgs.ReadString(args, "response")?.Trim();
        var top = Math.Clamp(ToolArgs.ReadInt(args, "top", 20), 1, 50);

        var events = await CharacterEsiExtraService.GetCalendarAsync(context);
        if (events is null)
        {
            return new ToolResult($"读取 {context.Name} 的日历失败（令牌失效或 ESI 不可用）。需要 esi-calendar.read_calendar_events.v1 权限。");
        }

        if (events.Count == 0)
        {
            return new ToolResult($"{context.Name} 没有日历事件（或该角色的日历权限未授权）。");
        }

        var now = DateTime.UtcNow;
        var until = now.AddDays(daysAhead);

        var query = events.AsEnumerable();
        if (!includePast)
        {
            query = query.Where(p => p.EventDate >= now.AddHours(-1));
        }

        query = query.Where(p => p.EventDate <= until);

        if (!string.IsNullOrWhiteSpace(response))
        {
            query = query.Where(p => string.Equals(p.Response, response, StringComparison.OrdinalIgnoreCase));
        }

        var visible = query.OrderBy(p => p.EventDate).ToList();
        if (visible.Count == 0)
        {
            return new ToolResult($"{context.Name} 有 {events.Count} 个日历事件，但{daysAhead} 天内没有" +
                (string.IsNullOrWhiteSpace(response) ? "符合条件的事件" : $"回复状态为「{response}」的事件") +
                $"（可用 includePast=true 看更早的）。");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"{context.Name} 的日历：共 {events.Count} 个事件，{daysAhead} 天内 {visible.Count} 个" +
            (string.IsNullOrWhiteSpace(response) ? string.Empty : $"（状态 {response}）"));

        foreach (var row in visible.Take(top))
        {
            var state = row.Response switch
            {
                "accepted" => "已接受",
                "declined" => "已拒绝",
                "tentative" => "待定",
                "not_responded" => "未回复",
                _ => row.Response ?? "未知",
            };
            var importance = row.Importance >= 1 ? $" · 重要度 {row.Importance}" : string.Empty;
            var local = DateTime.SpecifyKind(row.EventDate, DateTimeKind.Utc).ToLocalTime();

            builder.AppendLine($"- {local:yyyy-MM-dd HH:mm}｜{row.Title}｜{state}{importance}");
        }

        if (visible.Count > top)
        {
            builder.AppendLine($"（另有 {visible.Count - top} 个未列出）");
        }

        return new ToolResult(builder.ToString().TrimEnd(),
            ParamSummary: $"{context.Name} · {visible.Count} 个事件");
    }
}
