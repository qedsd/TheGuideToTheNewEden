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
/// 任务页 VM：左侧按"等级（1-5 级）"筛选 + 名称搜索的任务列表，右侧是选中任务的攻略正文（Markdown）。
/// 对应 WinUI 版 <c>MissionViewModel</c> + <c>MissionPage</c> 的代码后置。
/// <para>
/// 数据来自 Core 的 <c>MissionService</c>（静态库 <c>Resources/Database/static.db</c>）：
/// 列表按等级或名称查，正文按 <c>Mission.Id</c> 查 <c>MissionContent</c>。
/// </para>
/// </summary>
public sealed class MissionPageViewModel : INotifyPropertyChanged
{
    private const int SearchDebounceMs = 350;

    private readonly ObservableCollection<Mission> _items = new();

    private Mission? _selected;
    private int _levelIndex;
    private string _searchText = string.Empty;
    private bool _isLoading;
    private bool _isContentLoading;
    private bool _loadFailed;
    private bool _isSearchResult;
    private string _error = string.Empty;
    private string _contentText = string.Empty;
    private bool _initialized;

    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _contentCts;

    public MissionPageViewModel()
    {
        LanguageService.LanguageChanged += OnLanguageChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<Mission> Items => _items;

    public Mission? Selected
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(ShowNoContent));
                _ = LoadContentAsync(value);
            }
        }
    }

    public bool HasSelection => _selected is not null;

    /// <summary>等级下拉的选中索引（0 → 1 级 … 4 → 5 级，与 WinUI 的"需要 +1"一致）。</summary>
    public int LevelIndex
    {
        get => _levelIndex;
        set
        {
            if (Set(ref _levelIndex, value))
            {
                _ = ReloadAsync();
            }
        }
    }

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

    /// <summary>正文加载中（右侧卡片显示局部等待，不用全局遮罩）。</summary>
    public bool IsContentLoading
    {
        get => _isContentLoading;
        private set
        {
            if (Set(ref _isContentLoading, value))
            {
                OnPropertyChanged(nameof(ShowNoContent));
            }
        }
    }

    /// <summary>
    /// "该任务暂无攻略正文"提示的显隐：**必须等正文加载结束**再判断，否则每次切换任务都会先闪一下这句提示。
    /// </summary>
    public bool ShowNoContent => !_isContentLoading && _selected is not null && !HasContent;

    public bool IsEmpty => _items.Count == 0 && !IsLoading;

    /// <summary>选中任务的攻略正文（Markdown 原文，由 <c>MarkdownPresenter</c> 渲染）。</summary>
    public string ContentText
    {
        get => _contentText;
        private set
        {
            if (Set(ref _contentText, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(HasContent));
                OnPropertyChanged(nameof(ShowNoContent));
            }
        }
    }

    public bool HasContent => !string.IsNullOrWhiteSpace(_contentText);

    public string StatusText
    {
        get
        {
            if (IsLoading)
            {
                return FindString("MissionPage_Loading");
            }

            if (_loadFailed)
            {
                return string.Format(FindString("MissionPage_LoadFailed"), _error);
            }

            return string.Format(FindString(_isSearchResult ? "MissionPage_SearchSummary" : "MissionPage_Summary"), _items.Count);
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
        IsLoading = true;
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsEmpty));

        try
        {
            var result = keyword.Length > 0
                ? await MissionService.QueryMissionAsync(keyword)
                : await MissionService.QueryMissionAsync(_levelIndex + 1);

            if (cts.IsCancellationRequested)
            {
                return;
            }

            _isSearchResult = keyword.Length > 0;
            _loadFailed = false;
            _error = string.Empty;

            _items.Clear();
            foreach (var item in result ?? new List<Mission>())
            {
                _items.Add(item);
            }

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

    /// <summary>
    /// 取选中任务的正文。Core 的 <c>QueryMissionContent</c> 是同步的（单行查询），
    /// 放到线程池上跑，避免在 UI 线程上碰 SQLite（§9 第 37 条）。
    /// </summary>
    private async Task LoadContentAsync(Mission? mission)
    {
        _contentCts?.Cancel();

        if (mission is null)
        {
            ContentText = string.Empty;
            IsContentLoading = false;
            return;
        }

        var cts = new CancellationTokenSource();
        _contentCts = cts;
        IsContentLoading = true;

        try
        {
            var id = mission.Id;
            var content = await Task.Run(() => MissionService.QueryMissionContent(id));

            if (!cts.IsCancellationRequested)
            {
                ContentText = content?.Content ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);

            if (!cts.IsCancellationRequested)
            {
                ContentText = string.Empty;
                _error = ex.Message;
            }
        }
        finally
        {
            if (!cts.IsCancellationRequested)
            {
                IsContentLoading = false;
            }
        }
    }

    private void OnLanguageChanged(object? sender, string language)
        => OnPropertyChanged(nameof(StatusText));

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
