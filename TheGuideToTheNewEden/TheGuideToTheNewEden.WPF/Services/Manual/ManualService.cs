using System.IO;

namespace TheGuideToTheNewEden.WPF.Services.Manual;

/// <summary>一条检索结果。</summary>
/// <param name="Id">手册 id。</param>
/// <param name="Title">中文标题。</param>
/// <param name="Section">命中的节 slug（全文命中时为 <c>null</c>）。</param>
/// <param name="Score">相关度（越大越相关；只用于排序）。</param>
/// <param name="Snippet">命中处上下文（已截短）。</param>
public sealed record ManualSearchHit(string Id, string Title, string? Section, int Score, string Snippet);

/// <summary>
/// 内置说明书服务：从输出目录 <c>Resources\Manuals\&lt;lang&gt;\</c> 读取 .md，解析 front-matter，
/// 建立 id / 别名索引，供窗口浏览（F1 / 主页「手册」）、Markdown 内链跳转与 AI 工具检索阅读。
/// <para>
/// 说明书正文**只进工具结果，绝不进系统提示词**（token 契约：工具声明是每次请求的固定前缀，
/// 正文进前缀会打爆 DeepSeek 前缀缓存）。所以这里只提供「按需查询」的面：
/// <see cref="Find"/> / <see cref="Search"/> / <see cref="Read"/>。
/// </para>
/// </summary>
public static class ManualService
{
    /// <summary>还没有对应语言目录时回退到的语言。</summary>
    private const string FallbackLanguage = "zh-CN";

    private static readonly object LoadLock = new();
    private static List<ManualDocument> _all = [];
    private static Dictionary<string, ManualDocument> _byId = new(StringComparer.Ordinal);
    private static string? _loadedLanguage;
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
            _loadedLanguage = null;
            _all = [];
            _byId = new Dictionary<string, ManualDocument>(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// 按 id 找手册。宽松匹配：忽略大小写、允许 <c>_</c> 与 <c>-</c> 互换、允许带 <c>#节</c> 与 <c>manual:</c> 前缀。
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
            if (_loadedLanguage == language && _all.Count > 0)
            {
                return;
            }

            _loadedLanguage = language;
            _all = [];
            _byId = new Dictionary<string, ManualDocument>(StringComparer.Ordinal);
        }

        var directory = ResolveDirectory();
        if (directory is null)
        {
            if (!_missingLogged)
            {
                _missingLogged = true;
                Core.Log.Warn($"说明书目录不存在（{language} 与 {FallbackLanguage} 都没有）：Resources\\Manuals");
            }

            return;
        }

        var documents = new List<ManualDocument>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.md", SearchOption.AllDirectories))
        {
            try
            {
                var relative = Path.GetRelativePath(directory, file);
                var id = relative[..^".md".Length].Replace('\\', '/');
                documents.Add(ManualDocument.Parse(id, File.ReadAllText(file)));
            }
            catch (Exception ex)
            {
                Core.Log.Warn($"说明书解析失败，已跳过：{file} · {ex.Message}");
            }
        }

        documents.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
        lock (LoadLock)
        {
            _all = documents;
            _byId = documents.ToDictionary(document => document.Id, StringComparer.Ordinal);
        }
    }

    /// <summary>当前语言的说明书目录；当前语言没有就回退 zh-CN，再没有就 null。</summary>
    private static string? ResolveDirectory()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Resources", "Manuals");
        foreach (var language in new[] { LanguageService.Value, FallbackLanguage })
        {
            var directory = Path.Combine(root, language);
            if (Directory.Exists(directory))
            {
                return directory;
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

        return key.Replace('\\', '/').Trim();
    }

    /// <summary>检索用折叠：小写、去空格、<c>_</c> 与 <c>-</c> 视为同字符。</summary>
    private static string Fold(string text)
        => text.ToLowerInvariant().Replace('_', '-').Replace(" ", string.Empty);
}
