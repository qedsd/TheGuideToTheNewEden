using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.WPF.Services.KB;

namespace TheGuideToTheNewEden.WPF.ViewModels.KB;

/// <summary>ZKB 主页面（多标签宿主）的 ViewModel：只负责顶部的实体搜索。</summary>
public sealed class ZkbPageViewModel : INotifyPropertyChanged
{
    private CancellationTokenSource? _cts;
    private bool _hasSearchResults;
    private string? _searchStatus;
    private string _keyword = string.Empty;

    /// <summary>搜索结果（已过滤为 ZKB 支持的类别）。</summary>
    public ObservableCollection<IdName> SearchResults { get; } = [];

    public bool HasSearchResults
    {
        get => _hasSearchResults;
        private set => Set(ref _hasSearchResults, value);
    }

    /// <summary>搜索提示（如"未找到匹配的实体"）。</summary>
    public string? SearchStatus
    {
        get => _searchStatus;
        private set => Set(ref _searchStatus, value);
    }

    /// <summary>当前关键字（清空搜索框时由页面回写）。</summary>
    public string Keyword
    {
        get => _keyword;
        set => Set(ref _keyword, value);
    }

    public async Task SearchAsync(string? keyword)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        keyword = keyword?.Trim();
        if (string.IsNullOrEmpty(keyword))
        {
            ClearResults();
            return;
        }

        try
        {
            var results = await ZkbQueryService.SearchEntitiesAsync(keyword, token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            SearchResults.Clear();
            foreach (var item in results.Take(50))
            {
                SearchResults.Add(item);
            }

            HasSearchResults = SearchResults.Count > 0;
            SearchStatus = HasSearchResults ? null : FindString("ZKBPage_NoSearchResult");
        }
        catch (OperationCanceledException)
        {
            // 输入变化导致的取消，忽略
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            SearchStatus = FindString("ZKBPage_QueryFailed");
        }
    }

    /// <summary>清空搜索结果（选中或清空输入框后调用）。</summary>
    public void ClearResults()
    {
        SearchResults.Clear();
        HasSearchResults = false;
        SearchStatus = null;
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
