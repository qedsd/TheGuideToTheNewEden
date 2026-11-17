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
/// 「攻略 · 死亡远征」页 VM。
/// <para>
/// 数据来自 Core 的 <see cref="CombatSiteService"/>（<c>Resources/Database/static.db</c> 的 <c>CombatSite</c> 表，
/// 379 条：DED 评级 50 / 无评级 74 / 远征 76 / 战斗异常 126 / 化学实验室 34 / 战斗遗迹 6 / 三神裔 9 / 特殊 4）。
/// </para>
/// <para>
/// 相比旧版（只有"势力 + 名称搜索"）多了**分类**与**难度**两个维度：
/// 补进 126 条战斗异常以后，光靠势力已经筛不动了。难度下拉的内容随分类变——
/// 选"DED 评级复合体"给 1/10–10/10，选"战斗异常"给 T1–T10，选"无评级复合体"给"未定级 / 9/10"。
/// </para>
/// <para>
/// 右侧正文是**双轨**：<c>社区攻略</c>（本站整理的中文实战）与 <c>Wiki 数据</c>（EVE University Wiki 抓取并翻译）。
/// 只有一侧有内容时不显示页签栏，直接铺满（多数条目只有一侧）。
/// </para>
/// </summary>
public sealed class DEDPageViewModel : INotifyPropertyChanged
{
    private const int SearchDebounceMs = 350;

    private readonly ObservableCollection<CombatSite> _items = new();

    private CombatSite? _selected;
    private FilterOption? _selectedCategory;
    private FilterOption? _selectedFaction;
    private FilterOption? _selectedLevel;
    private string _searchText = string.Empty;
    private bool _isLoading;
    private bool _loadFailed;
    private bool _isSearchResult;
    private string _error = string.Empty;
    private bool _initialized;
    private int _contentTabIndex;

    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _loadCts;

    public DEDPageViewModel()
    {
        LanguageService.LanguageChanged += OnLanguageChanged;
        RebuildCategoryOptions();
        RebuildFactionOptions();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CombatSite> Items => _items;

    /// <summary>分类下拉（第一项是"全部"）。</summary>
    public ObservableCollection<FilterOption> CategoryOptions { get; } = new();

    /// <summary>势力下拉（第一项是"不限"）。</summary>
    public ObservableCollection<FilterOption> FactionOptions { get; } = new();

    /// <summary>难度下拉：内容随分类重建；分类不适用时整条隐藏。</summary>
    public ObservableCollection<FilterOption> LevelOptions { get; } = new();

    public FilterOption? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (Set(ref _selectedCategory, value))
            {
                RebuildLevelOptions();
                _ = ReloadAsync();
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
                _ = ReloadAsync();
            }
        }
    }

    public FilterOption? SelectedLevel
    {
        get => _selectedLevel;
        set
        {
            if (Set(ref _selectedLevel, value))
            {
                _ = ReloadAsync();
            }
        }
    }

    /// <summary>难度下拉是否可见（远征/实验室/遗迹/三神裔/特殊没有"等级"概念）。</summary>
    public bool IsLevelFilterVisible => LevelOptions.Count > 0;

    public CombatSite? Selected
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, value))
            {
                ContentTabIndex = HasCommunity ? 0 : (HasWiki ? 1 : 0);
                RaiseSelectionDependents();
            }
        }
    }

    public bool HasSelection => _selected is not null;

    /// <summary>该条目是否有本站中文攻略。</summary>
    public bool HasCommunity => !string.IsNullOrWhiteSpace(_selected?.CommunityContent);

    /// <summary>该条目是否有 wiki 中文正文。</summary>
    public bool HasWiki => !string.IsNullOrWhiteSpace(_selected?.WikiContent);

    /// <summary>两侧都有内容时才显示页签栏。</summary>
    public bool ShowTabStrip => HasCommunity && HasWiki;

    /// <summary>两侧都没内容（wiki 页缺失或本身是 stub）。</summary>
    public bool ShowNoContent => _selected is not null && !HasCommunity && !HasWiki;

    /// <summary>0 = 社区攻略，1 = Wiki 数据。</summary>
    public int ContentTabIndex
    {
        get => _contentTabIndex;
        set
        {
            if (Set(ref _contentTabIndex, value))
            {
                OnPropertyChanged(nameof(EffectiveContent));
            }
        }
    }

    /// <summary>
    /// 只有一侧有内容时（不显示页签栏）实际要铺满的那份正文；
    /// 两侧都有内容时由页签各自绑定，这个属性用不到。
    /// </summary>
    public string EffectiveContent => HasCommunity ? _selected?.CommunityContent ?? string.Empty
                                                   : _selected?.WikiContent ?? string.Empty;

    /// <summary>右侧元信息行：类别 · 势力 · 评级/档位 · 最大船型 · 出现空间。</summary>
    public string MetaText
    {
        get
        {
            if (_selected is null)
            {
                return string.Empty;
            }

            var bits = new List<string> { CombatSiteCategoryConverter.NameOf(_selected.Category) };

            if (_selected.Faction >= 0)
            {
                bits.Add(FindString(FactionKeyOf(_selected.Faction)));
            }

            switch ((CombatSiteCategory)_selected.Category)
            {
                case CombatSiteCategory.DedRated:
                    bits.Add($"{_selected.Rating}/10");
                    break;
                case CombatSiteCategory.Unrated when _selected.Rating > 0:
                    // 舰队集结点：CONCORD 没有评级，但玩家习惯叫 9/10，这里照习惯显示。
                    bits.Add($"{_selected.Rating}/10");
                    break;
                case CombatSiteCategory.Anomaly:
                    bits.Add(_selected.Level > 0 ? $"T{_selected.Tier}·{_selected.Level}" : $"T{_selected.Tier}");
                    break;
            }

            if (!string.IsNullOrWhiteSpace(_selected.ShipLimit))
            {
                bits.Add($"{FindString("DEDPage_MetaShipLimit")} {_selected.ShipLimit}");
            }

            if (!string.IsNullOrWhiteSpace(_selected.Location))
            {
                bits.Add(_selected.Location);
            }

            return string.Join(" · ", bits);
        }
    }

    /// <summary>"可升级为 / 触发来源"一行（没有就空串，界面据 <see cref="HasRelation"/> 隐藏）。</summary>
    public string RelationText
    {
        get
        {
            if (_selected is null)
            {
                return string.Empty;
            }

            var bits = new List<string>();
            if (!string.IsNullOrWhiteSpace(_selected.Escalation))
            {
                bits.Add($"{FindString("DEDPage_Escalation")} {_selected.Escalation}");
            }

            if (!string.IsNullOrWhiteSpace(_selected.Source))
            {
                bits.Add($"{FindString("DEDPage_Source")} {_selected.Source}");
            }

            return string.Join("　", bits);
        }
    }

    public bool HasRelation => !string.IsNullOrWhiteSpace(RelationText);

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (Set(ref _searchText, value ?? string.Empty))
            {
                QueueSearch();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => Set(ref _isLoading, value);
    }

    public bool IsEmpty => _items.Count == 0 && !IsLoading;

    public string StatusText
    {
        get
        {
            if (IsLoading)
            {
                return FindString("DEDPage_Loading");
            }

            if (_loadFailed)
            {
                return string.Format(FindString("DEDPage_LoadFailed"), _error);
            }

            return string.Format(FindString(_isSearchResult ? "DEDPage_SearchSummary" : "DEDPage_Summary"), _items.Count);
        }
    }

    public async Task InitAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await ReloadAsync();
    }

    // ---------------------------------------------------------------- 选项
    private void RebuildCategoryOptions()
    {
        var keep = _selectedCategory?.Key ?? int.MinValue;
        CategoryOptions.Clear();
        CategoryOptions.Add(new FilterOption(int.MinValue, FindString("DEDPage_AllCategories")));
        foreach (CombatSiteCategory c in Enum.GetValues(typeof(CombatSiteCategory)))
        {
            CategoryOptions.Add(new FilterOption((int)c, CombatSiteCategoryConverter.NameOf((int)c)));
        }

        _selectedCategory = CategoryOptions.FirstOrDefault(o => o.Key == keep) ?? CategoryOptions[0];
        OnPropertyChanged(nameof(SelectedCategory));
        RebuildLevelOptions();
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

        _selectedFaction = FactionOptions.FirstOrDefault(o => o.Key == keep) ?? FactionOptions[0];
        OnPropertyChanged(nameof(SelectedFaction));
    }

    /// <summary>难度下拉按分类重建：评级给 1–10，异常给 T1–T10，无评级给"未定级 / 9/10"。</summary>
    private void RebuildLevelOptions()
    {
        var keep = _selectedLevel?.Key ?? int.MinValue;
        LevelOptions.Clear();

        var category = _selectedCategory?.Key ?? int.MinValue;
        if (category == (int)CombatSiteCategory.DedRated)
        {
            LevelOptions.Add(new FilterOption(int.MinValue, FindString("DEDPage_AllLevels")));
            for (var r = 1; r <= 10; r++)
            {
                LevelOptions.Add(new FilterOption(r, $"{r}/10"));
            }
        }
        else if (category == (int)CombatSiteCategory.Unrated)
        {
            LevelOptions.Add(new FilterOption(int.MinValue, FindString("DEDPage_AllLevels")));
            LevelOptions.Add(new FilterOption(0, FindString("DEDPage_UnkownLevel")));
            // 舰队集结点在库里刻意记 Rating = 9（玩家习惯叫 9/10），单独给一档。
            LevelOptions.Add(new FilterOption(9, "9/10"));
        }
        else if (category == (int)CombatSiteCategory.Anomaly)
        {
            LevelOptions.Add(new FilterOption(int.MinValue, FindString("DEDPage_AllLevels")));
            for (var t = 1; t <= 10; t++)
            {
                LevelOptions.Add(new FilterOption(t, $"T{t}"));
            }
        }

        _selectedLevel = LevelOptions.FirstOrDefault(o => o.Key == keep) ?? (LevelOptions.Count > 0 ? LevelOptions[0] : null);
        OnPropertyChanged(nameof(SelectedLevel));
        OnPropertyChanged(nameof(IsLevelFilterVisible));
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

    // ---------------------------------------------------------------- 取数
    private void QueueSearch()
    {
        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;
        _ = RunDebouncedSearchAsync(cts);
    }

    private async Task RunDebouncedSearchAsync(CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(SearchDebounceMs, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!cts.IsCancellationRequested)
        {
            await ReloadAsync();
        }
    }

    private async Task ReloadAsync()
    {
        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;

        var keyword = _searchText.Trim();
        var category = _selectedCategory?.Key ?? int.MinValue;
        var faction = _selectedFaction?.Key ?? int.MinValue;
        var level = _selectedLevel?.Key ?? int.MinValue;

        var rating = -1;
        var tier = 0;
        if (category == (int)CombatSiteCategory.DedRated || category == (int)CombatSiteCategory.Unrated)
        {
            rating = level;
        }
        else if (category == (int)CombatSiteCategory.Anomaly)
        {
            tier = level > 0 ? level : 0;
        }

        IsLoading = true;
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsEmpty));

        try
        {
            var result = await CombatSiteService.QueryAsync(
                category == int.MinValue ? CombatSiteService.AnyCategory : category,
                faction == int.MinValue ? CombatSiteService.AnyFaction : faction,
                rating,
                tier,
                keyword.Length > 0 ? keyword : null);

            if (cts.IsCancellationRequested)
            {
                return;
            }

            _isSearchResult = keyword.Length > 0;
            _loadFailed = false;
            _error = string.Empty;

            var previousId = _selected?.Id;
            _items.Clear();
            foreach (var item in result ?? new List<CombatSite>())
            {
                _items.Add(item);
            }

            Selected = _items.FirstOrDefault(x => x.Id == previousId) ?? _items.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);

            if (!cts.IsCancellationRequested)
            {
                _loadFailed = true;
                _error = ex.Message;
                _items.Clear();
                Selected = null;
            }
        }
        finally
        {
            if (!cts.IsCancellationRequested)
            {
                IsLoading = false;
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    // ---------------------------------------------------------------- 通知
    private void RaiseSelectionDependents()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasCommunity));
        OnPropertyChanged(nameof(HasWiki));
        OnPropertyChanged(nameof(ShowTabStrip));
        OnPropertyChanged(nameof(ShowNoContent));
        OnPropertyChanged(nameof(MetaText));
        OnPropertyChanged(nameof(RelationText));
        OnPropertyChanged(nameof(HasRelation));
        OnPropertyChanged(nameof(EffectiveContent));
    }

    private void OnLanguageChanged(object? sender, string language)
    {
        // 下拉项与状态行都是"求值时查资源"的文案，换语言后整体重建（§9 第 63 条）。
        RebuildCategoryOptions();
        RebuildFactionOptions();
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(MetaText));
        OnPropertyChanged(nameof(RelationText));
    }

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

/// <summary>下拉项：<see cref="Key"/> 是取值（<c>int.MinValue</c> 表示"全部/不限"），<see cref="Label"/> 是本地化文案。</summary>
public sealed class FilterOption
{
    public FilterOption(int key, string label)
    {
        Key = key;
        Label = label;
    }

    public int Key { get; }

    public string Label { get; }

    public override string ToString() => Label;
}
