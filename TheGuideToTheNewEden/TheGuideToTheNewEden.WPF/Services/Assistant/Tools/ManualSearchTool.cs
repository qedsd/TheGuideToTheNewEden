using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 内置说明书检索：按关键词查 <see cref="Services.Manual.ManualService"/>，返回命中的手册、节与片段。
/// <para>
/// 与 <see cref="ManualReadTool"/> 成对（先检索定位、再读正文）；两者都是 Core 常驻工具——
/// 说明书正文只进工具结果、不进系统提示词（token 契约），所以任何套餐路由下都要能查手册。
/// </para>
/// </summary>
public sealed class ManualSearchTool : ITool
{
    public string Name => "manual_search";

    public string DisplayNameKey => "AssistantTool_manual_search";

    public string Description =>
        "按关键词检索内置说明书，返回命中的手册、章节与片段。用户问某功能在哪/怎么用/某术语什么意思时先用它定位，再用 manual_read 读正文。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["required"] = new JsonArray("query"),
        ["properties"] = new JsonObject
        {
            ["query"] = new JsonObject { ["type"] = "string", ["description"] = "关键词（可多个，空格分隔）" },
            ["max"] = new JsonObject { ["type"] = "integer", ["description"] = "返回条数（默认 5，上限 8）" },
        },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var query = ToolArgs.ReadString(args, "query")?.Trim() ?? string.Empty;
        if (query.Length == 0)
        {
            return Task.FromResult(new ToolResult("缺少 query：请给出要检索的关键词（功能名、界面文字或术语）。"));
        }

        if (!Services.Manual.ManualService.IsAvailable())
        {
            return Task.FromResult(new ToolResult("说明书目录不存在（输出目录缺少 Resources\\Manuals），无法检索。"));
        }

        var max = Math.Clamp(ToolArgs.ReadInt(args, "max", 5), 1, 8);
        var hits = Services.Manual.ManualService.Search(query, max);
        if (hits.Count == 0)
        {
            return Task.FromResult(new ToolResult($"没有命中「{query}」。可换界面上的文字（按钮/页签名）再试。"));
        }

        var builder = new StringBuilder();
        builder.Append($"命中 {hits.Count} 篇（用 manual_read + id 读正文）：");
        foreach (var hit in hits)
        {
            builder.AppendLine();
            builder.Append($"- {hit.Id} — {hit.Title}");
            if (hit.Section is { Length: > 0 } section)
            {
                builder.Append($"（节 {section}）");
            }

            if (hit.Snippet.Length > 0)
            {
                builder.Append($"：{hit.Snippet}");
            }
        }

        return Task.FromResult(new ToolResult(builder.ToString(), ParamSummary: query));
    }
}
