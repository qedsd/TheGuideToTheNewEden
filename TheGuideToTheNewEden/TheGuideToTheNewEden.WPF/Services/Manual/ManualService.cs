using System.IO;
using System.Text;

namespace TheGuideToTheNewEden.WPF.Services.Manual;

/// <summary>一条检索结果。</summary>
/// <param name="Id">手册 id。</param>
/// <param name="Title">中文标题。</param>
/// <param name="Section">命中的节 slug（全文命中时为 <c>null</c>）。</param>
/// <param name="Score">相关度（越大越相关；只用于排序）。</param>
/// <param name="Snippet">命中处上下文（已截短）。</param>
public sealed record ManualSearchHit(string Id, string Title, string? Section, int Score, string Snippet);

/// <summary>一节的分片读取结果（<see cref="ManualService.ReadSection"/>）。</summary>
/// <param name="Id">手册 id。</param>
/// <param name="Slug">节 slug。</param>
/// <param name="Title">节标题（已剥 <c>{#slug}</c>）。</param>
/// <param name="Text">本片正文。</param>
/// <param name="Part">当前片号（从 1 起）。</param>
/// <param name="TotalParts">总片数。</param>
/// <param name="TotalChars">该节总字符数（未分片时等于 <see cref="Text"/> 长度）。</param>
public sealed record ManualSectionChunk(
    string Id,
    string Slug,
    string Title,
    string Text,
    int Part,
    int TotalParts,
    int TotalChars);

/// <summary>
/// 内置说明书服务：从输出目录 <c>Resources\Manuals\&lt;lang&gt;\</c> 读取 .md，解析 front-matter，
/// 建立 id / 别名索引，供窗口浏览（F1 / 主页「手册」）、Markdown 内链跳转与 AI 工具检索阅读。
/// <para>
/// 说明书正文**只进工具结果，绝不进系统提示词**（token 契约：工具声明是每次请求的固定前缀，
/// 正文进前缀会打爆 DeepSeek 前缀缓存）。所以这里只提供「按需查询」的面：
/// <see cref="Find"/> / <see cref="Search"/> / <see cref="Read"/> / <see cref="ReadSection"/>。
/// </para>
/// </summary>
public static class ManualService
{
    /// <summary>还没有对应语言目录时回退到的语言。</summary>
    private const string FallbackLanguage = "zh-CN";

    /// <summary>
    /// 说明书根目录覆盖（**留给探针的挂载点**，生产代码不要设）。
    /// <para>
    /// 非空时 <see cref="ResolveDirectory"/> 从它而不是程序输出目录解析——探针的输出目录里
    /// 未必带着随包资源（尤其重定向 <c>OutDir</c> 时），直接指向仓库源目录最省事。
    /// 与之配套的做法见 <c>probe/AssistantProbe/ManualSuite.cs</c>：设置后调 <see cref="Reload"/>。
    /// </para>
    /// </summary>
    public static string? DirectoryOverride { get; set; }

    /// <summary>单节一次读取的字符上限（超出按行分片，见 <see cref="ReadSection"/>）。</summary>
    public const int MaxSectionChars = 1800;

    private static readonly object LoadLock = new();
    private static List<ManualDocument> _all = [];
    private static Dictionary<string, ManualDocument> _byId = new(StringComparer.Ordinal);
    private static Dictionary<string, string> _byTool = new(StringComparer.Ordinal);
    private static string? _indexedUiLanguage;
    private static string _sourceLanguage = FallbackLanguage;
    private static bool _missingLogged;

    static ManualService()
    {
        // 语言切换后目录不同，索引整个换掉；已打开的浏览窗口自行订阅同一事件刷新列表。
        LanguageService.LanguageChanged += (_, _) => Reload();
    }

    /// <summary>当前语言的全部说明书（懒加载；语言切换后自动重建）。</summary>
    public static IReadOnlyList<ManualDocument> All
    {
        get
        {
            EnsureLoaded();
            return _all;
        }
    }

    /// <summary>丢弃索引（下次访问重建）；语言切换时自动调用。</summary>
    public static void Reload()
    {
        lock (LoadLock)
        {
            _indexedUiLanguage = null;
            _all = [];
            _byId = new Dictionary<string, ManualDocument>(StringComparer.Ordinal);
            _byTool = new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    /// <summary>当前索引**实际来自**哪种语言（界面语言缺目录时是回退语言 <c>zh-CN</c>）。</summary>
    public static string SourceLanguage
    {
        get
        {
            EnsureLoaded();
            return _sourceLanguage;
        }
    }

    /// <summary>
    /// 界面语言没有对应的说明书目录、正在显示回退语言（<c>zh-CN</c>）时 true。
    /// 手册窗口据此在顶部给一条"此功能暂无英文说明"的提示——**不静默**（见 MANUAL_DESIGN §4.5）。
    /// </summary>
    public static bool IsFallback
    {
        get
        {
            EnsureLoaded();
            return !string.Equals(_sourceLanguage, LanguageService.Value, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 按 AI 工具名反查"讲这个工具的那篇手册"（front-matter 的 <c>tools:</c> 反查表，id 字典序优先）。
    /// 没有手册声明该工具时返回 <c>null</c>（调用方不加"相关手册"提示）。
    /// </summary>
    public static string? FindManualIdForTool(string? toolName)
    {
        var key = (toolName ?? string.Empty).Trim();
        if (key.Length == 0)
        {
            return null;
        }

        EnsureLoaded();
        return _byTool.TryGetValue(key, out var id) ? id : null;
    }

    /// <summary>
    /// 按 id 找手册。宽松匹配：忽略大小写、忽略 <c>_</c> / <c>-</c> / <c>/</c> 分隔符差异
    /// （<c>characters_mail</c> 也能命中 <c>characters/mail</c>）、允许带 <c>#节</c> 与 <c>manual:</c> 前缀。
    /// </summary>
    public static ManualDocument? Find(string? id)
    {
        var key = NormalizeId(id);
        if (key.Length == 0)
        {
            return null;
        }

        EnsureLoaded();
        if (_byId.TryGetValue(key, out var exact))
        {
            return exact;
        }

        var folded = Fold(key);
        foreach (var document in _all)
        {
            if (Fold(document.Id) == folded)
            {
                return document;
            }
        }

        // 最后退一步：只比 id 的最后一段（「mail」也能命中 characters/mail）。
        ManualDocument? bySuffix = null;
        foreach (var document in _all)
        {
            var suffix = document.Id.Contains('/') ? document.Id[(document.Id.LastIndexOf('/') + 1)..] : document.Id;
            if (Fold(suffix) == folded)
            {
                if (bySuffix is not null)
                {
                    return null; // 多个命中宁可不认（与 ToolRegistry.FindLoose 同一哲学）
                }

                bySuffix = document;
            }
        }

        return bySuffix;
    }

    /// <summary>读一篇说明书的正文（已剥 front-matter）。找不到返回 <c>null</c>。</summary>
    public static string? Read(string? id) => Find(id)?.Body;

    /// <summary>
    /// 某篇说明书的源文件绝对路径（手册窗口的「打开文件位置」用；找不到返回 <c>null</c>）。
    /// 指向输出目录里那一份——用户可以直接改它、改完重启程序即生效（见 MANUAL_DESIGN §4.1）。
    /// </summary>
    public static string? GetFilePath(string? id)
    {
        var document = Find(id);
        if (document is null || ResolveDirectory() is not { } source)
        {
            return null;
        }

        return Path.Combine(source.Directory, document.Id.Replace('/', Path.DirectorySeparatorChar) + ".md");
    }

    /// <summary>
    /// 一篇的"概览"：标题 + 关联工具 + <c>tldr</c> 全文 + 章节目录（<c>slug · 标题 · 字符数</c>）。
    /// AI 的 <c>manual_read</c> 在没给 <c>section</c> 时返回它——**两步读取**（先概览定位、再取节）是省 token 的关键。
    /// </summary>
    public static string BuildOverview(ManualDocument document)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {document.Title}（{document.Id}）");
        if (document.Tools.Count > 0)
        {
            builder.AppendLine($"关联 AI 工具：{string.Join(", ", document.Tools)}");
        }

        if (document.FindSection("tldr") is { } tldr)
        {
            builder.AppendLine();
            builder.AppendLine($"## {tldr.Title}");
            builder.AppendLine(tldr.Content);
        }

        builder.AppendLine();
        builder.AppendLine("章节目录（用 section 参数只取一节）：");
        foreach (var section in document.Sections)
        {
            var slug = section.Slug.Length > 0 ? section.Slug : "(无锚点)";
            builder.AppendLine($"- {slug} · {section.Title} · {section.Content.Length} 字");
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>全部手册的目录（<c>- id · 标题</c> 一行一条）；空检索词时返回它。</summary>
    public static string BuildIndex()
    {
        var builder = new StringBuilder();
        foreach (var document in All)
        {
            builder.AppendLine($"- {document.Id} · {document.Title}");
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// 按 slug 分片读取一节：<paramref name="part"/> 从 1 起，越界钳到边界。
    /// **分片是无损的**——各片按顺序用换行拼回来等于 <see cref="ManualSection.Content"/> 原文，
    /// 因此模型可以把长节分几次读完而不丢内容（探针断言这一点）。
    /// 找不到该节时返回 <c>null</c>。
    /// </summary>
    public static ManualSectionChunk? ReadSection(ManualDocument document, string? slug, int part)
    {
        var section = document.FindSection(slug);
        if (section is null)
        {
            return null;
        }

        var chunks = SplitChunks(section.Content, MaxSectionChars);
        var index = Math.Clamp(part, 1, chunks.Count) - 1;
        return new ManualSectionChunk(
            document.Id,
            section.Slug,
            section.Title,
            chunks[index],
            index + 1,
            chunks.Count,
            section.Content.Length);
    }

    /// <summary>按行分片（每片尽量不超过 <paramref name="maxChars"/>）；拼回来等于原文。</summary>
    private static List<string> SplitChunks(string content, int maxChars)
    {
        var chunks = new List<string>();
        var buffer = new List<string>();
        var size = 0;

        foreach (var line in content.Split('\n'))
        {
            if (size > 0 && size + line.Length + 1 > maxChars)
            {
                chunks.Add(string.Join("\n", buffer));
                buffer.Clear();
                size = 0;
            }

            buffer.Add(line);
            size = size == 0 ? line.Length : size + line.Length + 1;
        }

        if (buffer.Count > 0)
        {
            chunks.Add(string.Join("\n", buffer));
        }

        if (chunks.Count == 0)
        {
            chunks.Add(string.Empty);
        }

        return chunks;
    }

    /// <summary>
    /// 关键词检索：所有词都命中才算候选（AND），按 id / 标题 / 别名 / 节标题 / 正文加权，
    /// 返回最相关的若干条（带命中节与片段）。
    /// </summary>
    public static IReadOnlyList<ManualSearchHit> Search(string? query, int max = 5)
    {
        var terms = SplitTerms(query);
        if (terms.Count == 0)
        {
            return [];
        }

        // 上限放宽到 30：AI 工具自己在入参侧钳 8；浏览窗口的列表过滤需要更多结果。
        max = Math.Clamp(max, 1, 30);
        var hits = new List<ManualSearchHit>();
        foreach (var document in All)
        {
            var score = 0;
            var snippet = string.Empty;
            string? snippetSection = null;

            foreach (var term in terms)
            {
                var termScore = ScoreTerm(document, term, ref snippet, ref snippetSection);
                if (termScore <= 0)
                {
                    score = 0; // AND 语义：一个词都没中就出局
                    break;
                }

                score += termScore;
            }

            if (score > 0)
            {
                hits.Add(new ManualSearchHit(document.Id, document.Title, snippetSection, score, snippet));
            }
        }

        return hits.OrderByDescending(hit => hit.Score).ThenBy(hit => hit.Id, StringComparer.Ordinal).Take(max).ToList();
    }

    /// <summary>说明书根目录是否存在（当前语言或回退语言）。</summary>
    public static bool IsAvailable() => ResolveDirectory() is not null;

    private static void EnsureLoaded()
    {
        var language = LanguageService.Value;
        lock (LoadLock)
        {
            if (_indexedUiLanguage == language && _all.Count > 0)
            {
                return;
            }

            _indexedUiLanguage = language;
            _all = [];
            _byId = new Dictionary<string, ManualDocument>(StringComparer.Ordinal);
            _byTool = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var resolved = ResolveDirectory();
        if (resolved is not { } source)
        {
            if (!_missingLogged)
            {
                _missingLogged = true;
                Core.Log.Warn($"说明书目录不存在（{language} 与 {FallbackLanguage} 都没有）：Resources\\Manuals");
            }

            return;
        }

        var documents = new List<ManualDocument>();
        foreach (var file in Directory.EnumerateFiles(source.Directory, "*.md", SearchOption.AllDirectories))
        {
            try
            {
                var relative = Path.GetRelativePath(source.Directory, file);
                var id = relative[..^".md".Length].Replace('\\', '/');
                documents.Add(ManualDocument.Parse(id, File.ReadAllText(file)));
            }
            catch (Exception ex)
            {
                Core.Log.Warn($"说明书解析失败，已跳过：{file} · {ex.Message}");
            }
        }

        documents.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));

        // 工具 → 手册 的反查表（AI 工具失败时"去哪儿查怎么修"）：同一工具被多篇声明时取 id 字典序最靠前的。
        var byTool = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var document in documents)
        {
            foreach (var tool in document.Tools)
            {
                if (tool.Length > 0 && !byTool.ContainsKey(tool))
                {
                    byTool[tool] = document.Id;
                }
            }
        }

        lock (LoadLock)
        {
            _all = documents;
            _byId = documents.ToDictionary(document => document.Id, StringComparer.Ordinal);
            _byTool = byTool;
            _sourceLanguage = source.Language;
        }

        Core.Log.Info($"说明书索引：{documents.Count} 篇 / {documents.Sum(d => d.Body.Length)} 字符 / {source.Language}"
            + (string.Equals(source.Language, language, StringComparison.OrdinalIgnoreCase) ? string.Empty : $"（界面语言 {language} 无内容，已回退）"));
    }

    /// <summary>当前语言的说明书目录；当前语言没有就回退 zh-CN，再没有就 null。</summary>
    private static (string Language, string Directory)? ResolveDirectory()
    {
        var root = DirectoryOverride ?? Path.Combine(AppContext.BaseDirectory, "Resources", "Manuals");
        foreach (var language in new[] { LanguageService.Value, FallbackLanguage })
        {
            var directory = Path.Combine(root, language);
            if (Directory.Exists(directory))
            {
                return (language, directory);
            }
        }

        return null;
    }

    /// <summary>单个词的打分；<paramref name="snippet"/> 命中时顺便记下第一处片段。</summary>
    private static int ScoreTerm(ManualDocument document, string term, ref string snippet, ref string? snippetSection)
    {
        if (Fold(document.Id) == Fold(term))
        {
            return 60;
        }

        if (Fold(document.Id).Contains(term, StringComparison.Ordinal))
        {
            return 40;
        }

        if (Fold(document.Title).Contains(term, StringComparison.Ordinal))
        {
            return 30;
        }

        foreach (var alias in document.Aliases)
        {
            if (Fold(alias).Contains(term, StringComparison.Ordinal))
            {
                return 20;
            }
        }

        foreach (var section in document.Sections)
        {
            if (Fold(section.Title).Contains(term, StringComparison.Ordinal))
            {
                RememberSnippet(ref snippet, ref snippetSection, section.Slug.Length > 0 ? section.Slug : null, section.Title);
                return 15;
            }
        }

        if (Fold(document.Body).Contains(term, StringComparison.Ordinal))
        {
            foreach (var section in document.Sections)
            {
                var index = Fold(section.Content).IndexOf(term, StringComparison.Ordinal);
                if (index >= 0)
                {
                    RememberSnippet(ref snippet, ref snippetSection, section.Slug.Length > 0 ? section.Slug : null, section.Content, index, term.Length);
                    break;
                }
            }

            return 8;
        }

        return 0;
    }

    private static void RememberSnippet(ref string snippet, ref string? snippetSection, string? section, string source)
        => RememberSnippet(ref snippet, ref snippetSection, section, source, 0, source.Length);

    private static void RememberSnippet(ref string snippet, ref string? snippetSection, string? section, string source, int index, int length)
    {
        if (snippet.Length > 0)
        {
            return;
        }

        snippetSection = section;
        var start = Math.Max(0, index - 16);
        var take = Math.Min(source.Length - start, Math.Max(24, length + 32));
        snippet = source.Substring(start, take).Replace('\n', ' ').Trim();
        if (snippet.Length > 80)
        {
            snippet = snippet[..80] + "…";
        }
    }

    /// <summary>把查询拆成小写词（丢掉纯标点）。</summary>
    private static List<string> SplitTerms(string? query)
        => (query ?? string.Empty)
            .Split([' ', '\t', ',', '，', '、'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Fold)
            .Where(term => term.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>剥掉 id 的 <c>manual:</c> 前缀与 <c>#锚点</c>，统一分隔符。</summary>
    private static string NormalizeId(string? id)
    {
        var key = (id ?? string.Empty).Trim();
        if (key.StartsWith("manual:", StringComparison.OrdinalIgnoreCase))
        {
            key = key["manual:".Length..];
        }

        var hash = key.IndexOf('#');
        if (hash >= 0)
        {
            key = key[..hash];
        }

        // manual://market#errors 这种"带 // 的协议写法"也认（AI 回答里的链接就是它）。
        return key.Replace('\\', '/').TrimStart('/').Trim();
    }

    /// <summary>
    /// 检索/查找用折叠：小写，并去掉空格与 <c>_</c> <c>-</c> <c>/</c> 三种分隔符——
    /// 于是 <c>characters/mail</c>、<c>characters_mail</c>、<c>Characters-Mail</c> 视为同一个 id
    /// （模型从对话里抄 id 时常把分隔符写错，宽松匹配比报错有用）。
    /// </summary>
    private static string Fold(string text)
        => text.ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace("/", string.Empty);
}
