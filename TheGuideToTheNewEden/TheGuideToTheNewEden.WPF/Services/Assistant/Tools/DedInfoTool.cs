using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>DED 场景资料：按关键词搜索，或按势力列出该势力的全部场景。</summary>
public sealed class DedInfoTool : ITool
{
    /// <summary>势力名 → DED 库的类型索引（与 DED 页下拉顺序一致）。</summary>
    private static readonly (string[] Aliases, int Index, string Label)[] Factions =
    [
        (["无人机", "drone", "rogue"], 0, "无人机"),
        (["天蛇", "serpentis"], 1, "天蛇"),
        (["血袭者", "blood"], 2, "血袭者"),
        (["萨沙", "sansha"], 3, "萨沙"),
        (["天使", "angel"], 4, "天使"),
        (["古斯塔斯", "guristas"], 5, "古斯塔斯"),
    ];

    public string Name => "ded_info";

    public string DisplayNameKey => "AssistantTool_ded_info";

    public string Description =>
        "查询 DED 死亡空间（复合体）资料：场景等级、标题与要点说明。" +
        "按关键词或势力搜索（无人机/天蛇/血袭者/萨沙/天使/古斯塔斯），还可按等级过滤。" +
        "适合「DED 6/10 怎么打」「天蛇有哪些 DED」。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["keyword"] = new JsonObject { ["type"] = "string", ["description"] = "场景名/端口名关键词" },
            ["faction"] = new JsonObject { ["type"] = "string", ["description"] = "势力名，见工具说明" },
            ["level"] = new JsonObject { ["type"] = "integer", ["description"] = "只看该等级，如 6 即 6/10" },
            ["top"] = new JsonObject { ["type"] = "integer", ["description"] = "条数（默认 15，上限 40）" },
        },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var keyword = ToolArgs.ReadString(args, "keyword")?.Trim();
        var faction = ToolArgs.ReadString(args, "faction")?.Trim();
        var level = ToolArgs.ReadInt(args, "level", 0);
        var top = Math.Clamp(ToolArgs.ReadInt(args, "top", 15), 1, 40);

        List<Core.DBModels.DED>? list;
        string scope;
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            list = await DEDService.SearchAsync(keyword);
            scope = $"关键词「{keyword}」";
        }
        else if (!string.IsNullOrWhiteSpace(faction))
        {
            var index = ResolveFaction(faction);
            if (index < 0)
            {
                var supported = string.Join("、", Factions.Select(f => f.Label));
                return new ToolResult($"不认识的势力「{faction}」。支持：{supported}。");
            }

            list = await DEDService.QueryAllAsync(index);
            scope = $"{Factions.First(f => f.Index == index).Label} 势力";
        }
        else
        {
            return new ToolResult("请给 keyword（场景关键词）或 faction（势力名）之一。");
        }

        if (list is null || list.Count == 0)
        {
            return new ToolResult($"没有找到 {scope} 的 DED 资料（本地 DED 库为空或未命中）。");
        }

        var filtered = level > 0 ? list.Where(d => d.Level == level).ToList() : list;
        if (filtered.Count == 0)
        {
            return new ToolResult($"{scope} 下没有等级 {level} 的场景（该范围共 {list.Count} 条）。");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"{scope} 命中 {filtered.Count} 条，显示前 {Math.Min(top, filtered.Count)} 条：");
        foreach (var ded in filtered.Take(top))
        {
            var content = ToolArgs.Truncate((ded.Content ?? string.Empty).Replace("\r", " ").Replace("\n", " "), 180);
            builder.AppendLine($"- 【{ded.Level}/10】{ded.TitleCN}（{ded.TitleEN}，Id {ded.Id}）");
            if (!string.IsNullOrWhiteSpace(content))
            {
                builder.AppendLine($"  {content}");
            }
        }

        return new ToolResult(builder.ToString().TrimEnd(), ParamSummary: $"{scope} {filtered.Count} 条");
    }

    private static int ResolveFaction(string faction)
    {
        var text = faction.Trim().ToLowerInvariant();
        foreach (var (aliases, index, _) in Factions)
        {
            if (aliases.Any(alias => text.Contains(alias, StringComparison.OrdinalIgnoreCase)))
            {
                return index;
            }
        }

        return -1;
    }
}
