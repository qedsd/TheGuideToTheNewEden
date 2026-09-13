using System.Text;
using System.Text.RegularExpressions;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Models.Translation;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WPF.Services.Translation;

/// <summary>术语命中的一项：命中位置 + 打分（用于排序与去重）。</summary>
/// <param name="Entry">术语表条目。</param>
/// <param name="Start">在原文中的起始下标。</param>
/// <param name="Length">在原文中的长度。</param>
/// <param name="Score">分数（越大越优先）。</param>
public sealed record GlossaryHit(GlossaryEntry Entry, int Start, int Length, int Score);

/// <summary>
/// 术语库：把 SDE 主库（英文）与本地化库（中文）配成中英术语对，并在翻译时按原文**命中式**注入。
/// <para>
/// 为什么是命中式而不是整库喂给模型：实测两侧合计约 6.6 万条术语对（≈40 万 token），远超任何模型的上下文，
/// 而且绝大部分与当前句子无关。这里按原文做最长匹配，只把命中的 5–40 条（约几百 token）放进提示词。
/// </para>
/// <para>
/// 匹配做法：英文按"词起点 + 1..N 个连续词"的原文子串查表（保留词间的连字符/点/空格，
/// 因此 <c>Jita IV - Moon 4 - Caldari Navy Assembly Plant</c> 这类带标点的全名也能命中）；
/// 中文按 CJK 连续段内的 2..N 字滑窗查表。两者都只做字典查表，整句耗时在毫秒级。
/// </para>
/// </summary>
public static class GlossaryService
{
    /// <summary>英文候选最多跨多少个词（如 "Caldari Navy Assembly Plant" 是 4 个词）。</summary>
    private const int MaxEnglishWords = 6;

    /// <summary>单次扫描的文本上限（超长文本只取前 N 字符做术语命中）。</summary>
    private const int MaxScanLength = 4000;

    private static readonly Regex EnglishWordRegex = new(@"[\p{L}\p{N}][\p{L}\p{N}'\-\.]*", RegexOptions.Compiled);

    private static readonly object Sync = new();

    private static Dictionary<string, List<GlossaryEntry>> _byEnglish = new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, List<GlossaryEntry>> _byChinese = new(StringComparer.Ordinal);
    private static Dictionary<string, List<GlossaryEntry>> _userByEnglish = new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, List<GlossaryEntry>> _userByChinese = new(StringComparer.Ordinal);
    private static int _maxEnglishLength = 40;
    private static int _maxChineseLength = 16;
    private static Task<GlossaryBuildResult>? _buildTask;
    private static string _userSignatureSeen = string.Empty;

    /// <summary>术语库是否已载入（SDE 部分；用户术语表始终可用）。</summary>
    public static bool IsReady { get; private set; }

    /// <summary>SDE 术语条数。</summary>
    public static int Count { get; private set; }

    /// <summary>用户自定义术语条数。</summary>
    public static int UserCount => UserGlossaryService.Count;

    /// <summary>内容签名（SDE + 用户术语；用于 AI 结果缓存键：任一侧变了缓存自动失效）。</summary>
    public static string Signature => $"{_signature}-{UserGlossaryService.Signature}";

    private static string _signature = "empty";

    /// <summary>术语库变化（载入/重建完成）后触发，供界面刷新条数。</summary>
    public static event EventHandler? Changed;

    /// <summary>上一次构建的耗时与统计（诊断用）。</summary>
    public static string? LastBuildInfo { get; private set; }

    /// <summary>
    /// 确保术语库已载入（幂等；并发调用共享同一次构建）。
    /// 每次进程只从数据库抽取一次，约 0.5–1 秒；不做磁盘缓存以免与 SDE 更新脱节。
    /// </summary>
    public static Task<GlossaryBuildResult> EnsureLoadedAsync(IProgress<(int Done, int Total)>? progress = null, CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            if (IsReady)
            {
                return Task.FromResult(new GlossaryBuildResult(Count, TimeSpan.Zero, false));
            }

            return _buildTask ??= Task.Run(() => Build(progress, cancellationToken), cancellationToken);
        }
    }

    /// <summary>强制重新抽取（设置页的"重新载入术语库"）。</summary>
    public static Task<GlossaryBuildResult> ReloadAsync(IProgress<(int Done, int Total)>? progress = null, CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            _buildTask = null;
            IsReady = false;
        }

        return EnsureLoadedAsync(progress, cancellationToken);
    }

    private static GlossaryBuildResult Build(IProgress<(int Done, int Total)>? progress, CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        var entries = TranslationDbService.ExtractGlossary(
            (done, total) => progress?.Report((done, total)),
            cancellationToken);

        var byEnglish = new Dictionary<string, List<GlossaryEntry>>(StringComparer.OrdinalIgnoreCase);
        var byChinese = new Dictionary<string, List<GlossaryEntry>>(StringComparer.Ordinal);
        var maxEnglish = 0;
        var maxChinese = 0;

        foreach (var entry in entries)
        {
            Add(byEnglish, entry.English, entry);
            Add(byChinese, entry.Chinese, entry);
            maxEnglish = Math.Max(maxEnglish, entry.English.Length);
            maxChinese = Math.Max(maxChinese, entry.Chinese.Length);
        }

        // 极长的名字（个别空间站全名）用于滑窗意义不大，压到合理上限
        lock (Sync)
        {
            _byEnglish = byEnglish;
            _byChinese = byChinese;
            _maxEnglishLength = Math.Clamp(maxEnglish, 3, 48);
            _maxChineseLength = Math.Clamp(maxChinese, 2, 20);
            Count = entries.Count;
            _signature = ComputeSignature(entries);
            IsReady = true;
            LastBuildInfo = $"{entries.Count} 条 · {(DateTime.UtcNow - started).TotalMilliseconds:F0}ms";
        }

        RefreshUserTerms();
        Changed?.Invoke(null, EventArgs.Empty);
        return new GlossaryBuildResult(entries.Count, DateTime.UtcNow - started, false);
    }

    /// <summary>把用户术语表并入匹配索引（用户术语始终可用，不依赖 SDE 是否载入成功）。</summary>
    private static void RefreshUserTerms()
    {
        var byEnglish = new Dictionary<string, List<GlossaryEntry>>(StringComparer.OrdinalIgnoreCase);
        var byChinese = new Dictionary<string, List<GlossaryEntry>>(StringComparer.Ordinal);
        var maxEnglish = 0;
        var maxChinese = 0;

        foreach (var entry in UserGlossaryService.ToGlossaryEntries())
        {
            Add(byEnglish, entry.English, entry);
            Add(byChinese, entry.Chinese, entry);
            maxEnglish = Math.Max(maxEnglish, entry.English.Length);
            maxChinese = Math.Max(maxChinese, entry.Chinese.Length);
        }

        lock (Sync)
        {
            _userByEnglish = byEnglish;
            _userByChinese = byChinese;
            _maxEnglishLength = Math.Clamp(Math.Max(_maxEnglishLength, maxEnglish), 3, 48);
            _maxChineseLength = Math.Clamp(Math.Max(_maxChineseLength, maxChinese), 2, 20);
            _userSignatureSeen = UserGlossaryService.Signature;
        }
    }

    /// <summary>
    /// 用户术语一变就重建用户侧索引（用签名比对，命中时零成本）。
    /// 必须在每次匹配前调用：否则用户在设置页新加的术语要等 SDE 术语库重建才会生效（本轮实测踩到的缺陷）。
    /// </summary>
    private static void EnsureUserTermsFresh()
    {
        if (UserGlossaryService.Signature != _userSignatureSeen)
        {
            RefreshUserTerms();
        }
    }

    private static void Add(Dictionary<string, List<GlossaryEntry>> index, string key, GlossaryEntry entry)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var normalized = key.Trim();
        if (!index.TryGetValue(normalized, out var list))
        {
            index[normalized] = list = [];
        }

        list.Add(entry);
    }

    /// <summary>FNV-1a 滚动哈希：只在载入时算一次，用于缓存键（不落盘）。</summary>
    private static string ComputeSignature(List<GlossaryEntry> entries)
    {
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            foreach (var entry in entries)
            {
                hash = (hash ^ (uint)entry.Id) * 1099511628211UL;
                foreach (var ch in entry.English)
                {
                    hash = (hash ^ ch) * 1099511628211UL;
                }

                foreach (var ch in entry.Chinese)
                {
                    hash = (hash ^ ch) * 1099511628211UL;
                }
            }

            return $"{entries.Count:x}-{hash:x}";
        }
    }

    /// <summary>
    /// 按原文命中术语，返回按分数降序、且**互不重叠**的前 <paramref name="limit"/> 条。
    /// </summary>
    public static IReadOnlyList<GlossaryHit> Match(string? text, int limit)
    {
        EnsureUserTermsFresh();

        // 用户术语表不依赖 SDE：即使数据库没载入成功（IsReady=false），用户术语照样能命中
        if (string.IsNullOrWhiteSpace(text) || limit <= 0 || !IsReady && _userByEnglish.Count == 0)
        {
            return [];
        }

        var scan = text.Length > MaxScanLength ? text[..MaxScanLength] : text;
        var candidates = new List<GlossaryHit>();

        MatchEnglish(scan, candidates);
        MatchChinese(scan, candidates);

        return candidates
            .OrderByDescending(p => p.Score)
            .ThenBy(p => p.Start)
            .Aggregate(new List<GlossaryHit>(), (kept, hit) =>
            {
                if (kept.Count < limit && !kept.Any(k => Overlaps(k, hit)))
                {
                    kept.Add(hit);
                }

                return kept;
            });
    }

    /// <summary>
    /// 该英文词/短语是否已在术语库里（SDE 或用户术语）。
    /// 供"AI 新词候选"判断要不要记候选。
    /// </summary>
    public static bool IsKnownEnglish(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return false;
        }

        EnsureUserTermsFresh();
        var trimmed = term.Trim();
        return _byEnglish.ContainsKey(trimmed) || _userByEnglish.ContainsKey(trimmed);
    }

    private static bool Overlaps(GlossaryHit a, GlossaryHit b)
        => a.Start < b.Start + b.Length && b.Start < a.Start + a.Length;

    private static void MatchEnglish(string text, List<GlossaryHit> candidates)
    {
        var words = EnglishWordRegex.Matches(text);
        for (var i = 0; i < words.Count; i++)
        {
            for (var count = 1; count <= MaxEnglishWords && i + count <= words.Count; count++)
            {
                var start = words[i].Index;
                var end = words[i + count - 1].Index + words[i + count - 1].Length;
                var length = end - start;
                if (length > _maxEnglishLength)
                {
                    break;
                }

                var candidate = text.Substring(start, length);
                // 先用户术语后 SDE：同词条时用户术语分数更高，去重阶段自然胜出
                AddHits(_userByEnglish, candidate, start, length, text, candidates);
                AddHits(_byEnglish, candidate, start, length, text, candidates);
            }
        }
    }

    private static void AddHits(
        Dictionary<string, List<GlossaryEntry>> index,
        string candidate,
        int start,
        int length,
        string text,
        List<GlossaryHit> candidates)
    {
        if (index.TryGetValue(candidate, out var entries))
        {
            foreach (var entry in entries)
            {
                candidates.Add(new GlossaryHit(entry, start, length, Score(entry, candidate, length, text)));
            }
        }
    }

    private static void MatchChinese(string text, List<GlossaryHit> candidates)
    {
        var index = 0;
        while (index < text.Length)
        {
            // 一段"词字符"里只要含汉字就按中文处理：这样 "小裂谷2"、"10MN加力燃烧器" 这类
            // 中英/数字混排的译名（SDE 与用户术语里都常见）也能命中（此前只认纯 CJK 段，会漏掉它们）
            if (!IsWordChar(text[index]))
            {
                index++;
                continue;
            }

            var runEnd = index;
            var hasCjk = false;
            while (runEnd < text.Length && IsWordChar(text[runEnd]))
            {
                hasCjk |= IsCjk(text[runEnd]);
                runEnd++;
            }

            if (!hasCjk)
            {
                index = runEnd;
                continue;
            }

            for (var start = index; start < runEnd; start++)
            {
                var maxLength = Math.Min(_maxChineseLength, runEnd - start);
                for (var length = 2; length <= maxLength; length++)
                {
                    var candidate = text.Substring(start, length);
                    if (!ContainsCjk(candidate))
                    {
                        continue;
                    }

                    AddHits(_userByChinese, candidate, start, length, text, candidates);
                    AddHits(_byChinese, candidate, start, length, text, candidates);
                }
            }

            index = runEnd;
        }
    }

    private static bool IsWordChar(char ch) => IsCjk(ch) || char.IsLetterOrDigit(ch);

    private static bool ContainsCjk(string text)
    {
        foreach (var ch in text)
        {
            if (IsCjk(ch))
            {
                return true;
            }
        }

        return false;
    }

    private static int Score(GlossaryEntry entry, string matched, int length, string text)
    {
        var score = length * 10;

        // 用户术语表优先级最高：同名词条必须赢过 SDE（用户明确指定过译名）
        if (entry.Id < 0)
        {
            score += 2000;
        }

        // 整段输入就是这个术语（最常见：只翻译一个名词）→ 大幅加权
        if (string.Equals(matched.Trim(), text.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            score += 1000;
        }

        if (entry.IsMarketItem)
        {
            score += 40;
        }

        score += entry.Kind switch
        {
            DataBaseItemType.MapSolarSystem => 30,
            DataBaseItemType.MapRegion => 30,
            DataBaseItemType.InvType => 20,
            _ => 0,
        };

        // 代理人舰船这类"某某人的某船"噪声降权
        if (entry.Kind == DataBaseItemType.InvType && !entry.IsMarketItem && entry.English.Contains("'s ", StringComparison.Ordinal))
        {
            score -= 200;
        }

        return score;
    }

    private static bool IsCjk(char ch) => ch is >= '\u4e00' and <= '\u9fff';

    /// <summary>
    /// 生成注入提示词的术语表块。带上类别（舰船/星系…）能让模型正确选义；
    /// 只输出英文 = 中文（或反向），避免模型把解释当成译文。
    /// </summary>
    public static string BuildPromptBlock(IReadOnlyList<GlossaryHit> hits, bool sourceIsChinese)
    {
        if (hits.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var hit in hits)
        {
            var kind = KindLabel(hit.Entry.Kind);
            builder.Append(sourceIsChinese
                ? $"{hit.Entry.Chinese} [{kind}] = {hit.Entry.English}\n"
                : $"{hit.Entry.English} [{kind}] = {hit.Entry.Chinese}\n");
        }

        return builder.ToString().TrimEnd();
    }

    private static string KindLabel(DataBaseItemType kind) => kind switch
    {
        DataBaseItemType.InvType => "物品/舰船/装备",
        DataBaseItemType.MapSolarSystem => "星系",
        DataBaseItemType.MapRegion => "星域",
        DataBaseItemType.StaStation => "空间站",
        _ => "名词",
    };

    /// <summary>术语表条目的展示文本（界面"术语命中"用）。</summary>
    public static string DescribeHit(GlossaryHit hit) => $"{hit.Entry.English} = {hit.Entry.Chinese}";
}

/// <summary>术语库构建结果。</summary>
/// <param name="Count">术语条数。</param>
/// <param name="Elapsed">耗时。</param>
/// <param name="FromCache">是否来自缓存（当前实现恒为 false：每次进程重新抽取以避免与 SDE 更新脱节）。</param>
public sealed record GlossaryBuildResult(int Count, TimeSpan Elapsed, bool FromCache);
