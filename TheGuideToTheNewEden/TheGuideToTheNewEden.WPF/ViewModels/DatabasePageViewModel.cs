using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Services;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.ViewModels;

/// <summary>
/// 数据库页 VM：SDE 静态库的浏览/搜索工具（对应 WinUI 版 <c>Views/DatabasePage</c> 的代码后置，
/// WinUI 把搜索逻辑写在 <c>AutoSuggestBox.QuerySubmitted</c> 事件里，这里统一收进 VM，页面只做绑定与转发）。
/// <para>
/// 六种实体：物品（invTypes）/ 分组（invGroups）/ 星系（mapSolarSystems）/ 星域（mapRegions）/
/// 空间站（staStations）/ 缓存 ID-名称（IdName）。按名称模糊搜索（走各服务的 <c>SearchAsync</c>），
/// 输入若是数字则同时按 ID 精确查询并**置顶**（沿用 WinUI 语义，并做了去重）。
/// </para>
/// <para>
/// 与 WinUI 的有意差异：Core 的 <c>SearchAsync</c> 没有条数上限（物品表全表 LIKE 可能命中几万条），
/// WinUI 靠 SfDataGrid 虚拟化硬撑；WPF 侧在 VM 统一截断到 <see cref="MaxResults"/> 并在状态行提示"结果过多"，
/// 避免一次性实例化数万行表格。
/// </para>
/// </summary>
public sealed class DatabasePageViewModel : INotifyPropertyChanged
{
    /// <summary>模糊搜索的显示条数上限（截断提示文案用同一常量）。</summary>
    public const int MaxResults = 500;

    /// <summary>搜索防抖（本地库虽快，逐字符 LIKE 没有意义）。</summary>
    private const int SearchDebounceMs = 350;

    /// <summary>类型下拉的展示项：图标（编译期由 <see cref="SymbolRegular"/> 校验）+ 本地化名称键。</summary>
    public sealed record DbTypeOption(SymbolRegular Icon, string TextKey)
    {
        /// <summary>本地化名称（求值时查资源；语言切换由全局绑定重算覆盖，见 REFACTORING §9 第 63 条）。</summary>
        public string Text => FindString(TextKey);
    }

    private static readonly List<DbTypeOption> TypeOptionsAll = new()
    {
        new(SymbolRegular.Cube24, "DatabasePage_InvType"),
        new(SymbolRegular.Layer24, "DatabasePage_InvGroup"),
        new(SymbolRegular.Location24, "DatabasePage_MapSystem"),
        new(SymbolRegular.Globe24, "DatabasePage_MapRegion"),
        new(SymbolRegular.Building24, "DatabasePage_Station"),
        new(SymbolRegular.Tag24, "DatabasePage_IdName"),
    };

    private int _typeIndex;
    private string _searchText = string.Empty;
    private bool _isBusy;
    private bool _hasSearched;
    private bool _failed;
    private string _error = string.Empty;
    private bool _truncated;
    private int _resultCount;
    private object? _results;
    private CancellationTokenSource? _cts;

    public DatabasePageViewModel()
    {
        LanguageService.LanguageChanged += OnLanguageChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>类型下拉选项（0 物品 / 1 分组 / 2 星系 / 3 星域 / 4 空间站 / 5 ID-名称）。</summary>
    public IReadOnlyList<DbTypeOption> TypeOptions => TypeOptionsAll;

    /// <summary>当前实体类型索引；切换后用同一关键词立即重查。</summary>
    public int SelectedTypeIndex
    {
        get => _typeIndex;
        set
        {
            if (Set(ref _typeIndex, value))
            {
                _ = SearchAsync();
            }
        }
    }

    /// <summary>搜索关键字（350ms 防抖自动查；回车立即查走 <see cref="SearchNow"/>）。</summary>
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

    public bool IsBusy
    {
        get => _isBusy;
        private set => Set(ref _isBusy, value);
    }

    /// <summary>搜索结果（<c>List&lt;T&gt;</c>，运行时类型决定表格自动生成的列）。</summary>
    public object? Results
    {
        get => _results;
        private set => Set(ref _results, value);
    }

    /// <summary>当前显示条数（Header 计数）。</summary>
    public int ResultCount
    {
        get => _resultCount;
        private set => Set(ref _resultCount, value);
    }

    /// <summary>命中数超过上限、已截断（Footer 提示）。</summary>
    public bool Truncated
    {
        get => _truncated;
        private set => Set(ref _truncated, value);
    }

    public bool HasResults => _resultCount > 0;

    /// <summary>未搜索时的引导（居中图标 + 文案）。</summary>
    public bool ShowEmptyGuide => !_hasSearched && !_isBusy;

    /// <summary>已搜索但没有命中（区分"还没搜"与"搜了没有"）。</summary>
    public bool ShowNoResult => _hasSearched && !_isBusy && !_failed && _resultCount == 0;

    /// <summary>Header 右侧计数文案（"搜索结果 N"，语言切换后由绑定重算 + 重发通知双保险）。</summary>
    public string SearchResultText => $"{FindString("DatabasePage_SearchResult")} {_resultCount}";

    public bool HasStatus => _failed || _truncated;

    /// <summary>Footer 状态行：失败原因（危险色）或截断提示。</summary>
    public string StatusText
    {
        get
        {
            if (_failed)
            {
                return string.Format(FindString("DatabasePage_LoadFailed"), _error);
            }

            return _truncated
                ? string.Format(FindString("DatabasePage_Truncated"), MaxResults)
                : string.Empty;
        }
    }

    /// <summary>回车立即搜索（绕过防抖）。</summary>
    public void SearchNow() => _ = SearchAsync();

    private void QueueSearch()
    {
        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
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
            await SearchAsync();
        }
    }

    private async Task SearchAsync()
    {
        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;

        var keyword = _searchText.Trim();
        if (keyword.Length == 0)
        {
            ResetToEmpty();
            return;
        }

        IsBusy = true;
        _hasSearched = true;
        _failed = false;
        _error = string.Empty;
        NotifyState();

        try
        {
            // 输入整体是数字时按 ID 精确查询并置顶（沿用 WinUI 语义）。
            // ID 一律夹在 int 范围内：Core 各 QueryXxx 的 int 重载会把超出部分静默截断（§9 第 14 条）。
            var searchId = long.TryParse(keyword, out var parsed) && parsed > 0 && parsed <= int.MaxValue
                ? (int)parsed
                : -1;

            // 各分支返回不同泛型元组，switch 表达式推不出公共类型，用 switch 语句逐个解构。
            int hitCount;
            object? list;
            switch (_typeIndex)
            {
                case 0:
                    (var invTypes, hitCount) = await SearchInvTypesAsync(keyword, searchId);
                    list = invTypes;
                    break;
                case 1:
                    (var invGroups, hitCount) = await SearchInvGroupsAsync(keyword, searchId);
                    list = invGroups;
                    break;
                case 2:
                    (var solarSystems, hitCount) = await SearchSolarSystemsAsync(keyword, searchId);
                    list = solarSystems;
                    break;
                case 3:
                    (var regions, hitCount) = await SearchRegionsAsync(keyword, searchId);
                    list = regions;
                    break;
                case 4:
                    (var stations, hitCount) = await SearchStationsAsync(keyword, searchId);
                    list = stations;
                    break;
                default:
                    (var idNames, hitCount) = await SearchIdNamesAsync(keyword, searchId);
                    list = idNames;
                    break;
            }

            if (cts.IsCancellationRequested)
            {
                return;
            }

            Results = list;
            ResultCount = ((System.Collections.ICollection)list).Count;
            Truncated = hitCount > MaxResults;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            _failed = true;
            _error = ex.Message;
            Results = null;
            ResultCount = 0;
            Truncated = false;
        }
        finally
        {
            if (!cts.IsCancellationRequested)
            {
                IsBusy = false;
            }

            NotifyState();
        }
    }

    /// <summary>清空关键词：回到初始引导态（同时作废在跑的搜索）。</summary>
    private void ResetToEmpty()
    {
        _cts?.Cancel();
        _hasSearched = false;
        _failed = false;
        _error = string.Empty;
        Truncated = false;
        ResultCount = 0;
        Results = null;
        IsBusy = false;
        NotifyState();
    }

    // ---------- 六个实体类型的搜索分支 ----------
    // 通用流程：SearchAsync（命中 DataBaseSearchItem 列表）→ ID 去重 → 批量 Query 完整对象 → ID 精确置顶。

    private static async Task<(List<InvType>, int)> SearchInvTypesAsync(string keyword, int searchId)
    {
        var hits = await InvTypeService.SearchAsync(keyword);
        var ids = hits.Select(p => p.ID).Distinct().ToList();
        var list = ids.Count > 0 ? await InvTypeService.QueryTypesAsync(ids) : new List<InvType>();
        if (searchId > 0)
        {
            var exact = await InvTypeService.QueryTypeAsync(searchId);
            if (exact is not null)
            {
                InsertExact(list, exact, p => p.TypeID == exact.TypeID);
            }
        }

        return Cap(list, ids.Count);
    }

    private static async Task<(List<InvGroup>, int)> SearchInvGroupsAsync(string keyword, int searchId)
    {
        var hits = await InvGroupService.SearchAsync(keyword);
        var ids = hits.Select(p => p.ID).Distinct().ToList();
        var list = ids.Count > 0 ? await InvGroupService.QueryGroupsAsync(ids) : new List<InvGroup>();
        if (searchId > 0)
        {
            var exact = await InvGroupService.QueryGroupAsync(searchId);
            if (exact is not null)
            {
                InsertExact(list, exact, p => p.GroupID == exact.GroupID);
            }
        }

        return Cap(list, ids.Count);
    }

    private static async Task<(List<MapSolarSystem>, int)> SearchSolarSystemsAsync(string keyword, int searchId)
    {
        var hits = await MapSolarSystemService.SearchAsync(keyword);
        var ids = hits.Select(p => p.ID).Distinct().ToList();
        var list = ids.Count > 0 ? await MapSolarSystemService.QueryAsync(ids) : new List<MapSolarSystem>();
        if (searchId > 0)
        {
            var exact = await MapSolarSystemService.QueryAsync(searchId);
            if (exact is not null)
            {
                InsertExact(list, exact, p => p.SolarSystemID == exact.SolarSystemID);
            }
        }

        return Cap(list, ids.Count);
    }

    private static async Task<(List<MapRegion>, int)> SearchRegionsAsync(string keyword, int searchId)
    {
        var hits = await MapRegionService.SearchAsync(keyword);
        var ids = hits.Select(p => p.ID).Distinct().ToList();
        var list = ids.Count > 0 ? await MapRegionService.QueryAsync(ids) : new List<MapRegion>();
        if (searchId > 0)
        {
            var exact = await MapRegionService.QueryAsync(searchId);
            if (exact is not null)
            {
                InsertExact(list, exact, p => p.RegionID == exact.RegionID);
            }
        }

        return Cap(list, ids.Count);
    }

    private static async Task<(List<StaStation>, int)> SearchStationsAsync(string keyword, int searchId)
    {
        var hits = await StaStationService.SearchAsync(keyword);
        var ids = hits.Select(p => p.ID).Distinct().ToList();
        var list = ids.Count > 0 ? await StaStationService.QueryAsync(ids) : new List<StaStation>();
        if (searchId > 0)
        {
            var exact = await StaStationService.QueryAsync(searchId);
            if (exact is not null)
            {
                InsertExact(list, exact, p => p.StationID == exact.StationID);
            }
        }

        return Cap(list, ids.Count);
    }

    private static async Task<(List<IdName>, int)> SearchIdNamesAsync(string keyword, int searchId)
    {
        var list = await IDNameDBService.SearchAsync(keyword);
        var hitCount = list.Count;
        if (searchId > 0)
        {
            var exact = await IDNameDBService.QueryAsync(searchId);
            if (exact is not null)
            {
                InsertExact(list, exact, p => p.Id == exact.Id);
            }
        }

        return Cap(list, hitCount);
    }

    /// <summary>截断到 <see cref="MaxResults"/>（顺带给出命中总数，用于截断提示）。</summary>
    private static (List<T>, int) Cap<T>(List<T> all, int hitCount)
    {
        if (all.Count > MaxResults)
        {
            all = all.GetRange(0, MaxResults);
        }

        return (all, hitCount);
    }

    /// <summary>把"ID 精确命中的那一条"放到列表最前；已在列表里则移到最前（去重，不出现两条）。</summary>
    private static void InsertExact<T>(List<T> list, T exact, Func<T, bool> sameId)
    {
        // List<T>.FindIndex 只收 Predicate<T>，lambda 显式包一层（Func → Predicate 不能隐式转换）。
        var index = list.FindIndex(item => sameId(item));
        if (index >= 0)
        {
            var item = list[index];
            list.RemoveAt(index);
            list.Insert(0, item);
        }
        else
        {
            list.Insert(0, exact);
        }
    }

    private void OnLanguageChanged(object? sender, string language)
    {
        // 计数/状态行/类型名都是"求值时查资源"的文案，换语言后重发通知（§9 第 63 条）。
        NotifyState();
    }

    private static string FindString(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

    private void NotifyState()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ShowEmptyGuide));
        OnPropertyChanged(nameof(ShowNoResult));
        OnPropertyChanged(nameof(SearchResultText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HasStatus));
    }

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
