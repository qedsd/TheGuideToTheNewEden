using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 角色联系人（**只读**）：联系人列表、标签、是否监视/拉黑、声望。
/// <para>
/// 与 <c>character_standings</c> 同属"个人关系数据"，同样只在用户明确询问时查询。
/// 标签名走 ESI 的联系人标签端点（失败退化为「标签 N」）；联系人名走本地 IdName 库。
/// </para>
/// </summary>
public sealed class CharacterContactsTool : ITool
{
    public string Name => "character_contacts";

    public string DisplayNameKey => "AssistantTool_character_contacts";

    public string Description =>
        "查询某个角色的联系人列表（含标签、是否监视中、是否已拉黑、声望值）。" +
        "参数 characterName 用角色全名（省略 = 默认角色）；filter 选 all（默认）/ watched（只看监视中）/ blocked（只看拉黑）；" +
        "keyword 按联系人名或标签名过滤；top 控制条数（默认 15，上限 40）。" +
        "适合「我在监视谁」「黑名单里有哪些人」。注意：这是用户的私人关系数据，" +
        "只在用户明确问到时查询，不要主动罗列他人的黑名单或监视对象。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["characterName"] = new JsonObject { ["type"] = "string", ["description"] = "角色全名（省略 = 默认角色）" },
            ["filter"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "all", "watched", "blocked" }, ["description"] = "all（默认）/ watched / blocked" },
            ["keyword"] = new JsonObject { ["type"] = "string", ["description"] = "按联系人名或标签名过滤" },
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

        var filter = (ToolArgs.ReadString(args, "filter") ?? "all").Trim().ToLowerInvariant();
        var keyword = ToolArgs.ReadString(args, "keyword")?.Trim();
        var top = Math.Clamp(ToolArgs.ReadInt(args, "top", 15), 1, 40);

        var contacts = await CharacterEsiExtraService.GetContactsAsync(context);
        if (contacts is null)
        {
            return new ToolResult($"读取 {context.Name} 的联系人失败（令牌失效或 ESI 不可用）。需要 esi-characters.read_contacts.v1 权限。");
        }

        if (contacts.Count == 0)
        {
            return new ToolResult($"{context.Name} 没有联系人（或该角色的联系人权限未授权）。");
        }

        var query = contacts.AsEnumerable();
        query = filter switch
        {
            "watched" => query.Where(p => p.IsWatched),
            "blocked" => query.Where(p => p.IsBlocked),
            _ => query,
        };

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(p =>
                (p.ContactName ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || p.Labels.Any(l => l.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
        }

        var visible = query.OrderByDescending(p => p.Standing).ToList();
        if (visible.Count == 0)
        {
            return new ToolResult($"{context.Name} 有 {contacts.Count} 个联系人，但没有满足当前过滤条件的" +
                (string.IsNullOrWhiteSpace(keyword) ? "。" : $"（关键词「{keyword}」）。"));
        }

        var builder = new StringBuilder();
        builder.AppendLine($"{context.Name} 的联系人：共 {contacts.Count} 个（监视中 {contacts.Count(p => p.IsWatched)} · " +
            $"已拉黑 {contacts.Count(p => p.IsBlocked)}），显示 {Math.Min(top, visible.Count)} 个" +
            (filter == "all" ? "（按声望降序）" : $"（filter={filter}）"));

        foreach (var row in visible.Take(top))
        {
            var typeText = row.ContactType switch
            {
                "character" => "角色",
                "corporation" => "军团",
                "alliance" => "联盟",
                "faction" => "势力",
                _ => row.ContactType ?? "实体",
            };
            var marks = new List<string>(2);
            if (row.IsWatched)
            {
                marks.Add("监视中");
            }

            if (row.IsBlocked)
            {
                marks.Add("已拉黑");
            }

            var labels = row.Labels.Count > 0 ? $" · 标签 {string.Join("/", row.Labels)}" : string.Empty;
            var markText = marks.Count > 0 ? $" · {string.Join(" · ", marks)}" : string.Empty;

            builder.AppendLine($"- {row.ContactName}（{typeText}）：{row.Standing:+0.0;-0.0;0.0}{labels}{markText}");
        }

        if (visible.Count > top)
        {
            builder.AppendLine($"（另有 {visible.Count - top} 个未列出）");
        }

        return new ToolResult(builder.ToString().TrimEnd(),
            ParamSummary: $"{context.Name} · {visible.Count} 个联系人");
    }
}
