using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 角色资产（**只读**）：默认按地点/容器汇总，而不是把上万条原始资产丢给模型。
/// <para>
/// 数据来自 <see cref="CharacterEsiExtraService.GetAssetSummaryAsync"/>（分页取完的完整资产 + 已解析名字的分组）。
/// 位置名走本地解析（空间站/星系查本地库，结构只在本地结构列表里找）——**不为了一个名字额外打 ESI**，
/// 因此解析不到时按 ID 显示；位置本身是别的物品（舰船/货柜）时显示为该物品。
/// </para>
/// </summary>
public sealed class CharacterAssetsTool : ITool
{
    public string Name => "character_assets";

    public string DisplayNameKey => "AssistantTool_character_assets";

    public string Description =>
        "查询某个角色的资产分布（**聚合汇总**，不是逐条物品列表）：按地点/容器分组，每组给出条目数、" +
        "数量合计与该处数量最多的物品；groupBy=item 则按物品类型做全量汇总。" +
        "参数 characterName 用角色全名（省略 = 默认角色）；locationKeyword 只保留位置名含该关键词的分组（如「吉他」）；" +
        "top 控制显示条数（默认 12，上限 30）。适合「我东西都在哪」「我在吉他有多少货」「我总共有多少三钛合金」。" +
        "注意：位置若是结构且不在本地结构列表里，会显示为 ID（应用不为此额外请求 ESI）。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["characterName"] = new JsonObject { ["type"] = "string", ["description"] = "角色全名（省略 = 默认角色）" },
            ["groupBy"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "location", "item" }, ["description"] = "location（按地点，默认）/ item（按物品类型全量汇总）" },
            ["locationKeyword"] = new JsonObject { ["type"] = "string", ["description"] = "只看位置名含该关键词的分组（仅 groupBy=location 时生效）" },
            ["top"] = new JsonObject { ["type"] = "integer", ["description"] = "显示条数（默认 12，上限 30）" },
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

        var groupByItem = string.Equals(ToolArgs.ReadString(args, "groupBy"), "item", StringComparison.OrdinalIgnoreCase);
        var keyword = ToolArgs.ReadString(args, "locationKeyword")?.Trim();
        var top = Math.Clamp(ToolArgs.ReadInt(args, "top", 12), 1, 30);

        var summary = await CharacterEsiExtraService.GetAssetSummaryAsync(context);
        if (summary is null)
        {
            return new ToolResult($"读取 {context.Name} 的资产失败（令牌失效或 ESI 不可用）。若刚改过密码或权限，请在角色页重新授权。");
        }

        if (summary.Assets.Count == 0)
        {
            return new ToolResult($"{context.Name} 没有任何资产（或该角色的资产权限未授权：需要 esi-assets.read_assets.v1）。");
        }

        var builder = new StringBuilder();
        if (groupByItem)
        {
            AppendByItem(builder, context.Name, summary.Assets, top);
            return new ToolResult(builder.ToString().TrimEnd(),
                ParamSummary: $"{context.Name} · {summary.Assets.Count} 条资产 · 按物品");
        }

        AppendByLocation(builder, context.Name, summary, keyword, top);
        return new ToolResult(builder.ToString().TrimEnd(),
            ParamSummary: $"{context.Name} · {summary.Assets.Count} 条资产 / {summary.Groups.Count} 处");
    }

    /// <summary>按地点/容器展示（默认视图）。</summary>
    private static void AppendByLocation(StringBuilder builder, string characterName, CharacterAssetSummary summary, string? keyword, int top)
    {
        var groups = summary.Groups
            .Where(g => string.IsNullOrWhiteSpace(keyword)
                || (g.LocationName ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (groups.Count == 0)
        {
            builder.AppendLine($"{characterName} 共 {summary.Assets.Count} 条资产、分布在 {summary.Groups.Count} 处，" +
                $"但没有位置名含「{keyword}」的分组。");
            return;
        }

        builder.AppendLine($"{characterName} 的资产：{summary.Assets.Count} 条（数量合计 {summary.Assets.Sum(p => p.Quantity):N0}）" +
            (string.IsNullOrWhiteSpace(keyword)
                ? $"，分布在 {summary.Groups.Count} 处"
                : $"，位置名含「{keyword}」的 {groups.Count} 处"));

        foreach (var group in groups.Take(top))
        {
            builder.AppendLine();
            builder.AppendLine($"【{group.LocationName ?? $"位置 {group.LocationId}"}】{group.ItemCount} 条 · 数量合计 {group.TotalQuantity:N0}" +
                (string.IsNullOrWhiteSpace(group.LocationFlag) ? string.Empty : $" · {group.LocationFlag}"));

            foreach (var item in group.TopItems)
            {
                builder.AppendLine($"  - {item.TypeName} × {item.Quantity:N0}");
            }

            if (group.ItemCount > group.TopItems.Count)
            {
                builder.AppendLine($"  （另有 {group.ItemCount - group.TopItems.Count} 条未列出）");
            }
        }

        if (groups.Count > top)
        {
            builder.AppendLine();
            builder.AppendLine($"（还有 {groups.Count - top} 处未列出，可调大 top 或用 locationKeyword 缩小范围）");
        }
    }

    /// <summary>按物品类型做**全量**汇总（"我一共有多少 XX"）。</summary>
    private static void AppendByItem(StringBuilder builder, string characterName, List<CharacterAssetRow> assets, int top)
    {
        var byType = assets
            .GroupBy(p => p.TypeName ?? p.TypeId.ToString(), StringComparer.Ordinal)
            .Select(g => new
            {
                Name = g.Key,
                Quantity = g.Sum(p => p.Quantity),
                Rows = g.Count(),
                Places = g.Select(p => p.LocationId).Distinct().Count(),
                CopyCount = g.Count(p => p.IsBlueprintCopy == true),
            })
            .OrderByDescending(p => p.Quantity)
            .ToList();

        builder.AppendLine($"{characterName} 的资产按物品类型汇总：{byType.Count} 类 · {assets.Count} 条 · 数量合计 {assets.Sum(p => p.Quantity):N0}");

        foreach (var item in byType.Take(top))
        {
            var copy = item.CopyCount > 0 ? $" · 含拷贝 {item.CopyCount}" : string.Empty;
            builder.AppendLine($"- {item.Name}：数量 {item.Quantity:N0} · {item.Rows} 条 · 分布 {item.Places} 处{copy}");
        }

        if (byType.Count > top)
        {
            builder.AppendLine($"（另有 {byType.Count - top} 类未列出）");
        }
    }
}
