using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 内置说明书阅读：按 id 读一篇说明书的 Markdown 正文，或用 section 只取某一节。
/// <para>
/// 全文最长约 9000 字符，而工具结果有截断上限，所以默认建议先 <see cref="ManualSearchTool"/> 定位、
/// 再带 <c>section</c> 取节（tldr / where / prereq / steps / fields / ai / errors / limits / related）。
/// </para>
/// </summary>
public sealed class ManualReadTool : ITool
{
    public string Name => "manual_read";

    public string DisplayNameKey => "AssistantTool_manual_read";

    public string Description =>
        "读取一篇内置说明书（Markdown）。id 来自 manual_search；可用 section 只取某一节（如 tldr/steps/errors/limits）。没有 section 时返回全文。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["required"] = new JsonArray("id"),
        ["properties"] = new JsonObject
        {
            ["id"] = new JsonObject { ["type"] = "string", ["description"] = "说明书 id（manual_search 返回的）" },
            ["section"] = new JsonObject { ["type"] = "string", ["description"] = "只取这一节（节 slug，可选）" },
        },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var id = ToolArgs.ReadString(args, "id")?.Trim() ?? string.Empty;
        if (id.Length == 0)
        {
            return Task.FromResult(new ToolResult("缺少 id：先用 manual_search 检索，再用返回的 id 读取。"));
        }

        var document = Services.Manual.ManualService.Find(id);
        if (document is null)
        {
            return Task.FromResult(new ToolResult($"找不到说明书 {id}。先用 manual_search 检索拿到正确的 id。"));
        }

        var sectionSlug = ToolArgs.ReadString(args, "section")?.Trim();
        if (sectionSlug is { Length: > 0 })
        {
            var section = document.FindSection(sectionSlug);
            if (section is null)
            {
                var slugs = string.Join(" / ", document.Sections
                    .Where(item => item.Slug.Length > 0)
                    .Select(item => item.Slug));
                return Task.FromResult(new ToolResult(
                    $"{document.Id} 没有「{sectionSlug}」这一节。可用的节：{slugs}。"));
            }

            return Task.FromResult(new ToolResult(
                $"# {document.Title} · {section.Title}\n\n{section.Content}",
                ParamSummary: $"{document.Id}#{section.Slug}"));
        }

        var builder = new StringBuilder();
        builder.AppendLine($"# {document.Title}（{document.Id}）");
        if (document.Tools.Count > 0)
        {
            builder.AppendLine($"关联 AI 工具：{string.Join(", ", document.Tools)}");
        }

        builder.Append(document.Body);
        return Task.FromResult(new ToolResult(builder.ToString(), ParamSummary: document.Id));
    }
}
