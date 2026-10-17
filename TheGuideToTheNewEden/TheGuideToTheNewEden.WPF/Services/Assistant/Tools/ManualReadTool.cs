using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 内置说明书阅读：按 id 读一篇说明书的概览（一句话 + 章节目录）或某一节正文。
/// <para>
/// 两步读取是省 token 的关键：全文最长约 9000 字符，而工具结果有 4000 字符截断上限，
/// 所以先用 <see cref="ManualSearchTool"/> 定位、再不带 <c>section</c> 拿概览、
/// 最后按需 <c>section</c> 取节；超长节由 <c>part</c> 分片（返回里会写清片号与总片数）。
/// </para>
/// </summary>
public sealed class ManualReadTool : ITool
{
    public string Name => "manual_read";

    public string DisplayNameKey => "AssistantTool_manual_read";

    public string Description =>
        "读取内置说明书：默认返回一句话摘要与章节目录；指定小节时返回该节正文，过长则按顺序分片返回（片号见返回）。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["required"] = new JsonArray("id"),
        ["properties"] = new JsonObject
        {
            ["id"] = new JsonObject { ["type"] = "string", ["description"] = "说明书 id（示例：market）" },
            ["section"] = new JsonObject { ["type"] = "string", ["description"] = "节 slug，可选（见概览目录）" },
            ["part"] = new JsonObject { ["type"] = "integer", ["description"] = "分片号，从 1 起（默认 1）" },
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
            return Task.FromResult(new ToolResult(NotFound(id)));
        }

        var sectionSlug = ToolArgs.ReadString(args, "section")?.Trim();
        if (string.IsNullOrEmpty(sectionSlug))
        {
            return Task.FromResult(new ToolResult(
                Services.Manual.ManualService.BuildOverview(document),
                ParamSummary: document.Id));
        }

        var part = Math.Max(1, ToolArgs.ReadInt(args, "part", 1));
        var chunk = Services.Manual.ManualService.ReadSection(document, sectionSlug, part);
        if (chunk is null)
        {
            var slugs = string.Join(" / ", document.Sections
                .Where(item => item.Slug.Length > 0)
                .Select(item => item.Slug));
            return Task.FromResult(new ToolResult(
                $"{document.Id} 没有「{sectionSlug}」这一节。可用的节：{slugs}。"));
        }

        var builder = new StringBuilder();
        builder.Append($"# {document.Title} · {chunk.Title}（{chunk.Slug} · {chunk.Part}/{chunk.TotalParts}）");
        builder.AppendLine();
        builder.AppendLine();
        builder.Append(chunk.Text);
        if (chunk.TotalParts > 1)
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.Append(chunk.Part < chunk.TotalParts
                ? $"（本节共 {chunk.TotalParts} 片 / {chunk.TotalChars} 字，用 part={chunk.Part + 1} 读下一片）"
                : $"（本节已读完，共 {chunk.TotalParts} 片 / {chunk.TotalChars} 字）");
        }

        return Task.FromResult(new ToolResult(builder.ToString(), ParamSummary: $"{document.Id}#{chunk.Slug}"));
    }

    /// <summary>找不到 id 时给"最接近的几个 id"，让模型能自我纠正（不抛异常）。</summary>
    private static string NotFound(string id)
    {
        var candidates = Services.Manual.ManualService.Search(id, 5).Select(hit => hit.Id).ToList();
        if (candidates.Count == 0)
        {
            candidates = Services.Manual.ManualService.All.Take(8).Select(document => document.Id).ToList();
        }

        return candidates.Count == 0
            ? $"找不到说明书 {id}（本机没有可用的说明书目录）。"
            : $"找不到说明书 {id}。相近的 id：{string.Join(" / ", candidates)}；也可以用 manual_search 检索。";
    }
}
