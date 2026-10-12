using System.IO;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.WPF.Services.Home;

/// <summary>
/// 主页快捷方式的持久化（<c>%LocalAppData%\TheGuideToTheNewEden\Configs\HomeShortcuts.json</c>）。
/// <para>
/// <b>刻意单放一个文件</b>：与 WinUI 版共用的 <c>settings.json</c> 是扁平的"键 → 字符串"字典，
/// 有序列表塞进去既不便手改、又要与 WinUI 版争用同一个键名。本文件是 WPF 专有，WinUI 版不读。
/// </para>
/// <para>
/// 全部操作都只在 UI 线程调用（改动即落盘、落盘后触发 <see cref="Changed"/>）。
/// 加载时会<b>丢弃目录里不存在的 id</b> 并去重（功能改名/下线后不会留下死磁贴），
/// 文件损坏则备份为 <c>.bak</c> 后用推荐组合重建。
/// </para>
/// </summary>
public static class HomeShortcutService
{
    private static readonly object Sync = new();
    private static readonly JsonSerializerSettings JsonOptions = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore,
    };

    private static HomeShortcutFile? _file;

    /// <summary>快捷方式发生任何变化（增删/排序/改尺寸/恢复默认）后触发。</summary>
    public static event Action? Changed;

    public static string FilePath { get; } =
        Path.Combine(SettingsService.DataPath, "Configs", "HomeShortcuts.json");

    /// <summary>当前磁贴列表（顺序即显示顺序）。</summary>
    public static IReadOnlyList<HomeShortcutItem> Items => Current.Items;

    public static int MaxItems => Current.MaxItems;

    public static bool IsFull => Items.Count >= MaxItems;

    // ---------- 显示策略（P1 / P2 / P3） ----------

    /// <summary>钱包卡区间（7 / 30 / 0=全部）。</summary>
    public static int WalletRangeDays
    {
        get => Current.WalletRangeDays;
        set
        {
            var normalized = value is 30 or 0 ? value : 7;
            if (Current.WalletRangeDays == normalized)
            {
                return;
            }

            Current.WalletRangeDays = normalized;
            Commit();
        }
    }

    public static bool ShowWalletCard
    {
        get => Current.ShowWalletCard;
        set => SetFlag(value, v => Current.ShowWalletCard = v, Current.ShowWalletCard);
    }

    public static bool ShowMarketCard
    {
        get => Current.ShowMarketCard;
        set => SetFlag(value, v => Current.ShowMarketCard = v, Current.ShowMarketCard);
    }

    public static bool ShowRecent
    {
        get => Current.ShowRecent;
        set => SetFlag(value, v => Current.ShowRecent = v, Current.ShowRecent);
    }

    public static bool ShowGrouped
    {
        get => Current.ShowGrouped;
        set => SetFlag(value, v => Current.ShowGrouped = v, Current.ShowGrouped);
    }

    /// <summary>最近使用过的功能 id（最新在前）。</summary>
    public static IReadOnlyList<string> Recent => Current.Recent;

    /// <summary>记录一次"使用了某功能"（仅在开关打开时记录）。</summary>
    public static void PushRecent(string id)
    {
        if (!Current.ShowRecent || HomeFeatureCatalog.Find(id) is null)
        {
            return;
        }

        var recent = Current.Recent;
        recent.RemoveAll(p => string.Equals(p, id, StringComparison.OrdinalIgnoreCase));
        recent.Insert(0, id);

        while (recent.Count > HomeShortcutFile.DefaultRecentCapacity)
        {
            recent.RemoveAt(recent.Count - 1);
        }

        Commit();
    }

    public static void ClearRecent()
    {
        if (Current.Recent.Count == 0)
        {
            return;
        }

        Current.Recent.Clear();
        Commit();
    }

    private static void SetFlag(bool value, Action<bool> assign, bool current)
    {
        if (current == value)
        {
            return;
        }

        assign(value);
        Commit();
    }

    /// <summary>该功能是否已固定（**任意参数**；导航右键菜单用）。</summary>
    public static bool Contains(string id)
        => Items.Any(p => SameId(p, id));

    /// <summary>该功能本体（无参数）是否已固定；<paramref name="param"/> 非空则比较参数（添加功能窗口 / 磁贴用）。</summary>
    public static bool ContainsExact(string id, string? param)
        => Items.Any(p => SameId(p, id) && SameParam(p, param));

    /// <summary>固定一个功能（可带参数，如"市场 + 某物品"）；已固定该键或已达上限时返回 false。</summary>
    public static bool Add(string id, string? param = null, string? label = null)
    {
        if (HomeFeatureCatalog.Find(id) is null || ContainsExact(id, param))
        {
            return false;
        }

        var file = Current;
        if (file.Items.Count >= file.MaxItems)
        {
            return false;
        }

        file.Items.Add(new HomeShortcutItem
        {
            Id = id,
            Param = string.IsNullOrWhiteSpace(param) ? null : param.Trim(),
            Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
        });

        Commit();
        return true;
    }

    /// <summary>移除该功能的**全部**磁贴（导航"取消固定"用）。</summary>
    public static bool Remove(string id)
    {
        var file = Current;
        var removed = file.Items.RemoveAll(p => SameId(p, id));
        if (removed == 0)
        {
            return false;
        }

        Commit();
        return true;
    }

    /// <summary>只移除指定的一块磁贴（带参数的磁贴用它，不会误删同功能的其它磁贴）。</summary>
    public static bool RemoveExact(string id, string? param)
    {
        var file = Current;
        var removed = file.Items.RemoveAll(p => SameId(p, id) && SameParam(p, param));
        if (removed == 0)
        {
            return false;
        }

        Commit();
        return true;
    }

    /// <summary>把某块磁贴移到指定下标（不足/越界自动夹取）；下标未变返回 false。</summary>
    public static bool MoveTo(string id, string? param, int newIndex)
    {
        var file = Current;
        var current = file.Items.FindIndex(p => SameId(p, id) && SameParam(p, param));
        if (current < 0)
        {
            return false;
        }

        var target = Math.Clamp(newIndex, 0, file.Items.Count - 1);
        if (target == current)
        {
            return false;
        }

        var item = file.Items[current];
        file.Items.RemoveAt(current);
        file.Items.Insert(target, item);
        Commit();
        return true;
    }

    /// <summary>小 / 宽磁贴切换。</summary>
    public static bool SetSize(string id, string? param, bool wide)
    {
        var item = Current.Items.FirstOrDefault(p => SameId(p, id) && SameParam(p, param));
        if (item is null || item.IsWide == wide)
        {
            return false;
        }

        item.Size = wide ? HomeShortcutItem.WideSize : HomeShortcutItem.SmallSize;
        Commit();
        return true;
    }

    private static bool SameId(HomeShortcutItem item, string? id)
        => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase);

    private static bool SameParam(HomeShortcutItem item, string? param)
        => string.Equals(item.Param ?? string.Empty, param ?? string.Empty, StringComparison.Ordinal);

    /// <summary>整体替换（拖动排序 / 添加功能窗口用）。目录里没有的 id 会被忽略。</summary>
    public static bool ReplaceAll(IEnumerable<HomeShortcutItem> items)
    {
        var normalized = Normalize(items);
        if (normalized.Count == 0 && items.Any())
        {
            return false;
        }

        Current.Items = normalized;
        Commit();
        return true;
    }

    /// <summary>恢复推荐组合。</summary>
    public static void ResetToDefault()
    {
        Current.Items = HomeFeatureCatalog.DefaultSeed
            .Select(id => new HomeShortcutItem { Id = id })
            .ToList();
        Commit();
    }

    /// <summary>丢弃内存态并从磁盘重读（外部改了文件时用）。</summary>
    public static void Reload()
    {
        lock (Sync)
        {
            _file = null;
        }

        var _ = Current; // 触发重新加载
        Changed?.Invoke();
    }

    // ---------- 内部 ----------

    private static HomeShortcutFile Current
    {
        get
        {
            lock (Sync)
            {
                return _file ??= Load();
            }
        }
    }

    private static HomeShortcutFile Load()
    {
        HomeShortcutFile? file = null;

        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    file = JsonConvert.DeserializeObject<HomeShortcutFile>(json, JsonOptions);
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error($"[主页] 快捷方式文件解析失败，将用推荐组合重建：{ex.Message}");
            BackupBrokenFile();
            file = null;
        }

        // 首次运行（或文件损坏）：写入推荐组合。
        if (file is null)
        {
            file = new HomeShortcutFile
            {
                Items = HomeFeatureCatalog.DefaultSeed.Select(id => new HomeShortcutItem { Id = id }).ToList(),
            };
            Save(file);
            return file;
        }

        file.MaxItems = Math.Clamp(
            file.MaxItems <= 0 ? HomeShortcutFile.DefaultMaxItems : file.MaxItems, 1, 48);

        var normalized = Normalize(file.Items);
        if (normalized.Count != file.Items.Count
            || !normalized.Select(p => p.Id).SequenceEqual(file.Items.Select(p => p.Id), StringComparer.Ordinal))
        {
            // 清掉了未知 / 重复 / 空 id —— 写回一次，避免每次启动重复清理。
            Core.Log.Warn($"[主页] 快捷方式已清理：{file.Items.Count} → {normalized.Count} 项（功能下线或文件手改）。");
            file.Items = normalized;
            Save(file);
        }
        else
        {
            file.Items = normalized;
        }

        return file;
    }

    /// <summary>丢弃未知 / 空 / 重复的 id（保留首次出现的顺序）；同一功能的多个参数磁贴视为不同项。</summary>
    private static List<HomeShortcutItem> Normalize(IEnumerable<HomeShortcutItem>? items)
    {
        var result = new List<HomeShortcutItem>();
        if (items is null)
        {
            return result;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Id) || HomeFeatureCatalog.Find(item.Id) is null)
            {
                continue;
            }

            var key = item.Key;
            if (!seen.Add(key))
            {
                continue;
            }

            result.Add(new HomeShortcutItem
            {
                Id = item.Id,
                Size = item.Size,
                Badge = item.Badge,
                Param = item.Param,
                Label = item.Label,
            });
        }

        return result;
    }

    private static void Commit()
    {
        Save(Current);
        Changed?.Invoke();
    }

    private static void Save(HomeShortcutFile file)
    {
        try
        {
            var folder = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            // 先写临时文件再替换，避免写到一半崩溃留下半个文件。
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(file, JsonOptions));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Core.Log.Error($"[主页] 快捷方式落盘失败：{ex.Message}");
        }
    }

    private static void BackupBrokenFile()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                File.Copy(FilePath, FilePath + ".bak", overwrite: true);
            }
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"[主页] 备份损坏的快捷方式文件失败：{ex.Message}");
        }
    }
}
