using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 角色蓝本（**只读**）：原图/拷贝、材料与时间效率、所在位置、剩余流程。
/// <para>
/// 蓝图数量通常远小于资产，因此直接列清单；按 <c>TypeId</c> 聚合时会保留最好的一份研究等级。
/// 产物名走静态库 <c>industryActivityProducts</c>（制造活动），查不到就不显示产物。
/// </para>
/// </summary>
public sealed class CharacterBlueprintsTool : ITool
{
    public string Name => "character_blueprints";

    public string DisplayNameKey => "AssistantTool_character_blueprints";

    public string Description =>
        "查询某个角色的蓝图（原图/拷贝、材料效率 ME、时间效率 TE、剩余流程、所在位置）。" +
        "参数 characterName 用角色全名（省略 = 默认角色）；keyword 按蓝图名或产物名过滤（如「战列舰」「掠夺」）；" +
        "onlyOriginals=true 只看原图、false 只看拷贝、省略则都看；minMe 只看材料效率不低于该值的（0-10）；" +
        "top 控制条数（默认 15，上限 40）。适合「我有哪些蓝图」「我有没有 T2 掠夺图」「原图都研究到几了」。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["characterName"] = new JsonObject { ["type"] = "string", ["description"] = "角色全名（省略 = 默认角色）" },
            ["keyword"] = new JsonObject { ["type"] = "string", ["description"] = "按蓝图名/产物名过滤" },
            ["onlyOriginals"] = new JsonObject { ["type"] = "boolean", ["description"] = "true 只看原图 / false 只看拷贝（省略 = 都看）" },
            ["minMe"] = new JsonObject { ["type"] = "integer", ["description"] = "只看 ME ≥ 该值（0-10）" },
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

        var keyword = ToolArgs.ReadString(args, "keyword")?.Trim();
        var onlyOriginals = ReadOptionalBool(args, "onlyOriginals");
        var minMe = Math.Clamp(ToolArgs.ReadInt(args, "minMe", 0), 0, 10);
        var top = Math.Clamp(ToolArgs.ReadInt(args, "top", 15), 1, 40);

        var blueprints = await CharacterEsiExtraService.GetBlueprintsAsync(context);
        if (blueprints is null)
        {
            return new ToolResult($"读取 {context.Name} 的蓝图失败（令牌失效或 ESI 不可用）。蓝图需要 esi-characters.read_blueprints.v1 权限。");
        }

        if (blueprints.Count == 0)
        {
            return new ToolResult($"{context.Name} 没有蓝图（或该角色的蓝图权限未授权）。");
        }

        var query = blueprints.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(p =>
                (p.TypeName ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (p.ProductName ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (onlyOriginals is { } originals)
        {
            query = query.Where(p => p.IsCopy != originals);
        }

        if (minMe > 0)
        {
            query = query.Where(p => p.MaterialEfficiency >= minMe);
        }

        // 同一蓝图类型可能有多份/多拷贝，按类型聚合展示（保留最好的研究等级），并记录份数
        var grouped = query
            .GroupBy(p => p.TypeId)
            .Select(g => new
            {
                Blueprint = g.OrderByDescending(p => p.MaterialEfficiency).ThenByDescending(p => p.TimeEfficiency).First(),
                Count = g.Count(),
                Copies = g.Count(p => p.IsCopy),
                Locations = g.Select(p => p.LocationName ?? p.LocationId.ToString())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct()
                    .ToList(),
            })
            .OrderByDescending(p => p.Blueprint.MaterialEfficiency)
            .ThenBy(p => p.Blueprint.TypeName, StringComparer.Ordinal)
            .ToList();

        if (grouped.Count == 0)
        {
            return new ToolResult($"{context.Name} 有 {blueprints.Count} 份蓝图，但都不满足当前过滤条件" +
                (string.IsNullOrWhiteSpace(keyword) ? string.Empty : $"（关键词「{keyword}」）") + "。");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"{context.Name} 的蓝图：共 {blueprints.Count} 份 · 匹配 {grouped.Count} 种" +
            (string.IsNullOrWhiteSpace(keyword) ? string.Empty : $"（关键词「{keyword}」）"));

        foreach (var item in grouped.Take(top))
        {
            var bp = item.Blueprint;
            var product = string.IsNullOrWhiteSpace(bp.ProductName) ? string.Empty : $" → {bp.ProductName}";
            var kind = bp.IsCopy ? $"拷贝（剩余 {Math.Max(bp.Runs, 0)} 次）" : "原图";
            var extra = item.Count > 1 ? $" · 共 {item.Count} 份（拷贝 {item.Copies}）" : string.Empty;
            var where = item.Locations.Count > 0 ? $" · {string.Join("、", item.Locations.Take(2))}" : string.Empty;

            builder.AppendLine($"- {bp.TypeName}{product}｜{kind}｜ME {bp.MaterialEfficiency} / TE {bp.TimeEfficiency}{extra}{where}");
        }

        if (grouped.Count > top)
        {
            builder.AppendLine($"（另有 {grouped.Count - top} 种未列出，可用 keyword / top 缩小范围）");
        }

        var originalCount = blueprints.Count(p => !p.IsCopy);
        builder.AppendLine($"（该角色蓝图构成：原图 {originalCount} 份 · 拷贝 {blueprints.Count - originalCount} 份）");

        return new ToolResult(builder.ToString().TrimEnd(),
            ParamSummary: $"{context.Name} · {grouped.Count} 种 / {blueprints.Count} 份");
    }

    /// <summary>读"三态布尔"：没给返回 null（区别于显式 false）。</summary>
    private static bool? ReadOptionalBool(JsonElement args, string name)
    {
        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty(name, out var value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        return null;
    }
}
