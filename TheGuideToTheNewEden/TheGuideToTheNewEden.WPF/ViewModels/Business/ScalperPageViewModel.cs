using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Business;
using TheGuideToTheNewEden.WPF.Services.Settings;
using MarketOrder = TheGuideToTheNewEden.Core.Models.Market.Order;

namespace TheGuideToTheNewEden.WPF.ViewModels.Business;

/// <summary>
/// 倒货页：源/目的市场与目标物品设置、取订单与历史、调用计算引擎得出推荐结果。
/// 展示投影与取数编排在此，不直接触碰控件或窗口（详情窗由页面负责弹出）。
/// </summary>
public sealed class ScalperPageViewModel : INotifyPropertyChanged
{
    private CancellationTokenSource? _cancellationTokenSource;
    private int _typeCount;

    // ---------- 基本设置 ----------

    public ScalperSetting Setting { get; private set; } = new();

    public int BuyPriceType
    {
        get => (int)Setting.BuyPrice;
        set
        {
            Setting.BuyPrice = (ScalperSetting.PriceType)value;
            OnPropertyChanged();
        }
    }

    public int SellPriceType
    {
        get => (int)Setting.SellPrice;
        set
        {
            Setting.SellPrice = (ScalperSetting.PriceType)value;
            OnPropertyChanged();
        }
    }

    public int SourceSalesType
    {
        get => (int)Setting.SourceSalesType;
        set
        {
            Setting.SourceSalesType = (ScalperSetting.SalesType)value;
            OnPropertyChanged();
        }
    }

    public int DestinationSalesType
    {
        get => (int)Setting.DestinationSalesType;
        set
        {
            Setting.DestinationSalesType = (ScalperSetting.SalesType)value;
            OnPropertyChanged();
        }
    }

    // ---------- 目标物品 ----------

    private List<int> _targetMarketTypes = [];

    public List<int> TargetMarketTypes
    {
        get => _targetMarketTypes;
        set
        {
            if (Set(ref _targetMarketTypes, value))
            {
                TargetMarketTypesCount = value.Count;
            }
        }
    }

    private int _targetMarketTypesCount;

    /// <summary>已选目标物品数量。树控件就地增删列表后通过 TwoWay 绑定回写这里刷新显示。</summary>
    public int TargetMarketTypesCount
    {
        get => _targetMarketTypesCount;
        set => Set(ref _targetMarketTypesCount, value);
    }

    // ---------- 过滤清单 ----------

    public ObservableCollection<InvType> FilterTypes { get; } = [];

    // ---------- 结果 ----------

    private List<ScalperItem> _scalperItems = [];

    public List<ScalperItem> ScalperItems
    {
        get => _scalperItems;
        private set => Set(ref _scalperItems, value);
    }

    public bool HasResult => ScalperItems.Count > 0;

    // ---------- 状态 ----------

    private bool _isBusy;

    /// <summary>是否正在取数/分析（用于界面态；等待文案与结果提示走 <see cref="PageNotifyService"/>）。</summary>
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }

    // ---------- 初始化 ----------

    public void Init()
    {
        Setting = ScalperSettingService.Load();
        OnPropertyChanged(nameof(Setting));
        OnPropertyChanged(nameof(BuyPriceType));
        OnPropertyChanged(nameof(SellPriceType));
        OnPropertyChanged(nameof(SourceSalesType));
        OnPropertyChanged(nameof(DestinationSalesType));

        TargetMarketTypes = Setting.MarketTypes?.ToList() ?? [];

        FilterTypes.Clear();
        foreach (var type in BusinessService.Current.GetFilterTypes())
        {
            FilterTypes.Add(type);
        }

        BusinessService.Current.FilterChanged -= OnFilterChanged;
        BusinessService.Current.FilterChanged += OnFilterChanged;
    }

    /// <summary>页面离开时解绑事件并取消进行中的取数。</summary>
    public void Dispose()
    {
        BusinessService.Current.FilterChanged -= OnFilterChanged;
        _cancellationTokenSource?.Cancel();
    }

    private void OnFilterChanged(List<InvType> types, bool isAdd)
    {
        if (!isAdd)
        {
            return;
        }

        foreach (var type in types)
        {
            if (FilterTypes.All(p => p.TypeID != type.TypeID))
            {
                FilterTypes.Add(type);
            }
        }
    }

    public void AddFilterTypes(IEnumerable<InvType> types)
    {
        foreach (var type in types)
        {
            if (FilterTypes.All(p => p.TypeID != type.TypeID))
            {
                FilterTypes.Add(type);
            }
        }
    }

    public void RemoveFilterTypes(IEnumerable<InvType> types)
    {
        var list = types.ToList();
        foreach (var type in list)
        {
            var local = FilterTypes.FirstOrDefault(p => p.TypeID == type.TypeID);
            if (local is not null)
            {
                FilterTypes.Remove(local);
            }
        }

        BusinessService.Current.RemoveFromFilter(list);
    }

    public void SaveSetting()
    {
        Setting.MarketTypes = TargetMarketTypes.ToArray();
        ScalperSettingService.Save(Setting);
    }

    // ---------- 校验 ----------

    /// <summary>源/目的市场与目标物品是否都已选。不通过时右下角给错误提示。</summary>
    public bool IsValid()
    {
        if (Setting.SourceMarketLocation is null)
        {
            PageNotifyService.Error(FindString("BusinessPage_UnselectedSourceMarket"));
            return false;
        }

        if (Setting.DestinationMarketLocation is null)
        {
            PageNotifyService.Error(FindString("BusinessPage_UnselectedDestinationMarket"));
            return false;
        }

        if (TargetMarketTypes.Count == 0)
        {
            PageNotifyService.Error(FindString("BusinessPage_UnselectedTargetType"));
            return false;
        }

        return true;
    }

    public void Cancel() => _cancellationTokenSource?.Cancel();

    // ---------- 取订单 ----------

    public async Task GetOrdersAsync()
    {
        if (!IsValid())
        {
            return;
        }

        SaveSetting();
        IsBusy = true;
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        var ero = Core.Log.GetErrorCount();
        var info = Core.Log.GetInfoCount();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 全屏等待遮罩 + 取消（对应 WinUI 的 ShowWaiting(..., CancelCallback)）
        PageNotifyService.ShowWaiting(FindString("BusinessPage_GettingSourceMarketOrder"), Cancel);

        try
        {
            List<MarketOrder>? allSourceOrders;
            try
            {
                allSourceOrders = await GetOrdersOfAsync(Setting.SourceMarketLocation, token, FindString("BusinessPage_GettingSourceMarketOrder"));
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                PageNotifyService.Error($"{FindString("BusinessPage_GettingSourceMarketOrderFalied")}：{ex.Message}");
                return;
            }

            if (IsCancelled(token))
            {
                return;
            }

            PageNotifyService.UpdateWaiting(FindString("BusinessPage_GettingTargetMarketOrder"));
            List<MarketOrder>? allDestinationOrders;
            try
            {
                allDestinationOrders = await GetOrdersOfAsync(Setting.DestinationMarketLocation, token, FindString("BusinessPage_GettingTargetMarketOrder"));
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                PageNotifyService.Error($"{FindString("BusinessPage_GettingTargetMarketOrderFalied")}：{ex.Message}");
                return;
            }

            if (IsCancelled(token))
            {
                return;
            }

            if (FilterTypes.Count > 0)
            {
                var filterIds = FilterTypes.Select(p => (long)p.TypeID).ToHashSet();
                allSourceOrders = RemoveFilterTypes(allSourceOrders, filterIds);
                allDestinationOrders = RemoveFilterTypes(allDestinationOrders, filterIds);
            }

            if (allSourceOrders is not { Count: > 0 } || allDestinationOrders is not { Count: > 0 })
            {
                PageNotifyService.Error(FindString("BusinessPage_GetOrderFailed"));
                return;
            }

            var items = await Task.Run(() => ScalperCalculator.BuildItems(allSourceOrders, allDestinationOrders, TargetMarketTypes.ToHashSet()));
            var typeIds = items.Select(p => p.InvType.TypeID).Distinct().ToList();
            _typeCount = typeIds.Count;

            var sourceNoHistory = -1;
            var destinationNoHistory = -1;
            if (typeIds.Count > 0)
            {
                PageNotifyService.UpdateWaiting(FindString("BusinessPage_GettingSourceMarketHistyory"));
                var sourceHistory = await MarketOrderService.Current.GetHistoryBatchAsync(
                    typeIds, Setting.SourceMarketLocation.RegionId, token,
                    (done, _) => PageNotifyService.UpdateWaiting($"{FindString("BusinessPage_GettingSourceMarketHistyory")}（{done}/{_typeCount}）"));
                if (IsCancelled(token))
                {
                    return;
                }

                sourceNoHistory = typeIds.Count - sourceHistory.Count;

                PageNotifyService.UpdateWaiting(FindString("BusinessPage_GettingTargetMarketHistyory"));
                var destinationHistory = await MarketOrderService.Current.GetHistoryBatchAsync(
                    typeIds, Setting.DestinationMarketLocation.RegionId, token,
                    (done, _) => PageNotifyService.UpdateWaiting($"{FindString("BusinessPage_GettingTargetMarketHistyory")}（{done}/{_typeCount}）"));
                if (IsCancelled(token))
                {
                    return;
                }

                destinationNoHistory = typeIds.Count - destinationHistory.Count;

                PageNotifyService.UpdateWaiting(FindString("BusinessPage_MatchinOorderHistory"));
                items = await Task.Run(() =>
                {
                    ScalperCalculator.SetHistory(items, sourceHistory, destinationHistory);
                    return items.Where(p => p.SourceStatistics is { Count: > 0 } && p.DestinationStatistics is { Count: > 0 }).ToList();
                });
            }

            ScalperItems = items;
            OnPropertyChanged(nameof(HasResult));
            stopwatch.Stop();
            PageNotifyService.Success(string.Format(
                FindString("BusinessPage_GotOrder"),
                ScalperItems.Count,
                stopwatch.Elapsed.TotalMinutes.ToString("N2"),
                Core.Log.GetErrorCount() - ero,
                Core.Log.GetInfoCount() - info,
                sourceNoHistory,
                destinationNoHistory));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(ex.Message);
        }
        finally
        {
            IsBusy = false;
            PageNotifyService.HideWaiting();
        }
    }

    private static List<MarketOrder>? RemoveFilterTypes(List<MarketOrder>? orders, HashSet<long> filterIds)
    {
        return orders?.Where(p => !filterIds.Contains(p.TypeId)).ToList();
    }

    private async Task<List<MarketOrder>?> GetOrdersOfAsync(MarketLocation location, CancellationToken token, string statusPrefix)
    {
        var skipStructure = MarketOrderSettingService.ScalperSikpStructureValue;
        switch (location.Type)
        {
            case MarketLocationType.Region:
                return await MarketOrderService.Current.GetAllRegionOrdersAsync(
                    location.Id, skipStructure, token,
                    (page, total) => PageNotifyService.UpdateWaiting($"{statusPrefix}:{page}/{total}"));

            case MarketLocationType.SolarSystem:
                return await MarketOrderService.Current.GetSolarSystemOrdersAsync(
                    (int)location.Id, skipStructure, token,
                    (page, total) => PageNotifyService.UpdateWaiting($"{statusPrefix}:{page}/{total}"));

            case MarketLocationType.Structure:
                return await MarketOrderService.Current.GetStructureOrdersAsync(location.Id, token);

            default:
                return null;
        }
    }

    // ---------- 分析 ----------

    public async Task AnalyseAsync()
    {
        if (ScalperItems.Count == 0)
        {
            PageNotifyService.Error(FindString("BusinessPage_NoOrder"));
            return;
        }

        if (!IsValid())
        {
            return;
        }

        SaveSetting();
        IsBusy = true;
        PageNotifyService.ShowWaiting(FindString("BusinessPage_Analyzing"));
        try
        {
            var items = await Task.Run(() => ScalperCalculator.Calculate(ScalperItems.ToList(), Setting));
            ScalperItems = items;
            OnPropertyChanged(nameof(HasResult));
            PageNotifyService.Success($"{FindString("BusinessPage_Analysed")}: {items.Count}");
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(ex.Message);
        }
        finally
        {
            IsBusy = false;
            PageNotifyService.HideWaiting();
        }
    }

    private bool IsCancelled(CancellationToken token)
    {
        if (!token.IsCancellationRequested)
        {
            return false;
        }

        PageNotifyService.Info(FindString("General_Canceld"));
        return true;
    }

    // ---------- 详情与购物车 ----------

    /// <summary>把分析结果转成购物车条目。</summary>
    public static List<ScalperShoppingItem> ToShoppingItems(IEnumerable<ScalperItem> items)
        => items.Select(p => new ScalperShoppingItem(p)).ToList();

    public void AddToCart(IEnumerable<ScalperItem> items)
    {
        foreach (var item in items)
        {
            BusinessService.Current.ShoppingCart.Add(new ScalperShoppingItem(item));
        }
    }

    // ---------- INotifyPropertyChanged ----------

    public event PropertyChangedEventHandler? PropertyChanged;

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

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
