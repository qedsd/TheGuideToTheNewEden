using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.WPF.Services.Translation;

/// <summary>一条"可能是专有名词、但术语库没有"的候选。</summary>
public sealed class GlossaryCandidate
{
    public string English { get; set; } = string.Empty;

    /// <summary>出现次数。</summary>
    public int Count { get; set; }

    /// <summary>最近一次的原文（截断）。</summary>
    public string Sample { get; set; } = string.Empty;

    /// <summary>最近一次的译文（截断）。</summary>
    public string SampleTranslation { get; set; } = string.Empty;

    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// AI 新词候选：翻译成功后，把"原文里像专有名词、但术语库没有、且没有原样出现在译文里"的词
/// 记进候选表，供用户在设置页**人工确认**成正式术语。
/// <para>
/// 刻意**不自动写入术语表**：模型输出无法可靠地反推出"哪个英文词对应哪个中文词"，
/// 自动写入会把错译固化下来（比不写更糟）。这里只做"排队 + 计数 + 留样例"，由人拍板。
/// </para>
/// </summary>
public static class GlossaryCandidateService
{
    /// <summary>候选上限（超出丢最旧/最少见的）。</summary>
    private const int MaxCandidates = 300;

    private static readonly string FilePath = Path.Combine(Services.SettingsService.DataPath, "Configs", "GlossaryCandidates.json");

    /// <summary>像专有名词的片段：大写字母开头，可含数字/连字符/点/撇号，允许 1..3 个词。</summary>
    private static readonly Regex PhraseRegex = new(@"[A-Z][A-Za-z0-9'\-\.]*(?:\s+[A-Z][A-Za-z0-9'\-\.]*){0,2}", RegexOptions.Compiled);

    /// <summary>常见英文词/寒暄语，不作为候选（避免候选表被 "The"、"Hello" 刷满）。</summary>
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "The", "A", "An", "And", "Or", "But", "If", "Then", "Than", "That", "This", "These", "Those",
        "I", "You", "He", "She", "It", "We", "They", "My", "Your", "His", "Her", "Our", "Their", "Me", "Us", "Them",
        "Is", "Are", "Was", "Were", "Be", "Been", "Am", "Do", "Does", "Did", "Have", "Has", "Had", "Can", "Could",
        "Will", "Would", "Shall", "Should", "May", "Might", "Must", "To", "Of", "In", "On", "At", "For", "With",
        "From", "By", "As", "So", "No", "Not", "Yes", "Ok", "Okay", "Hi", "Hello", "Hey", "Thanks", "Thank", "Please",
        "Sorry", "Good", "Bad", "Now", "Here", "There", "What", "When", "Where", "Who", "Why", "How", "All", "Any",
        "Some", "One", "Two", "Three", "New", "Old", "Big", "Small", "More", "Most", "Very", "Just", "Also", "Only",
        "About", "After", "Before", "Over", "Under", "Into", "Out", "Up", "Down", "Off", "Get", "Got", "Go", "Going",
        "Come", "Came", "See", "Saw", "Look", "Need", "Want", "Like", "Know", "Think", "Use", "Used", "Make", "Made",
        "Jita", "Amarr", "Dodixie", "Rens", "Hek", "EVE", "Eve", "ISK", "LP", "PVP", "PVE", "WTF", "LOL", "GG",
    };

    private static readonly object Sync = new();
    private static List<GlossaryCandidate> _items = [];
    private static HashSet<string> _ignored = new(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded;

    /// <summary>候选或忽略名单变化。</summary>
    public static event EventHandler? Changed;

    /// <summary>当前候选（按次数/最近时间降序）。</summary>
    public static IReadOnlyList<GlossaryCandidate> Items
    {
        get
        {
            lock (Sync)
            {
                Load();
                return Sort(_items).ToList();
            }
        }
    }

    public static int Count
    {
        get
        {
            lock (Sync)
            {
                Load();
                return _items.Count;
            }
        }
    }

    public static void Initialize()
    {
        lock (Sync)
        {
            Load();
        }
    }

    /// <summary>忽略某个候选（不再记录，用户已判断它不是专有名词）。</summary>
    public static void Ignore(string english)
    {
        if (string.IsNullOrWhiteSpace(english))
        {
            return;
        }

        lock (Sync)
        {
            Load();
            _ignored.Add(english.Trim());
            _items.RemoveAll(p => string.Equals(p.English, english.Trim(), StringComparison.OrdinalIgnoreCase));
            Save();
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static void Remove(string english)
    {
        if (string.IsNullOrWhiteSpace(english))
        {
            return;
        }

        lock (Sync)
        {
            Load();
            if (_items.RemoveAll(p => string.Equals(p.English, english.Trim(), StringComparison.OrdinalIgnoreCase)) == 0)
            {
                return;
            }

            Save();
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static void Clear()
    {
        lock (Sync)
        {
            Load();
            if (_items.Count == 0)
            {
                return;
            }

            _items.Clear();
            Save();
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// 从一次成功的翻译里挑候选：原文中"像专有名词"的片段，且
    /// ① 术语库（SDE + 用户）里没有；② 不在忽略名单；③ 没有原样出现在译文里（说明模型翻过或丢了它）。
    /// </summary>
    /// <param name="source">原文（传**预清洗后**的纯文本：带标签时标签会被当成"像专有名词"的片段记下来）。</param>
    /// <param name="translation">译文。</param>
    public static void Record(string? source, string? translation)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        var output = translation ?? string.Empty;
        var changed = false;

        lock (Sync)
        {
            Load();
            foreach (Match match in PhraseRegex.Matches(source))
            {
                var phrase = match.Value.Trim();
                if (phrase.Length < 3 || StopWords.Contains(phrase) || _ignored.Contains(phrase))
                {
                    continue;
                }

                if (GlossaryService.IsKnownEnglish(phrase))
                {
                    continue;
                }

                if (output.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                {
                    // 原文名词被原样搬进译文：按术语规范"保留原文"是期望行为，不进候选
                    continue;
                }

                var existing = _items.FirstOrDefault(p => string.Equals(p.English, phrase, StringComparison.OrdinalIgnoreCase));
                if (existing is null)
                {
                    _items.Add(new GlossaryCandidate
                    {
                        English = phrase,
                        Count = 1,
                        Sample = Truncate(source, 120),
                        SampleTranslation = Truncate(output, 120),
                    });
                }
                else
                {
                    existing.Count++;
                    existing.Sample = Truncate(source, 120);
                    existing.SampleTranslation = Truncate(output, 120);
                    existing.LastSeenUtc = DateTime.UtcNow;
                }

                changed = true;
            }

            if (!changed)
            {
                return;
            }

            if (_items.Count > MaxCandidates)
            {
                _items = Sort(_items).Take(MaxCandidates).ToList();
            }

            Save();
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static IEnumerable<GlossaryCandidate> Sort(IEnumerable<GlossaryCandidate> items)
        => items.OrderByDescending(p => p.Count).ThenByDescending(p => p.LastSeenUtc);

    private static string Truncate(string text, int length)
    {
        var normalized = (text ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= length ? normalized : normalized[..length] + "…";
    }

    private static void Load()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        try
        {
            if (File.Exists(FilePath))
            {
                var file = JsonConvert.DeserializeObject<CandidateFile>(File.ReadAllText(FilePath));
                _items = file?.Items?.Where(p => !string.IsNullOrWhiteSpace(p.English)).ToList() ?? [];
                _ignored = new HashSet<string>(file?.Ignored ?? [], StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                _items = [];
                _ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            _items = [];
            _ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void Save()
    {
        try
        {
            var folder = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(FilePath, JsonConvert.SerializeObject(
                new CandidateFile { Items = Sort(_items).ToList(), Ignored = _ignored.ToList() },
                Formatting.Indented));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private sealed class CandidateFile
    {
        public List<GlossaryCandidate> Items { get; set; } = [];

        public List<string> Ignored { get; set; } = [];
    }
}
