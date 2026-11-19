using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Converters;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.ViewModels;

/// <summary>
/// 「攻略 · 异常」页 VM：**一张全宽表格**列出 126 条战斗异常，以及它升级成的站点。
/// <para>
/// 与「死亡远征」页刻意不同：那个是"左列表 + 右正文"的阅读器，这个是一眼看全的**对照表**。
/// 筛选条件放在卡片**右上角**（搜索框 + 势力 + 档位），筛完的结果整张表铺满。
/// </para>
/// <para>
/// 目标 Id 来自 <see cref="CombatSite.EscalationId"/>（构建期按 wiki 页名写死），
/// 不在运行时解析中文名——中文名会被 SDE 纠名改掉，按名字反查会悄悄失效。
/// </para>
/// </summary>
public sealed class AnomalyPageViewModel : INotifyPropertyChanged
{
    private const int SearchDebounceMs = 350;

    /// <summary>「冬眠者」筛选项的哨兵值（不与其他势力 0–5 冲突）。</summary>
    private const int SleeperFilterKey = -2;

    private readonly ObservableCollection<AnomalyRow> _items = new();
    private readonly List<CombatSite> _all = new();
    private readonly Dictionary<int, CombatSite> _byId = new();

    private FilterOption? _selectedFaction;
    private FilterOption? _selectedTier;
    private string _searchText = string.Empty;
    private bool _isLoading;
    private bool _isSearchResult;
    private bool _initialized;

    private CancellationTokenSource? _searchCts;

    public AnomalyPageViewModel()
    {
        LanguageService.LanguageChanged += OnLanguageChanged;
        RebuildFactionOptions();
        RebuildTierOptions();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>表格数据源（已经过筛选与排序）。</summary>
    public ObservableCollection<AnomalyRow> Items => _items;

    /// <summary>势力筛选（第一项是"不限"）。</summary>
    public ObservableCollection<FilterOption> FactionOptions { get; } = new();

    /// <summary>档位筛选（T1–T10，第一项是"不限"）。</summary>
    public ObservableCollection<FilterOption> TierOptions { get; } = new();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (Set(ref _searchText, value))
            {
                QueueSearch();
            }
        }
    }

    public FilterOption? SelectedFaction
    {
        get => _selectedFaction;
        set
        {
            if (Set(ref _selectedFaction, value))
            {
                ApplyFilter();
            }
        }
    }

    public FilterOption? SelectedTier
    {
        get => _selectedTier;
        set
        {
            if (Set(ref _selectedTier, value))
            {
                ApplyFilter();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => Set(ref _isLoading, value);
    }

    public bool IsEmpty => !_isLoading && _items.Count == 0;

    public string StatusText => string.Format(
        FindString(_isSearchResult ? "DEDPage_SearchSummary" : "AnomalyPage_Summary"),
        _items.Count);

    public async Task InitAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        IsLoading = true;
        try
        {
            // 一次取全量：126 条异常要显示，另外 265 条用来把"可升级为"解析成可点目标。
            var all = await CombatSiteService.QueryAsync();
            _all.Clear();
            _byId.Clear();
            foreach (var site in all)
            {
                _all.Add(site);
                _byId[site.Id] = site;
            }
        }
        finally
        {
            IsLoading = false;
        }

        ApplyFilter();
    }

    /// <summary>点异常自己的名字（或双击整行）→ 打开这个异常自己的详细页。</summary>
    public static void OpenSelf(AnomalyRow? row)
    {
        if (row is not null && row.Id > 0)
        {
            Navigation.NavigateToCombatSite(row.Id);
        }
    }

    /// <summary>点「远征」列 → 打开它升级成的那个站点。</summary>
    public static void OpenTarget(AnomalyRow? row)
    {
        if (row is not null && row.TargetId > 0)
        {
            Navigation.NavigateToCombatSite(row.TargetId);
        }
    }

    private void ApplyFilter()
    {
        var faction = _selectedFaction?.Key ?? int.MinValue;
        var tier = _selectedTier?.Key ?? int.MinValue;
        var keyword = _searchText.Trim();

        // 虫洞站点(14) 也在这张表里：分类显示"冬眠者"、出现空间显示"虫洞"
        IEnumerable<CombatSite> query = _all.Where(s =>
            s.Category == (int)CombatSiteCategory.Anomaly
            || s.Category == (int)CombatSiteCategory.WormholeSite);

        if (faction == SleeperFilterKey)
        {
            // 「冬眠者」= 虫洞站点（CombatSite.Faction 是 -1，没有具体海盗势力）
            query = query.Where(s => s.Category == (int)CombatSiteCategory.WormholeSite);
        }
        else if (faction != int.MinValue)
        {
            query = query.Where(s => s.Faction == faction);
        }

        if (tier != int.MinValue)
        {
            query = query.Where(s => s.Tier == tier);
        }

        if (keyword.Length > 0)
        {
            query = query.Where(s =>
                (s.TitleCN?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                || (s.TitleEN?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var list = query.OrderBy(s => s.Category).ThenBy(s => s.Tier).ThenBy(s => s.Level)
                        .ThenBy(s => s.Faction).ThenBy(s => s.TitleCN, StringComparer.Ordinal).ToList();

        _items.Clear();
        foreach (var site in list)
        {
            var target = site.EscalationId > 0 && _byId.TryGetValue(site.EscalationId, out var hit) ? hit : null;
            var isWormhole = site.Category == (int)CombatSiteCategory.WormholeSite;

            _items.Add(new AnomalyRow(
                site.Id,
                CombatSiteNameConverter.Strip(site.TitleCN) ?? string.Empty,
                site.TitleEN,
                site.Level > 0 ? $"T{site.Tier}·{site.Level}" : (site.Tier > 0 ? $"T{site.Tier}" : string.Empty),
                isWormhole
                    ? FindString("AnomalyPage_FactionSleeper")
                    : (site.Faction >= 0 ? FindString(FactionKeyOf(site.Faction)) : string.Empty),
                target is null ? site.Escalation ?? string.Empty : DescribeTarget(target),
                target?.Id ?? 0,
                isWormhole ? FindString("AnomalyPage_SpaceWormhole") : site.Location ?? string.Empty));
        }

        _isSearchResult = keyword.Length > 0;
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(StatusText));
    }

    /// <summary>「可升级为」单元格的文案：中文名 + 徽标（`沙沙司令部（3/10）`）。</summary>
    private static string DescribeTarget(CombatSite target)
    {
        var tag = CombatSiteTagConverter.TagOf(target);
        if (string.IsNullOrEmpty(tag))
        {
            tag = CombatSiteCategoryConverter.NameOf(target.Category);
        }

        return string.IsNullOrEmpty(tag) ? target.TitleCN : $"{target.TitleCN}（{tag}）";
    }

    private void QueueSearch()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SearchDebounceMs, token);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                await Application.Current.Dispatcher.InvokeAsync(ApplyFilter);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        }, token);
    }

    private void RebuildFactionOptions()
    {
        var keep = _selectedFaction?.Key ?? int.MinValue;
        FactionOptions.Clear();
        FactionOptions.Add(new FilterOption(int.MinValue, FindString("DEDPage_AllFactions")));
        for (var i = 0; i <= 5; i++)
        {
            FactionOptions.Add(new FilterOption(i, FindString(FactionKeyOf(i))));
        }

        // 虫洞站点没有海盗势力，单列一项「冬眠者」
        FactionOptions.Add(new FilterOption(SleeperFilterKey, FindString("AnomalyPage_FactionSleeper")));

        _selectedFaction = FactionOptions.FirstOrDefault(o => o.Key == keep) ?? FactionOptions[0];
        OnPropertyChanged(nameof(SelectedFaction));
    }

    private void RebuildTierOptions()
    {
        var keep = _selectedTier?.Key ?? int.MinValue;
        TierOptions.Clear();
        TierOptions.Add(new FilterOption(int.MinValue, FindString("DEDPage_AllLevels")));
        for (var i = 1; i <= 10; i++)
        {
            TierOptions.Add(new FilterOption(i, $"T{i}"));
        }

        _selectedTier = TierOptions.FirstOrDefault(o => o.Key == keep) ?? TierOptions[0];
        OnPropertyChanged(nameof(SelectedTier));
    }

    private void OnLanguageChanged(object? sender, string language)
    {
        // 下拉项与表格里的势力/目标文案都是"求值时查资源"，换语言后整表重建（§9 第 63 条）。
        RebuildFactionOptions();
        RebuildTierOptions();
        ApplyFilter();
        OnPropertyChanged(nameof(StatusText));
    }

    private static string FactionKeyOf(int faction) => faction switch
    {
        0 => "DEDPage_Drone",
        1 => "DEDPage_Serpentis",
        2 => "DEDPage_BloodRaider",
        3 => "DEDPage_Sansha",
        4 => "DEDPage_Angel",
        5 => "DEDPage_Guristas",
        _ => "DEDPage_Drone",
    };

    private static string FindString(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>对照表的一行：一条战斗异常 + 它能升级成的站点。</summary>
public sealed class AnomalyRow
{
    public AnomalyRow(
        int id,
        string titleCn,
        string titleEn,
        string level,
        string faction,
        string targetText,
        int targetId,
        string location)
    {
        Id = id;
        TitleCN = titleCn;
        TitleEN = titleEn;
        Level = level;
        Faction = faction;
        TargetText = targetText;
        TargetId = targetId;
        Location = location;
    }

    public int Id { get; }

    public string TitleCN { get; }

    public string TitleEN { get; }

    /// <summary>难度档：`T8·3`。</summary>
    public string Level { get; }

    public string Faction { get; }

    /// <summary>「可升级为」的显示文案；没有目标时是 wiki 原文（`8/10`、`?`）。</summary>
    public string TargetText { get; }

    /// <summary>&gt; 0 表示可以点过去（对应 <c>CombatSite.Id</c>）。</summary>
    public int TargetId { get; }

    public bool HasTarget => TargetId > 0;

    public string Location { get; }
}
