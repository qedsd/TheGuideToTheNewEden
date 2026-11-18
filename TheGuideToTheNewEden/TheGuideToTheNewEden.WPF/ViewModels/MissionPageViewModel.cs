using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TheGuideToTheNewEden.Core.DBModels.Static;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.ViewModels;

/// <summary>
/// 「攻略 · 任务」页 VM。
/// <para>
/// 数据来自 Core 的 <see cref="MissionService"/>（<c>Resources/Database/static.db</c> 的 <c>Mission</c> 表），
/// 字段对齐 wiki 的 <c>{{Missiondetails}}</c>：分类 / 等级 / 类型 / 势力 / 任务目标 / 电子战 / 船型限制 /
/// 推荐船型 / 奖励 / 声望损失 / 备注。正文是渲染好的中文 Markdown（简报 + 分节 + NPC 表）。
/// </para>
/// <para>
/// 与「死亡远征」页同构：左卡搜索 + 列表 + 三个筛选（分类 / 等级 / 势力），右卡标题 + 元信息 + 正文。
/// 老版本只有"按等级列出 + 一段纯文本"，分类和势力都筛不了。
/// </para>
/// </summary>
public sealed class MissionPageViewModel : INotifyPropertyChanged
{
    private const int SearchDebounceMs = 350;

    private readonly ObservableCollection<Mission> _items = new();

    private Mission? _selected;
    private FilterOption? _selectedCategory;
    private FilterOption? _selectedLevel;
    private FilterOption? _selectedFaction;
    private string _searchText = string.Empty;
    private bool _isLoading;
    private bool _loadFailed;
    private bool _isSearchResult;
    private string _error = string.Empty;
    private bool _initialized;

    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _loadCts;

    public MissionPageViewModel()
    {
        LanguageService.LanguageChanged += OnLanguageChanged;
        RebuildCategoryOptions();
        RebuildLevelOptions();
        RebuildFactionOptions();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<Mission> Items => _items;

    /// <summary>分类下拉（第一项"全部分类"）。</summary>
    public ObservableCollection<FilterOption> CategoryOptions { get; } = new();

    /// <summary>等级下拉（不分等级 + L1–L5）。</summary>
    public ObservableCollection<FilterOption> LevelOptions { get; } = new();

    /// <summary>势力下拉（不限 + 6 个海盗势力）。</summary>
    public ObservableCollection<FilterOption> FactionOptions { get; } = new();

    public Mission? Selected
    {
        get => _selected;
        set
        {
            if (!Set(ref _selected, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(MetaText));
            OnPropertyChanged(nameof(HasMeta));
        }
    }

    public bool HasSelection => _selected is not null;

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

    public FilterOption? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (Set(ref _selectedCategory, value))
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

    public bool IsLoading
    {
        get => _isLoading;
        private set => Set(ref _isLoading, value);
    }

    public bool LoadFailed
    {
        get => _loadFailed;
        private set => Set(ref _loadFailed, value);
    }

    public string Error
    {
        get => _error;
        private set => Set(ref _error, value);
    }

    public bool IsEmpty => !_isLoading && _items.Count == 0;

    /// <summary>右侧元信息：分类 · 等级 · 类型 · 势力（· 任务线）。</summary>
    public string MetaText
    {
        get
        {
            if (_selected is null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            var cat = MissionCategoryName(_selected.Category);
            if (!string.IsNullOrEmpty(cat))
            {
                parts.Add(cat);
            }

            if (_selected.Level > 0)
            {
                parts.Add($"L{_selected.Level}");
            }

            if (!string.IsNullOrWhiteSpace(_selected.MissionType))
            {
                parts.Add(MissionTypeName(_selected.MissionType));
            }

            if (_selected.Faction >= 0)
            {
                parts.Add(FindString(FactionKeyOf(_selected.Faction)));
            }

            if (!string.IsNullOrWhiteSpace(_selected.Arc))
            {
                parts.Add(_selected.Arc);
            }

            return string.Join(" · ", parts);
        }
    }

    public bool HasMeta => !string.IsNullOrEmpty(MetaText);

    public string StatusText => string.Format(
        FindString(_isSearchResult ? "DEDPage_SearchSummary" : "MissionPage_Summary"),
        _items.Count);

    public async Task InitAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await ReloadAsync();
    }

    // ---------------------------------------------------------------- 取数
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

                await Application.Current.Dispatcher.InvokeAsync(() => _ = ReloadAsync());
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

    private async Task ReloadAsync()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        IsLoading = true;
        LoadFailed = false;
        Error = string.Empty;
        // 状态行（"共 N 项"）是求值时读 _items.Count 的，必须在加载开始/结束各通知一次，
        // 否则绑定永远停在初始值 0（DED 页有这两行，任务页之前漏了）
        OnPropertyChanged(nameof(StatusText));
        var previousId = _selected?.Id;

        try
        {
            // 下拉用 int.MinValue 表示"不限"，服务层用的是 AnyCategory(-1) / AnyLevel(0) / AnyFaction(-1)，
            // 直接透传会把哨兵值当成真实条件（结果是 0 行，踩过一次）。
            var category = _selectedCategory is null || _selectedCategory.Key == int.MinValue
                ? MissionService.AnyCategory
                : _selectedCategory.Key;
            var level = _selectedLevel is null || _selectedLevel.Key == int.MinValue
                ? MissionService.AnyLevel
                : _selectedLevel.Key;
            var faction = _selectedFaction is null || _selectedFaction.Key == int.MinValue
                ? MissionService.AnyFaction
                : _selectedFaction.Key;

            var list = await MissionService.QueryAsync(category, level, faction, _searchText);

            if (token.IsCancellationRequested)
            {
                return;
            }

            _items.Clear();
            foreach (var m in list)
            {
                _items.Add(m);
            }

            _isSearchResult = !string.IsNullOrWhiteSpace(_searchText);
            Selected = _items.FirstOrDefault(x => x.Id == previousId) ?? _items.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            LoadFailed = true;
            Error = ex.Message;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    // ---------------------------------------------------------------- 下拉
    private void RebuildCategoryOptions()
    {
        var keep = _selectedCategory?.Key ?? int.MinValue;
        CategoryOptions.Clear();
        CategoryOptions.Add(new FilterOption(int.MinValue, FindString("MissionPage_AllCategories")));
        foreach (var c in Enum.GetValues(typeof(MissionCategory)).Cast<MissionCategory>())
        {
            CategoryOptions.Add(new FilterOption((int)c, MissionCategoryName((int)c)));
        }

        _selectedCategory = CategoryOptions.FirstOrDefault(o => o.Key == keep) ?? CategoryOptions[0];
        OnPropertyChanged(nameof(SelectedCategory));
    }

    private void RebuildLevelOptions()
    {
        var keep = _selectedLevel?.Key ?? int.MinValue;
        LevelOptions.Clear();
        LevelOptions.Add(new FilterOption(int.MinValue, FindString("MissionPage_AllLevels")));
        for (var i = 1; i <= 5; i++)
        {
            LevelOptions.Add(new FilterOption(i, $"L{i}"));
        }

        _selectedLevel = LevelOptions.FirstOrDefault(o => o.Key == keep) ?? LevelOptions[0];
        OnPropertyChanged(nameof(SelectedLevel));
    }

    private void RebuildFactionOptions()
    {
        var keep = _selectedFaction?.Key ?? int.MinValue;
        FactionOptions.Clear();
        FactionOptions.Add(new FilterOption(int.MinValue, FindString("DEDPage_AllFactions")));
        // 0–5 海盗势力，6–9 四大帝国（任务里"针对四大种族"那一类）
        for (var i = 0; i <= 9; i++)
        {
            FactionOptions.Add(new FilterOption(i, FindString(FactionKeyOf(i))));
        }

        _selectedFaction = FactionOptions.FirstOrDefault(o => o.Key == keep) ?? FactionOptions[0];
        OnPropertyChanged(nameof(SelectedFaction));
    }

    // ---------------------------------------------------------------- 文案
    private static string MissionCategoryName(int category)
    {
        var key = CategoryKeyOf(category);
        return key is null ? string.Empty : FindString(key);
    }

    private static string? CategoryKeyOf(int category) => category switch
    {
        (int)MissionCategory.Security => "MissionPage_Cat_Security",
        (int)MissionCategory.Distribution => "MissionPage_Cat_Distribution",
        (int)MissionCategory.Mining => "MissionPage_Cat_Mining",
        (int)MissionCategory.Storyline => "MissionPage_Cat_Storyline",
        (int)MissionCategory.Epic => "MissionPage_Cat_Epic",
        (int)MissionCategory.Anomic => "MissionPage_Cat_Anomic",
        (int)MissionCategory.Cosmos => "MissionPage_Cat_Cosmos",
        (int)MissionCategory.Rnd => "MissionPage_Cat_Rnd",
        (int)MissionCategory.Career => "MissionPage_Cat_Career",
        (int)MissionCategory.Circle => "MissionPage_Cat_Circle",
        _ => null,
    };

    /// <summary>wiki 的 Type 原值 → 本地化文案（未登记的原样返回）。</summary>
    private static string MissionTypeName(string type)
    {
        var t = type.Trim();
        var key = t.ToLowerInvariant() switch
        {
            "encounter" => "MissionPage_Type_Encounter",
            "courier" => "MissionPage_Type_Courier",
            "mining" => "MissionPage_Type_Mining",
            "trade" => "MissionPage_Type_Trade",
            "talk to agent" => "MissionPage_Type_TalkToAgent",
            "agent interaction" => "MissionPage_Type_AgentInteraction",
            "storyline(encounter)" => "MissionPage_Type_StorylineEncounter",
            "storyline(courier)" => "MissionPage_Type_StorylineCourier",
            "storyline(mining)" => "MissionPage_Type_StorylineMining",
            "storyline(trade)" => "MissionPage_Type_StorylineTrade",
            "storyline" => "MissionPage_Type_Storyline",
            _ => null,
        };
        return key is null ? t : FindString(key);
    }

    private static string FactionKeyOf(int faction) => faction switch
    {
        0 => "DEDPage_Drone",
        1 => "DEDPage_Serpentis",
        2 => "DEDPage_BloodRaider",
        3 => "DEDPage_Sansha",
        4 => "DEDPage_Angel",
        5 => "DEDPage_Guristas",
        6 => "MissionPage_Faction_Amarr",
        7 => "MissionPage_Faction_Caldari",
        8 => "MissionPage_Faction_Gallente",
        9 => "MissionPage_Faction_Minmatar",
        _ => "DEDPage_Drone",
    };

    private void OnLanguageChanged(object? sender, string language)
    {
        RebuildCategoryOptions();
        RebuildLevelOptions();
        RebuildFactionOptions();
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(MetaText));
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
