using System.IO;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.Translation;

namespace TheGuideToTheNewEden.WPF.Services.Translation;

/// <summary>一条用户自定义术语（优先级高于 SDE 术语库）。</summary>
public sealed class UserGlossaryTerm
{
    /// <summary>英文名。</summary>
    public string English { get; set; } = string.Empty;

    /// <summary>中文名。</summary>
    public string Chinese { get; set; } = string.Empty;

    /// <summary>是否"固定译名"（要求模型原样使用；默认开）。</summary>
    public bool Pinned { get; set; } = true;

    /// <summary>备注（来源/原因）。</summary>
    public string? Note { get; set; }

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 用户术语表：SDE 术语库覆盖不到的译名（玩家/军团/建筑/自定义简称）由用户手工维护，
/// 持久化到 <c>Configs/UserGlossary.json</c>。
/// <para>
/// 参与翻译的三条路径：① 注入提示词（排名高于 SDE 条目，同名词条以用户为准）；
/// ② 术语后校验（按用户译名做确定性替换）；③ 本地数据库源（直接覆盖 SDE 的译名）。
/// 用户术语变更会改变 <see cref="Signature"/>，从而让 AI 结果缓存自动失效。
/// </para>
/// </summary>
public static class UserGlossaryService
{
    private static readonly string FilePath = Path.Combine(Services.SettingsService.DataPath, "Configs", "UserGlossary.json");
    private static readonly object Sync = new();

    private static List<UserGlossaryTerm> _terms = [];
    private static Dictionary<string, UserGlossaryTerm> _byEnglish = new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, UserGlossaryTerm> _byChinese = new(StringComparer.Ordinal);
    private static string _signature = "u0";
    private static bool _loaded;

    /// <summary>术语变更（增/删/改/载入）后触发。</summary>
    public static event EventHandler? Changed;

    /// <summary>当前术语（快照，按英文名排序）。</summary>
    public static IReadOnlyList<UserGlossaryTerm> Terms
    {
        get
        {
            lock (Sync)
            {
                Load();
                return _terms.ToList();
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
                return _terms.Count;
            }
        }
    }

    /// <summary>内容签名（用户术语参与 AI 结果缓存键）。</summary>
    public static string Signature
    {
        get
        {
            lock (Sync)
            {
                Load();
                return _signature;
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

    /// <summary>新增或更新一条术语（英文名唯一，忽略大小写）。</summary>
    public static bool Add(string english, string chinese, string? note = null, bool pinned = true)
    {
        english = (english ?? string.Empty).Trim();
        chinese = (chinese ?? string.Empty).Trim();
        if (english.Length == 0 || chinese.Length == 0)
        {
            return false;
        }

        lock (Sync)
        {
            Load();
            var existing = _terms.FirstOrDefault(p => string.Equals(p.English, english, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                _terms.Add(new UserGlossaryTerm
                {
                    English = english,
                    Chinese = chinese,
                    Note = note,
                    Pinned = pinned,
                });
            }
            else
            {
                existing.English = english;
                existing.Chinese = chinese;
                existing.Note = note;
                existing.Pinned = pinned;
                existing.UpdatedUtc = DateTime.UtcNow;
            }

            RebuildIndexes();
            Save();
        }

        Changed?.Invoke(null, EventArgs.Empty);
        return true;
    }

    /// <summary>按英文名删除。</summary>
    public static bool Remove(string english)
    {
        if (string.IsNullOrWhiteSpace(english))
        {
            return false;
        }

        lock (Sync)
        {
            Load();
            var removed = _terms.RemoveAll(p => string.Equals(p.English, english.Trim(), StringComparison.OrdinalIgnoreCase)) > 0;
            if (!removed)
            {
                return false;
            }

            RebuildIndexes();
            Save();
        }

        Changed?.Invoke(null, EventArgs.Empty);
        return true;
    }

    public static void Clear()
    {
        lock (Sync)
        {
            Load();
            if (_terms.Count == 0)
            {
                return;
            }

            _terms.Clear();
            RebuildIndexes();
            Save();
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>按英文名查（忽略大小写）。</summary>
    public static UserGlossaryTerm? FindByEnglish(string? english)
    {
        if (string.IsNullOrWhiteSpace(english))
        {
            return null;
        }

        lock (Sync)
        {
            Load();
            return _byEnglish.TryGetValue(english.Trim(), out var term) ? term : null;
        }
    }

    /// <summary>按中文名查。</summary>
    public static UserGlossaryTerm? FindByChinese(string? chinese)
    {
        if (string.IsNullOrWhiteSpace(chinese))
        {
            return null;
        }

        lock (Sync)
        {
            Load();
            return _byChinese.TryGetValue(chinese.Trim(), out var term) ? term : null;
        }
    }

    /// <summary>把用户术语转成术语库条目（供 <see cref="GlossaryService"/> 合并匹配）。</summary>
    public static IEnumerable<GlossaryEntry> ToGlossaryEntries()
    {
        List<UserGlossaryTerm> snapshot;
        lock (Sync)
        {
            Load();
            snapshot = _terms.ToList();
        }

        return snapshot.Select((term, index) => new GlossaryEntry
        {
            // 负数 ID 与 SDE 区分开（界面/调试时一眼能看出是用户术语）
            Id = -1 - index,
            Kind = DataBaseItemType.InvType,
            English = term.English,
            Chinese = term.Chinese,
            IsMarketItem = true,
        });
    }

    /// <summary>
    /// 用用户术语覆盖本地数据库源的译名（例：用户把 <c>Rifter</c> 固定成"小裂谷"，
    /// 则本地库源查 <c>Rifter</c> 时也直接给"小裂谷"）。
    /// </summary>
    public static List<TranslationItem> ApplyOverrides(IEnumerable<TranslationItem> items)
    {
        Dictionary<string, UserGlossaryTerm> byEnglish;
        Dictionary<string, UserGlossaryTerm> byChinese;
        lock (Sync)
        {
            Load();
            // 只取引用：RebuildIndexes 是"整体替换字典"，不会就地改动，因此循环里无需反复加锁
            byEnglish = _byEnglish;
            byChinese = _byChinese;
        }

        var result = new List<TranslationItem>();
        foreach (var item in items)
        {
            if (item.IsFromDataBase)
            {
                var query = (item.Query ?? string.Empty).Trim();
                if (byEnglish.TryGetValue(query, out var englishTerm))
                {
                    item.Translation = englishTerm.Chinese;
                }
                else if (byChinese.TryGetValue(query, out var chineseTerm))
                {
                    item.Translation = chineseTerm.English;
                }
            }

            result.Add(item);
        }

        return result;
    }

    // ---------- 内部 ----------

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
                var json = File.ReadAllText(FilePath);
                _terms = JsonConvert.DeserializeObject<List<UserGlossaryTerm>>(json) ?? [];
            }
            else
            {
                _terms = [];
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            _terms = [];
        }

        // 丢掉空行/非法行，避免界面出现空条目
        _terms = _terms
            .Where(p => !string.IsNullOrWhiteSpace(p.English) && !string.IsNullOrWhiteSpace(p.Chinese))
            .GroupBy(p => p.English.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        RebuildIndexes();
    }

    private static void RebuildIndexes()
    {
        _byEnglish = new Dictionary<string, UserGlossaryTerm>(StringComparer.OrdinalIgnoreCase);
        _byChinese = new Dictionary<string, UserGlossaryTerm>(StringComparer.Ordinal);
        foreach (var term in _terms)
        {
            term.English = term.English.Trim();
            term.Chinese = term.Chinese.Trim();
            _byEnglish[term.English] = term;
            _byChinese[term.Chinese] = term;
        }

        _terms = _terms.OrderBy(p => p.English, StringComparer.OrdinalIgnoreCase).ToList();
        _signature = ComputeSignature(_terms);
    }

    private static string ComputeSignature(List<UserGlossaryTerm> terms)
    {
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            foreach (var term in terms)
            {
                foreach (var ch in term.English)
                {
                    hash = (hash ^ ch) * 1099511628211UL;
                }

                hash = (hash ^ '=') * 1099511628211UL;
                foreach (var ch in term.Chinese)
                {
                    hash = (hash ^ ch) * 1099511628211UL;
                }

                hash = (hash ^ (term.Pinned ? 'p' : 'f')) * 1099511628211UL;
            }

            return $"u{terms.Count:x}-{hash:x}";
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

            File.WriteAllText(FilePath, JsonConvert.SerializeObject(_terms, Formatting.Indented));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }
}
