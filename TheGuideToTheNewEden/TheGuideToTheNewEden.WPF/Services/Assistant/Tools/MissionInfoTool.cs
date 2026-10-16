using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 任务资料：在本地任务库（<c>Resources/Database/static.db</c> 的 <c>Mission</c> / <c>MissionContent</c>）里
/// 按关键词或等级检索，返回任务标题与攻略正文（Markdown 原文）。
/// <para>
/// 与「任务」页同一数据源与同一查询方法（<see cref="MissionService"/>），因此正文里的排版、
/// 掉落表、要点列表与页面上看到的完全一致（界面用 <c>MarkdownPresenter</c> 渲染，模型直接读原文）。
/// 正文可能很长，按 <c>contentChars</c> 截断并**明确告知**被截断——不要指望一次读完超长攻略，
/// 需要更多就让模型指定更小的范围或让用户去页面看。
/// </para>
/// </summary>
public sealed class MissionInfoTool : ITool
{
    /// <summary>正文默认截断长度（约 800–1000 token，够看清结构与要点）。</summary>
    private const int DefaultContentChars = 1500;

    public string Name => "mission_info";

    public string DisplayNameKey => "AssistantTool_mission_info";

    public string Description =>
        "查本地 EVE 任务资料：任务标题与攻略正文（要点、掉落、流程，Markdown）。" +
        "可按任务名搜索，或按等级列出任务清单。只读。" +
        "唯一命中直接返回攻略正文，超长会截断并注明。适合「这个任务怎么做」「4 级任务有哪些」。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["keyword"] = new JsonObject { ["type"] = "string", ["description"] = "任务名关键词，中英文均可" },
            ["level"] = new JsonObject { ["type"] = "integer", ["description"] = "任务等级 1–5，按等级列出" },
            ["top"] = new JsonObject { ["type"] = "integer", ["description"] = "返回条数，默认 10，上限 30" },
            ["contentChars"] = new JsonObject { ["type"] = "integer", ["description"] = "正文长度，默认 1500，上限 6000" },
        },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var keyword = ToolArgs.ReadString(args, "keyword")?.Trim();
        var level = ToolArgs.ReadInt(args, "level", 0);
        var top = Math.Clamp(ToolArgs.ReadInt(args, "top", 10), 1, 30);
        var contentChars = Math.Clamp(ToolArgs.ReadInt(args, "contentChars", DefaultContentChars), 200, 6000);

        if (string.IsNullOrWhiteSpace(keyword) && level is <= 0 or > 5)
        {
            return new ToolResult("请给 keyword（任务名关键词）或 level（1–5 的任务等级）之一。");
        }

        ct.ThrowIfCancellationRequested();

        var missions = !string.IsNullOrWhiteSpace(keyword)
            ? await MissionService.QueryMissionAsync(keyword)
            : await MissionService.QueryMissionAsync(level);

        if (missions is not { Count: > 0 })
        {
            var scope = !string.IsNullOrWhiteSpace(keyword) ? $"关键词「{keyword}」" : $"{level} 级任务";
            return new ToolResult($"任务库里没有匹配 {scope} 的条目（本地任务库为空或未命中）。");
        }

        var builder = new StringBuilder();

        // 唯一命中：直接把正文给模型（省一轮往返）
        if (missions.Count == 1)
        {
            var mission = missions[0];
            builder.AppendLine($"【{mission.Title_Zh}】（{mission.Title_En}，{mission.Level} 级，Id {mission.Id}）");
            AppendContent(builder, mission.Id, contentChars);
            return new ToolResult(builder.ToString().TrimEnd(),
                ParamSummary: $"{mission.Title_Zh}（{mission.Level} 级）");
        }

        var scopeText = !string.IsNullOrWhiteSpace(keyword) ? $"关键词「{keyword}」" : $"{level} 级";
        builder.AppendLine($"{scopeText}命中 {missions.Count} 个任务，显示前 {Math.Min(top, missions.Count)} 个：");
        foreach (var mission in missions.Take(top))
        {
            builder.AppendLine($"- 【{mission.Level} 级】{mission.Title_Zh}（{mission.Title_En}，Id {mission.Id}）");
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

    /// <summary>
    /// 取正文并按上限截断。<c>MissionService.QueryMissionContent</c> 是**同步**单行查询（SqlSugar），
    /// 放到线程池上跑，避免在 UI 线程上碰 SQLite（§9 第 37 条）。
    /// </summary>
    private static void AppendContent(StringBuilder builder, int missionId, int contentChars)
    {
        string? content;
        try
        {
            var row = Task.Run(() => MissionService.QueryMissionContent(missionId)).GetAwaiter().GetResult();
            content = row?.Content;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            builder.AppendLine("（读取任务正文失败，请到「任务」页查看）");
            return;
        }

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
