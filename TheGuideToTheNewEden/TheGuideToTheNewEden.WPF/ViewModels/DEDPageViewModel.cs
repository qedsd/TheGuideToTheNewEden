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
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.ViewModels;

/// <summary>
/// DED 页 VM：左侧按"海盗势力"筛选 + 名称搜索的列表，右侧是选中项的攻略正文（Markdown）。
/// 对应 WinUI 版 <c>DEDViewModel</c> + <c>DEDPage</c> 的代码后置（WinUI 把搜索放在 AutoSuggestBox 的事件里，
/// WPF 侧统一收到 VM，页面只做绑定与转发）。
/// <para>
/// 数据来自 Core 的 <c>DEDService</c>（静态库 <c>Resources/Database/ded.db</c>，由 <c>CoreInitializer</c>
/// 通过 <c>Config.DEDDBPath</c> 初始化），**不新增服务层**——Core 就是这一层的数据访问。
/// </para>
/// </summary>
public sealed class DEDPageViewModel : INotifyPropertyChanged
{
    /// <summary>搜索防抖（库不大，但逐字符查库没有意义）。</summary>
    private const int SearchDebounceMs = 350;

    private readonly ObservableCollection<DED> _items = new();

    private DED? _selected;
    private int _selectedTypeIndex;
    private string _searchText = string.Empty;
    private bool _isLoading;
    private bool _loadFailed;
    private bool _isSearchResult;
    private string _error = string.Empty;
    private bool _initialized;

    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _loadCts;

    public DEDPageViewModel()
    {
        LanguageService.LanguageChanged += OnLanguageChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>当前列表（按势力筛选的结果，或搜索结果）。</summary>
    public ObservableCollection<DED> Items => _items;

    /// <summary>当前选中的 DED（右侧正文跟着它变）。</summary>
    public DED? Selected
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(ShowNoContent));
            }
        }
    }

    public bool HasSelection => _selected is not null;

    /// <summary>
    /// "该 DED 空间暂无攻略正文"的显隐。库里确实有正文为空的条目（探针实测 22 条无人机 DED 里有 1 条标题也不全），
    /// 没有这句提示的话右侧会是一片空白，看起来像加载失败。
    /// </summary>
    public bool ShowNoContent => _selected is not null && string.IsNullOrWhiteSpace(_selected.Content);

    /// <summary>势力筛选（0 无人机 / 1 天蛇 / 2 血袭者 / 3 萨沙 / 4 天使 / 5 古斯塔斯）。</summary>
    public int SelectedTypeIndex
    {
        get => _selectedTypeIndex;
        set
        {
            if (Set(ref _selectedTypeIndex, value))
            {
                _ = ReloadAsync();
            }
        }
    }

    /// <summary>搜索关键字（回车/输入即生效，带 350ms 防抖；清空后回到势力筛选）。</summary>
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

    /// <summary>列表为空（用于空状态提示）。</summary>
    public bool IsEmpty => _items.Count == 0 && !IsLoading;

    /// <summary>状态行文案（条数 / 加载中 / 失败原因）。</summary>
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

    /// <summary>首次进入页面时加载（页面常驻缓存，只做一次）。</summary>
    public async Task InitAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await ReloadAsync();
    }

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

    /// <summary>
    /// 按"搜索关键字优先、否则势力筛选"重新取数。上一轮未完成的取数会被作废（结果直接丢弃）。
    /// </summary>
    private async Task ReloadAsync()
    {
        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;

        var keyword = _searchText.Trim();
        IsLoading = true;
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsEmpty));

        try
        {
            var result = keyword.Length > 0
                ? await DEDService.SearchAsync(keyword)
                : await DEDService.QueryAllAsync(_selectedTypeIndex);

            if (cts.IsCancellationRequested)
            {
                return;
            }

            _isSearchResult = keyword.Length > 0;
            _loadFailed = false;
            _error = string.Empty;

            _items.Clear();
            foreach (var item in result ?? new List<DED>())
            {
                _items.Add(item);
            }

            // 保持原来的选中项；它不在新结果里就改选第一项（避免右侧留着一个已经看不见的实体）。
            Selected = _selected is not null && _items.Contains(_selected)
                ? _selected
                : _items.FirstOrDefault();
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

    private void OnLanguageChanged(object? sender, string language)
    {
        // 状态行是"求值时查资源"的文案，换语言后要重新通知一次（§9 第 63 条）。
        OnPropertyChanged(nameof(StatusText));
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
