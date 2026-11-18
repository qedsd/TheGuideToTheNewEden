using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 任务资料：在本地任务库（<c>Resources/Database/static.db</c> 的 <c>Mission</c> 表）里
/// 按关键词 / 分类 / 等级 / 势力检索，返回任务的结构化信息与攻略正文（Markdown）。
/// <para>
/// 与「任务」页同一数据源与同一查询方法（<see cref="MissionService"/>），因此正文里的排版、
/// NPC 表、要点列表与页面上看到的完全一致（界面用 <c>MarkdownPresenter</c> 渲染，模型直接读原文）。
/// 正文可能很长，按 <c>contentChars</c> 截断并**明确告知**被截断。
/// </para>
/// </summary>
public sealed class MissionInfoTool : ITool
{
    /// <summary>正文默认截断长度（约 800–1000 token，够看清结构与要点）。</summary>
    private const int DefaultContentChars = 1500;

    /// <summary>分类中英文别名 → <see cref="Core.Enums.MissionCategory"/> 值。</summary>
    private static readonly (string[] Aliases, int Value, string Name)[] Categories =
    [
        (["安全", "战斗", "security", "encounter"], 0, "安全任务"),
        (["运输", "快递", "distribution", "courier"], 1, "运输任务"),
        (["采矿", "mining"], 2, "采矿任务"),
        (["故事线", "storyline"], 3, "故事线任务"),
        (["史诗", "epic"], 4, "史诗任务"),
        (["异常", "燃烧", "anomic", "burner"], 5, "异常任务"),
        (["cosmos"], 6, "COSMOS 任务"),
        (["研发", "r&d", "rnd", "research"], 7, "研发任务"),
        (["职业", "career", "tutorial"], 8, "职业代理人任务"),
        (["circle"], 9, "Circle 任务"),
    ];

    public string Name => "mission_info";

    public string DisplayNameKey => "AssistantTool_mission_info";

    public string Description =>
        "查本地 EVE 任务资料：任务的结构化信息（分类/等级/势力/目标/奖励/电子战/船型限制）与攻略正文（Markdown）。"
        + "可按任务名关键词搜索，也可按分类、等级、势力筛选。只读。"
        + "唯一命中直接返回攻略正文，超长会截断并注明。适合「这个任务怎么做」「4 级任务有哪些」「燃烧者任务怎么打」。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["keyword"] = new JsonObject { ["type"] = "string", ["description"] = "任务名关键词，中英文均可" },
            ["category"] = new JsonObject { ["type"] = "string", ["description"] = "分类：安全/运输/采矿/故事线/史诗/异常/COSMOS/研发/职业/Circle" },
            ["level"] = new JsonObject { ["type"] = "integer", ["description"] = "任务等级 1–5" },
            ["faction"] = new JsonObject { ["type"] = "integer", ["description"] = "势力：0 无人机 1 天蛇 2 血袭者 3 萨沙 4 天使 5 古斯塔斯" },
            ["top"] = new JsonObject { ["type"] = "integer", ["description"] = "返回条数，默认 10，上限 30" },
            ["contentChars"] = new JsonObject { ["type"] = "integer", ["description"] = "正文长度，默认 1500，上限 6000" },
        },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var keyword = ToolArgs.ReadString(args, "keyword")?.Trim();
        var categoryText = ToolArgs.ReadString(args, "category")?.Trim();
        var level = ToolArgs.ReadInt(args, "level", 0);
        var faction = ToolArgs.ReadInt(args, "faction", -1);
        var top = Math.Clamp(ToolArgs.ReadInt(args, "top", 10), 1, 30);
        var contentChars = Math.Clamp(ToolArgs.ReadInt(args, "contentChars", DefaultContentChars), 200, 6000);

        var category = MissionService.AnyCategory;
        var categoryName = string.Empty;
        if (!string.IsNullOrWhiteSpace(categoryText))
        {
            var hit = Categories.FirstOrDefault(c =>
                c.Aliases.Any(a => categoryText.Contains(a, StringComparison.OrdinalIgnoreCase)));
            if (hit.Aliases is null)
            {
                return new ToolResult(
                    $"无法识别分类「{categoryText}」。可用：安全 / 运输 / 采矿 / 故事线 / 史诗 / 异常 / COSMOS / 研发 / 职业 / Circle。");
            }

            category = hit.Value;
            categoryName = hit.Name;
        }

        if (string.IsNullOrWhiteSpace(keyword) && category == MissionService.AnyCategory
            && level is <= 0 or > 5 && faction < 0)
        {
            return new ToolResult("请给 keyword（任务名）、category（分类）、level（1–5）或 faction 之一。");
        }

        ct.ThrowIfCancellationRequested();

        var missions = await MissionService.QueryAsync(
            category,
            level is > 0 and <= 5 ? level : MissionService.AnyLevel,
            faction is >= 0 and <= 5 ? faction : MissionService.AnyFaction,
            keyword);

        if (missions is not { Count: > 0 })
        {
            var scope = !string.IsNullOrWhiteSpace(keyword) ? $"关键词「{keyword}」" : "这些条件";
            return new ToolResult($"任务库里没有匹配 {scope} 的条目。");
        }

        var builder = new StringBuilder();

        // 唯一命中：直接把正文给模型（省一轮往返）
        if (missions.Count == 1)
        {
            var mission = missions[0];
            builder.AppendLine(Headline(mission));
            AppendContent(builder, mission.WikiContent, contentChars);
            return new ToolResult(builder.ToString().TrimEnd(),
                ParamSummary: $"{mission.TitleCN}（L{mission.Level}）");
        }

        var scopeText = string.Join(" · ", new[]
        {
            !string.IsNullOrWhiteSpace(keyword) ? $"关键词「{keyword}」" : null,
            string.IsNullOrEmpty(categoryName) ? null : categoryName,
            level is > 0 and <= 5 ? $"L{level}" : null,
        }.Where(s => s is not null));

        builder.AppendLine($"{scopeText}命中 {missions.Count} 个任务，显示前 {Math.Min(top, missions.Count)} 个：");
        foreach (var mission in missions.Take(top))
        {
            builder.AppendLine("- " + Headline(mission));
        }

        if (missions.Count > top)
        {
            builder.AppendLine($"（其余 {missions.Count - top} 个未列出，可加更具体的关键词缩小范围）");
        }

        builder.AppendLine();
        builder.AppendLine("需要正文时用更精确的任务名再查一次（唯一命中会直接返回攻略正文）。");

        return new ToolResult(builder.ToString().TrimEnd(),
            ParamSummary: $"{scopeText} · {missions.Count} 个任务");
    }

    private static string Headline(Core.DBModels.Static.Mission m)
    {
        var parts = new List<string> { $"L{m.Level}" };
        if (!string.IsNullOrWhiteSpace(m.MissionType))
        {
            parts.Add(m.MissionType);
        }

        if (!string.IsNullOrWhiteSpace(m.Objective))
        {
            parts.Add("目标：" + m.Objective);
        }

        return $"【{m.TitleCN}】（{m.TitleEN}，{string.Join(" · ", parts)}，Id {m.Id}）";
    }

    /// <summary>取正文并按上限截断（内容已在库里渲染成中文 Markdown）。</summary>
    private static void AppendContent(StringBuilder builder, string? content, int contentChars)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            builder.AppendLine("（这个任务在本地库里没有攻略正文）");
            return;
        }

        var text = content.Replace("\r\n", "\n").Trim();
        if (text.Length <= contentChars)
        {
            builder.AppendLine();
            builder.AppendLine(text);
            return;
        }

        builder.AppendLine();
        builder.AppendLine(text[..contentChars]);
        builder.AppendLine();
        builder.Append($"（正文已截断：共 {text.Length} 字符，这里显示前 {contentChars} 字符。需要后面部分请说明，或到「任务」页看全文。）");
    }
}
