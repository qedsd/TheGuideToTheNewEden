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

    /// <summary>阶段 299：两侧价格口径都为订单簿口径时启用静态预筛（两阶段取数）。</summary>
    public bool PrescreenEnabled => !Setting.IsHistoryBuyPrice && !Setting.IsHistorySellPrice;

    /// <summary>热力值阈值模式下拉索引（0=绝对 / 1=相对自身日均）。</summary>
    public int HeatThresholdModeIndex
    {
        get => Setting.HeatThresholdMode == Core.Models.Market.ScalperSetting.HeatThresholdKind.RelativeToAverage ? 1 : 0;
        set
        {
            Setting.HeatThresholdMode = value == 1
                ? Core.Models.Market.ScalperSetting.HeatThresholdKind.RelativeToAverage
                : Core.Models.Market.ScalperSetting.HeatThresholdKind.Absolute;
            OnPropertyChanged(nameof(ShowHeatRelativeFactor));
        }
    }

    /// <summary>相对模式才显示"自身日均倍数"输入框。</summary>
    public bool ShowHeatRelativeFactor => Setting.HeatThresholdMode == Core.Models.Market.ScalperSetting.HeatThresholdKind.RelativeToAverage;

    /// <summary>阶段 299：推荐权重预设（保守/均衡/激进），应用到全部 25 个权重并立即持久化。</summary>
    public void ApplyWeightPreset(string kind)
    {
        var (conservative, balanced, aggressive) = PresetWeights();
        var values = kind == "conservative" ? conservative : kind == "aggressive" ? aggressive : balanced;
        Setting.SuggestionROI = values.ROI;
        Setting.SuggestionNetProfit = values.TargetNetProfit;
        Setting.SuggestionPrincipal = values.Principal;
        Setting.SuggestionSales = values.Sales;
        Setting.SuggestionHistoryPriceFluctuation = values.HistoryFluctuation;
        Setting.SuggestionNowPriceFluctuation = values.NowFluctuation;
        Setting.SuggestionSaturation = values.Saturation;
        Setting.SuggestionHeatValue = values.Heat;
        Setting.SuggestionIskPerVolume = values.IskPerVolume;
        Setting.SuggestionNetProfitAfterFees = values.NetProfitAfterFees;
        Setting.SuggestionROIAfterFees = values.ROIAfterFees;
        Setting.SuggestionFillRatio = values.FillRatio;
        Setting.SuggestionCompetitionShare = values.CompetitionShare;
        Setting.SuggestionDaysToSell = values.DaysToSell;
        Setting.SuggestionProfitPerHour = values.ProfitPerHour;
        Setting.SuggestionRiskAdjustedProfit = values.RiskAdjustedProfit;
        Setting.SuggestionProfitPerVolumeAfterFees = values.ProfitPerVolume;
        Setting.SuggestionPriceTrend = values.Trend;
        Setting.SuggestionPriceCushion = values.Cushion;
        Setting.SuggestionSalesStability = values.Stability;
        Setting.SuggestionConfidence = values.Confidence;
        PageNotifyService.Success(LanguageService.Format("BusinessPage_WeightPresetApplied", kind));
        SaveSetting();
    }

    private static (PresetSet, PresetSet, PresetSet) PresetWeights()
    {
        PresetSet balanced = new(40, 30, 10, 5, 5, 5, 5, 30, 10, 30, 30, 15, 15, 20, 25, 10, 10, 15, 10, 15, 15);
        PresetSet conservative = new(10, 10, 0, 5, 0, 0, 10, 10, 5, 25, 25, 25, 20, 25, 10, 35, 10, 5, 15, 20, 30);
        PresetSet aggressive = new(30, 40, 0, 5, 0, 0, 5, 10, 15, 50, 10, 10, 10, 10, 40, 0, 15, 10, 5, 5, 10);
        return (conservative, balanced, aggressive);
    }

    /// <summary>权重预设值（顺序见 ApplyWeightPreset）。</summary>
    private sealed record PresetSet(
        double ROI, double TargetNetProfit, double Principal, double Sales, double HistoryFluctuation,
        double NowFluctuation, double Saturation, double Heat, double IskPerVolume,
        double NetProfitAfterFees, double ROIAfterFees, double FillRatio, double CompetitionShare,
        double DaysToSell, double ProfitPerHour, double RiskAdjustedProfit, double ProfitPerVolume,
        double Trend, double Cushion, double Stability, double Confidence);

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

    // ---------- 排除列表（阶段 286）：复用市场物品选择树 ----------

    /// <summary>选择树与排除列表同步用的 TypeId 列表（控件就地增删，TwoWay 绑定）。</summary>
    public List<int> FilterTypeSelection { get; set; } = [];

    /// <summary>选择树勾选变化 → 同步到排除列表（新增/移除）。</summary>
    public void SyncFilterTypesFromSelection(IReadOnlyList<int>? selectedIds)
    {
        var ids = selectedIds is null ? new HashSet<int>() : selectedIds.Where(p => p > 0).ToHashSet();

        var toRemove = FilterTypes.Where(p => !ids.Contains(p.TypeID)).ToList();
        if (toRemove.Count > 0)
        {
            RemoveFilterTypes(toRemove);
        }

        var existing = FilterTypes.Select(p => p.TypeID).ToHashSet();
        var toAddIds = ids.Where(p => !existing.Contains(p)).ToList();
        if (toAddIds.Count > 0)
        {
            var types = Core.Services.DB.InvTypeService.QueryTypes(toAddIds);
            if (types.Count > 0)
            {
                AddFilterTypes(types);
                BusinessService.Current.AddToFilter(types);
            }
        }
    }

    /// <summary>把当前排除列表回写成选择树的选中集合（列表里删除后同步过去）。</summary>
    private void RefreshFilterSelection()
    {
        FilterTypeSelection = FilterTypes.Select(p => p.TypeID).ToList();
        OnPropertyChanged(nameof(FilterTypeSelection));
    }

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

    // ---------- 页面内等待态（阶段 292：不再使用全局等待遮罩，避免阻塞其它功能） ----------

    private string _busyText = string.Empty;
    public string BusyText { get => _busyText; private set => Set(ref _busyText, value); }

    private bool _canCancelBusy;
    public bool CanCancelBusy { get => _canCancelBusy; private set => Set(ref _canCancelBusy, value); }

    private void BeginBusy(string text, bool canCancel)
    {
        BusyText = text;
        CanCancelBusy = canCancel;
        IsBusy = true;
    }

    private void UpdateBusy(string text) => BusyText = text;

    private void EndBusy()
    {
        IsBusy = false;
        CanCancelBusy = false;
    }

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
        RefreshFilterSelection();

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
        RefreshFilterSelection();
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
        RefreshFilterSelection();
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
        RefreshFilterSelection();
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
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        var ero = Core.Log.GetErrorCount();
        var info = Core.Log.GetInfoCount();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 页面内等待态（阶段 292：只遮倒货页，不阻塞其它功能），可取消
        BeginBusy(FindString("BusinessPage_GettingSourceMarketOrder"), canCancel: true);

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

            UpdateBusy(FindString("BusinessPage_GettingTargetMarketOrder"));
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

            var items = await Task.Run(() =>
                ScalperCalculator.BuildItems(allSourceOrders, allDestinationOrders, TargetMarketTypes.ToHashSet(), Setting.StaleOrderDays));
            // 阶段 299：两阶段取数——先用订单簿静态预筛，只对"税后价差为正"的候选拉历史
            List<ScalperItem> prescreened;
            if (PrescreenEnabled)
            {
                UpdateBusy(FindString("BusinessPage_Prescreening"));
                prescreened = await Task.Run(() => ScalperCalculator.Prescreen(items, Setting));
                if (prescreened is not { Count: > 0 })
                {
                    PageNotifyService.Error(FindString("BusinessPage_PrescreenEmpty"));
                    return;
                }
            }
            else
            {
                prescreened = items;
            }

            var typeIds = prescreened.Select(p => p.InvType.TypeID).Distinct().ToList();
            _typeCount = typeIds.Count;

            var sourceNoHistory = -1;
            var destinationNoHistory = -1;
            if (typeIds.Count > 0)
            {
                UpdateBusy(FindString("BusinessPage_GettingSourceMarketHistyory"));
                var sourceHistory = await MarketOrderService.Current.GetHistoryBatchAsync(
                    typeIds, Setting.SourceMarketLocation.RegionId, token,
                    (done, _) => UpdateBusy($"{FindString("BusinessPage_GettingSourceMarketHistyory")}（{done}/{_typeCount}）"));
                if (IsCancelled(token))
                {
                    return;
                }

                sourceNoHistory = typeIds.Count - sourceHistory.Count;

                UpdateBusy(FindString("BusinessPage_GettingTargetMarketHistyory"));
                var destinationHistory = await MarketOrderService.Current.GetHistoryBatchAsync(
                    typeIds, Setting.DestinationMarketLocation.RegionId, token,
                    (done, _) => UpdateBusy($"{FindString("BusinessPage_GettingTargetMarketHistyory")}（{done}/{_typeCount}）"));
                if (IsCancelled(token))
                {
                    return;
                }

                destinationNoHistory = typeIds.Count - destinationHistory.Count;

                UpdateBusy(FindString("BusinessPage_MatchinOorderHistory"));
                prescreened = await Task.Run(() =>
                {
                    ScalperCalculator.SetHistory(prescreened, sourceHistory, destinationHistory);
                    return prescreened.Where(p => p.SourceStatistics is { Count: > 0 } && p.DestinationStatistics is { Count: > 0 }).ToList();
                });
            }

            ScalperItems = prescreened;
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
            // 阶段 291：取完订单直接自动分析推荐（分析失败会走外层 catch）
            UpdateBusy(FindString("BusinessPage_Analyzing"));
            await RunAnalysisCoreAsync();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(ex.Message);
        }
        finally
        {
                EndBusy();
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
                    (page, total) => UpdateBusy($"{statusPrefix}:{page}/{total}"));

            case MarketLocationType.SolarSystem:
                return await MarketOrderService.Current.GetSolarSystemOrdersAsync(
                    (int)location.Id, skipStructure, token,
                    (page, total) => UpdateBusy($"{statusPrefix}:{page}/{total}"));

            case MarketLocationType.Structure:
                return await MarketOrderService.Current.GetStructureOrdersAsync(location.Id, token);

            default:
                return null;
        }
    }

    // ---------- 分析 ----------

    /// <summary>分析推荐（不管理等待遮罩）；"获取订单"末尾会自动调用一次。</summary>
    private async Task RunAnalysisCoreAsync()
    {
        var items = await Task.Run(() => ScalperCalculator.Calculate(ScalperItems.ToList(), Setting));
        ScalperItems = items;
        OnPropertyChanged(nameof(HasResult));
        PageNotifyService.Success($"{FindString("BusinessPage_Analysed")}: {items.Count}");
    }

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
        BeginBusy(FindString("BusinessPage_Analyzing"), canCancel: false);
        try
        {
            await RunAnalysisCoreAsync();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(ex.Message);
        }
        finally
        {
                EndBusy();
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
