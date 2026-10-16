namespace TheGuideToTheNewEden.WPF.Services.Manual;

/// <summary>说明书的一个 H2 节（标题末尾带 {#slug} 锚点）。</summary>
/// <param name="Slug">英文锚点（如 <c>steps</c>），与写作规范固定九节对应。</param>
/// <param name="Title">标题原文（已剥掉 <c>{#slug}</c>）。</param>
/// <param name="Content">本节正文（不含标题行）。</param>
public sealed record ManualSection(string Slug, string Title, string Content);

/// <summary>
/// 一篇内置说明书的解析结果：front-matter 元数据 + 正文 + 按 H2 切分的节。
/// <para>
/// front-matter 是「最小 YAML」：只认 <c>key: value</c> 与 <c>key: [a, b, c]</c> 两种形状
/// （键集见 MANUAL_DESIGN.md：id/title/group/page/tools/aliases/updated/children）。
/// 缺 front-matter 或解析失败时整篇当正文、元数据取默认值，不让一篇坏文件拖垮整个目录。
/// </para>
/// </summary>
/// <param name="Id">手册 id = 相对语言目录的路径去掉 .md（如 <c>characters/mail</c>）。</param>
/// <param name="Title">中文标题（H1 与之同文）。</param>
/// <param name="Group">导航分组键（Nav.*）。</param>
/// <param name="Page">对应页面 XAML（相对项目根）；横切文档没有。</param>
/// <param name="Tools">本功能关联的 AI 工具名（检索/反查用）。</param>
/// <param name="Aliases">口语别名（检索用，含英文词）。</param>
/// <param name="Updated">内容日期（评审用，不参与运行时逻辑）。</param>
/// <param name="Children">子页手册 id 列表。</param>
/// <param name="Body">剥掉 front-matter 后的完整正文（渲染与全文读取都用它）。</param>
/// <param name="Sections">按 H2 切分的节（含无 slug 的标题，slug 为空串）。</param>
public sealed record ManualDocument(
    string Id,
    string Title,
    string Group,
    string? Page,
    IReadOnlyList<string> Tools,
    IReadOnlyList<string> Aliases,
    string? Updated,
    IReadOnlyList<string> Children,
    string Body,
    IReadOnlyList<ManualSection> Sections)
{
    /// <summary>H2 标题末尾的 <c>{#slug}</c>（写作规范的固定锚点）。</summary>
    private static readonly System.Text.RegularExpressions.Regex SlugRegex =
        new(@"\{\s*#([a-z0-9-]+)\s*\}\s*$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>从 Markdown 全文解析一篇说明书。</summary>
    /// <param name="id">手册 id（来自相对路径，不由 front-matter 决定——路径就是 id）。</param>
    /// <param name="markdown">文件全文。</param>
    public static ManualDocument Parse(string id, string markdown)
    {
        var body = markdown ?? string.Empty;
        var meta = new Dictionary<string, string>(StringComparer.Ordinal);

        if (body.StartsWith("---", StringComparison.Ordinal))
        {
            var firstLineEnd = body.IndexOf('\n');
            if (firstLineEnd > 0)
            {
                var close = IndexOfFrontMatterClose(body, firstLineEnd + 1);
                if (close > firstLineEnd)
                {
                    foreach (var rawLine in body.Substring(firstLineEnd + 1, close - firstLineEnd - 1).Split('\n'))
                    {
                        var sep = rawLine.IndexOf(':');
                        if (sep > 0)
                        {
                            var key = rawLine[..sep].Trim();
                            var value = rawLine[(sep + 1)..].Trim().TrimEnd('\r');
                            if (key.Length > 0)
                            {
                                meta[key] = value;
                            }
                        }
                    }

                    // 关闭行的下一个字符起就是正文（关掉行是 "---\n"，跳过其后的空行）。
                    body = body[(close + 4)..].TrimStart('\r', '\n');
                }
            }
        }

        var title = meta.GetValueOrDefault("title", id);
        return new ManualDocument(
            Id: id,
            Title: title,
            Group: meta.GetValueOrDefault("group", string.Empty),
            Page: meta.TryGetValue("page", out var page) && page.Length > 0 ? page : null,
            Tools: ParseList(meta.GetValueOrDefault("tools")),
            Aliases: ParseList(meta.GetValueOrDefault("aliases")),
            Updated: meta.TryGetValue("updated", out var updated) && updated.Length > 0 ? updated : null,
            Children: ParseList(meta.GetValueOrDefault("children")),
            Body: body,
            Sections: ParseSections(body, title));
    }

    /// <summary>取一节（按 slug，忽略大小写与 <c>#</c> 前缀）；找不到返回 <c>null</c>。</summary>
    public ManualSection? FindSection(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var wanted = slug.Trim().TrimStart('#');
        foreach (var section in Sections)
        {
            if (string.Equals(section.Slug, wanted, StringComparison.OrdinalIgnoreCase))
            {
                return section;
            }
        }

        return null;
    }

    /// <summary>front-matter 关闭行（<c>---</c>）的位置；没找到返回 -1。</summary>
    private static int IndexOfFrontMatterClose(string text, int start)
    {
        var index = text.IndexOf("\n---", start, StringComparison.Ordinal);
        if (index < 0)
        {
            return -1;
        }

        return index + 1; // 指向 "---" 行首；正文从它 +4（---\n）开始
    }

    /// <summary>解析 <c>[a, b, c]</c> 形状的列表值；空值给空表。</summary>
    private static IReadOnlyList<string> ParseList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
        {
            trimmed = trimmed[1..^1];
        }

        return trimmed
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Length > 0)
            .ToList();
    }

    /// <summary>按 H2 行切节；H1 与空行归入正文，不单独成节。</summary>
    private static IReadOnlyList<ManualSection> ParseSections(string body, string fallbackTitle)
    {
        var sections = new List<ManualSection>();
        var currentSlug = string.Empty;
        var currentTitle = fallbackTitle;
        var buffer = new System.Text.StringBuilder();
        var started = false;

        foreach (var rawLine in body.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                if (started)
                {
                    sections.Add(new ManualSection(currentSlug, currentTitle, buffer.ToString().Trim()));
                }

                started = true;
                var heading = line[3..].Trim();
                currentSlug = SlugRegex.Match(heading) is { Success: true } match ? match.Groups[1].Value : string.Empty;
                currentTitle = SlugRegex.Replace(heading, string.Empty).Trim();
                buffer.Clear();
                continue;
            }

            if (started)
            {
                buffer.AppendLine(line);
            }
        }

        if (started)
        {
            sections.Add(new ManualSection(currentSlug, currentTitle, buffer.ToString().Trim()));
        }

        return sections;
    }
}
