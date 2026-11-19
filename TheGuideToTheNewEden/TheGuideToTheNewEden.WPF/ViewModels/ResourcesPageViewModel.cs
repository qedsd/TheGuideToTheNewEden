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
/// 「攻略 · 资源」页 VM：左侧列表 + 筛选，右侧正文——与「死亡远征」页同一套阅读器结构。
/// <para>
/// 数据来自 `CombatSite` 里**非战斗**的那几档：战斗遗迹(5) / 数据站点(10) / 矿石站点(11) /
/// 气云站点(12) / 冰带(13)。虫洞站点(14) 不在这里，它并进了「战斗异常」页的列表。
/// </para>
/// </summary>
public sealed class ResourcesPageViewModel : INotifyPropertyChanged
{
    private const int SearchDebounceMs = 300;

    /// <summary>本页包含的站点分类。</summary>
    private static readonly int[] Categories =
    {
        (int)CombatSiteCategory.RelicSite,
        (int)CombatSiteCategory.DataSite,
        (int)CombatSiteCategory.OreSite,
        (int)CombatSiteCategory.GasSite,
        (int)CombatSiteCategory.IceBelt,
    };

    private readonly ObservableCollection<CombatSite> _items = new();
    private readonly List<CombatSite> _all = new();

    private CombatSite? _selected;
    private string _searchText = string.Empty;
    private FilterOption? _selectedCategory;
    private bool _isLoading;
    private bool _isSearchResult;
    private bool _initialized;
    private CancellationTokenSource? _searchCts;

    public ResourcesPageViewModel()
    {
        LanguageService.LanguageChanged += OnLanguageChanged;
        RebuildCategoryOptions();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CombatSite> Items => _items;

    public ObservableCollection<FilterOption> CategoryOptions { get; } = new();

    public CombatSite? Selected
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(MetaText));
                OnPropertyChanged(nameof(HasMeta));
            }
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
                ApplyFilter();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (Set(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    public bool IsEmpty => !_isLoading && _items.Count == 0;

    /// <summary>右侧元信息：分类 ｜ 区域 ｜ 势力。</summary>
    public string MetaText
    {
        get
        {
            if (_selected is null)
            {
                return string.Empty;
            }

            var bits = new List<string>
            {
                CombatSiteCategoryConverter.NameOf(_selected.Category),
            };
            if (!string.IsNullOrWhiteSpace(_selected.Location))
            {
                bits.Add(_selected.Location!);
            }

            if (!string.IsNullOrWhiteSpace(_selected.ShipLimit))
            {
                bits.Add(_selected.ShipLimit!);
            }

            return string.Join(" ｜ ", bits.Where(b => !string.IsNullOrWhiteSpace(b)));
        }
    }

    public bool HasMeta => !string.IsNullOrWhiteSpace(MetaText);

    public string StatusText => string.Format(
        FindString(_isSearchResult ? "ResourcesPage_SearchSummary" : "ResourcesPage_Summary"),
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
            var all = await CombatSiteService.QueryAsync();
            _all.Clear();
            foreach (var site in all)
            {
                if (Categories.Contains(site.Category))
                {
                    _all.Add(site);
                }
            }
        }
        finally
        {
            IsLoading = false;
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var category = _selectedCategory?.Key ?? int.MinValue;
        var keyword = _searchText.Trim();

        IEnumerable<CombatSite> query = _all;
        if (category != int.MinValue)
        {
            query = query.Where(s => s.Category == category);
        }

        if (keyword.Length > 0)
        {
            query = query.Where(s =>
                (s.TitleCN?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                || (s.TitleEN?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var list = query.OrderBy(s => s.Category).ThenBy(s => s.TitleCN, StringComparer.Ordinal).ToList();

        var previousId = _selected?.Id;
        _items.Clear();
        foreach (var site in list)
        {
            _items.Add(site);
        }

        Selected = _items.FirstOrDefault(s => s.Id == previousId) ?? _items.FirstOrDefault();
        _isSearchResult = keyword.Length > 0;
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(StatusText));
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

    private void RebuildCategoryOptions()
    {
        var keep = _selectedCategory?.Key ?? int.MinValue;
        CategoryOptions.Clear();
        CategoryOptions.Add(new FilterOption(int.MinValue, FindString("ResourcesPage_AllCategories")));
        foreach (var code in Categories)
        {
            CategoryOptions.Add(new FilterOption(code, CombatSiteCategoryConverter.NameOf(code)));
        }

        _selectedCategory = CategoryOptions.FirstOrDefault(o => o.Key == keep) ?? CategoryOptions[0];
        OnPropertyChanged(nameof(SelectedCategory));
    }

    private void OnLanguageChanged(object? sender, string language)
    {
        RebuildCategoryOptions();
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
