using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.Core.Models.Universe;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Business;
using TheGuideToTheNewEden.WPF.Services.Settings;
using MarketOrder = TheGuideToTheNewEden.Core.Models.Market.Order;

namespace TheGuideToTheNewEden.WPF.ViewModels.Business;

/// <summary>
/// 市场页：物品选择（分组树/搜索/收藏）+ 星域订单（卖单/买单）+ 历史统计图表（LiveCharts）。
/// 展示投影与取数编排在此，不直接触碰控件；取数失败由服务层吞异常并返回 null。
/// </summary>
public sealed class MarketPageViewModel : INotifyPropertyChanged
{
    private static readonly HttpClient Http = new();

    private const int DefaultHistoryRangeIndex = 1; // 默认最近 3 个月（与 WinUI 一致）

    // ---------- 市场位置（星域 / 星系 / 建筑） ----------

    private MarketLocation? _selectedMarketLocation;

    /// <summary>
    /// 当前价格来源（由 <see cref="Views.UserControls.MarketLocationSelectorView"/> TwoWay 回写；
    /// 变更后自动重取当前物品的订单与历史）。
    /// </summary>
    public MarketLocation? SelectedMarketLocation
    {
        get => _selectedMarketLocation;
        set
        {
            if (Set(ref _selectedMarketLocation, value) && value is not null && SelectedInvType is not null)
            {
                _ = SelectInvTypeAsync(SelectedInvType);
            }
        }
    }

    // ---------- 物品选择 ----------

    /// <summary>市场分组树（分组 → 物品）。</summary>
    public ObservableCollection<MarketItem> TreeRoots { get; } = [];

    private readonly List<MarketItem> _allTypeItems = [];

    public ObservableCollection<MarketItem> SearchResults { get; } = [];

    public ObservableCollection<MarketItem> StaredItems { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (Set(ref _searchText, value))
            {
                OnPropertyChanged(nameof(IsSearching));
                ApplySearch();
            }
        }
    }

    /// <summary>是否处于搜索态（搜索框有内容时用结果列表替换树）。</summary>
    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchText);

    private MarketItem? _selectedInvTypeItem;

    public MarketItem? SelectedInvTypeItem
    {
        get => _selectedInvTypeItem;
        set
        {
            if (Set(ref _selectedInvTypeItem, value) && value is not null)
            {
                _ = SelectInvTypeAsync(value.InvType);
            }
        }
    }

    private InvType? _selectedInvType;

    public InvType? SelectedInvType
    {
        get => _selectedInvType;
        private set
        {
            if (Set(ref _selectedInvType, value))
            {
                OnPropertyChanged(nameof(HasSelectedInvType));
                OnPropertyChanged(nameof(SelectedInvTypeName));
                OnPropertyChanged(nameof(SelectedInvTypeDescription));
                OnPropertyChanged(nameof(SelectedInvTypeVolumeText));
            }
        }
    }

    public bool HasSelectedInvType => SelectedInvType is not null;

    public string SelectedInvTypeName => SelectedInvType?.TypeName ?? string.Empty;

    public string SelectedInvTypeDescription => SelectedInvType?.Description ?? string.Empty;

    public string SelectedInvTypeVolumeText => SelectedInvType is null ? string.Empty : $"{SelectedInvType.Volume:N2} m³";

    private ImageSource? _selectedInvTypeIcon;

    public ImageSource? SelectedInvTypeIcon
    {
        get => _selectedInvTypeIcon;
        private set => Set(ref _selectedInvTypeIcon, value);
    }

    private bool _stared;

    public bool Stared
    {
        get => _stared;
        private set => Set(ref _stared, value);
    }

    // ---------- 订单与统计 ----------

    /// <summary>卖单：价格升序。</summary>
    public ObservableCollection<MarketOrder> SellOrders { get; } = [];

    /// <summary>买单：价格降序。</summary>
    public ObservableCollection<MarketOrder> BuyOrders { get; } = [];

    private double _sell5P, _buy5P, _sellMean, _buyMean;
    private long _sellAmount, _buyAmount;

    public double Sell5P { get => _sell5P; private set => Set(ref _sell5P, value); }
    public double Buy5P { get => _buy5P; private set => Set(ref _buy5P, value); }
    public double SellMean { get => _sellMean; private set => Set(ref _sellMean, value); }
    public double BuyMean { get => _buyMean; private set => Set(ref _buyMean, value); }
    public long SellAmount { get => _sellAmount; private set => Set(ref _sellAmount, value); }
    public long BuyAmount { get => _buyAmount; private set => Set(ref _buyAmount, value); }

    // ---------- 历史图表 ----------

    private readonly LineSeries<DateTimePoint> _highestSeries = new() { Name = "Highest", GeometrySize = 0 };
    private readonly LineSeries<DateTimePoint> _averageSeries = new() { Name = "Average", GeometrySize = 0 };
    private readonly LineSeries<DateTimePoint> _lowestSeries = new() { Name = "Lowest", GeometrySize = 0 };
    private readonly LineSeries<DateTimePoint> _volumeSeries = new() { Name = "Volume", GeometrySize = 0 };
    private readonly Axis _xAxis;
    private readonly Axis _yAxis;

    private List<EVEStandard.Models.MarketRegionHistory>? _statistics;

    public ISeries[] PriceSeries { get; }

    public ISeries[] VolumeSeries { get; }

    public Axis[] XAxes { get; }

    public Axis[] YAxes { get; }

    private int _historyRangeIndex = DefaultHistoryRangeIndex;

    public int HistoryRangeIndex
    {
        get => _historyRangeIndex;
        set
        {
            if (Set(ref _historyRangeIndex, value))
            {
                ApplyStatistics();
            }
        }
    }

    // ---------- 计算器 ----------

    private double _calSalesTax = 3.6;
    private double _calAgentTax = 1;
    private double _calSellAmount = 1;
    private double _calSellPrice;
    private double _calSellResult;
    private bool _calSellImmediately;
    private double _calBuyAmount = 1;
    private double _calBuyPrice;
    private double _calBuyResult;
    private bool _autoSetBuyPrice = true;

    public double CalSalesTax { get => _calSalesTax; set { if (Set(ref _calSalesTax, value)) Recalculate(); } }
    public double CalAgentTax { get => _calAgentTax; set { if (Set(ref _calAgentTax, value)) Recalculate(); } }
    public double CalSellAmount { get => _calSellAmount; set { if (Set(ref _calSellAmount, value)) Recalculate(); } }
    public double CalSellPrice { get => _calSellPrice; private set => Set(ref _calSellPrice, value); }
    public double CalSellResult { get => _calSellResult; private set => Set(ref _calSellResult, value); }
    public bool CalSellImmediately { get => _calSellImmediately; set { if (Set(ref _calSellImmediately, value)) Recalculate(); } }
    public double CalBuyAmount { get => _calBuyAmount; set { if (Set(ref _calBuyAmount, value)) Recalculate(); } }
    public double CalBuyPrice { get => _calBuyPrice; private set => Set(ref _calBuyPrice, value); }
    public double CalBuyResult { get => _calBuyResult; private set => Set(ref _calBuyResult, value); }
    public bool AutoSetBuyPrice { get => _autoSetBuyPrice; set { if (Set(ref _autoSetBuyPrice, value)) Recalculate(); } }

    /// <summary>买入计算明细（逐档吃单），每行形如 "1,000 × 1,290,000.00 = 1,290,000,000.00"。</summary>
    public ObservableCollection<string> BuyCalcDetails { get; } = [];

    private bool _buyCalcShortOfOrders;

    /// <summary>卖单数量不足以满足买入量（剩余部分按最低卖价估算）。</summary>
    public bool BuyCalcShortOfOrders { get => _buyCalcShortOfOrders; private set => Set(ref _buyCalcShortOfOrders, value); }

    // ---------- 状态 ----------

    private bool _isLoading;
    private string _statusText = string.Empty;
    private string? _errorMessage;

    public bool IsLoading { get => _isLoading; private set => Set(ref _isLoading, value); }
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    public string? ErrorMessage { get => _errorMessage; private set => Set(ref _errorMessage, value); }

    public MarketPageViewModel()
    {
        _xAxis = new DateTimeAxis(TimeSpan.FromDays(1), d => d.ToString("yyyy.MM.dd")) { TextSize = 12 };
        _yAxis = new Axis { Labeler = v => v.ToString("N2"), TextSize = 12 };

        _highestSeries.Fill = null;
        _averageSeries.Fill = null;
        _lowestSeries.Fill = null;
        _volumeSeries.Fill = null;

        PriceSeries = [_highestSeries, _averageSeries, _lowestSeries];
        VolumeSeries = [_volumeSeries];
        XAxes = [_xAxis];
        YAxes = [_yAxis];

        ApplyThemeColors();

        // LiveCharts 的 Paint 是 SkiaSharp 对象，无法用 DynamicResource 跟随主题，
        // 因此订阅应用的主题切换事件重新着色（见 ThemeService.ThemeChanged）。
        ThemeService.ThemeChanged += ApplyThemeColors;
    }

    /// <summary>图表/坐标轴配色，全部取自主题资源；主题切换后重新调用。</summary>
    public void ApplyThemeColors()
    {
        if (MakePaint("SystemFillColorCriticalBrush", 2) is { } highest)
        {
            _highestSeries.Stroke = highest;
        }

        if (MakePaint("SystemAccentColorPrimaryBrush", 2) is { } average)
        {
            _averageSeries.Stroke = average;
        }

        if (MakePaint("SystemFillColorSuccessBrush", 2) is { } lowest)
        {
            _lowestSeries.Stroke = lowest;
        }

        if (MakePaint("TextFillColorSecondaryBrush", 2) is { } volume)
        {
            _volumeSeries.Stroke = volume;
        }

        if (MakePaint("TextFillColorSecondaryBrush", 1) is { } labelPaint)
        {
            _xAxis.LabelsPaint = labelPaint;
            _yAxis.LabelsPaint = labelPaint;
        }

        if (MakePaint("DividerStrokeColorDefaultBrush", 1) is { } separatorPaint)
        {
            _xAxis.SeparatorsPaint = separatorPaint;
            _yAxis.SeparatorsPaint = separatorPaint;
        }
    }

    private static SolidColorPaint? MakePaint(string resourceKey, double thickness)
    {
        if (Application.Current?.TryFindResource(resourceKey) is not SolidColorBrush brush)
        {
            return null;
        }

        var c = brush.Color;
        return new SolidColorPaint(new SKColor(c.R, c.G, c.B, c.A), (float)thickness);
    }

    // ---------- 初次加载 ----------

    public async Task LoadAsync()
    {
        if (TreeRoots.Count > 0)
        {
            return; // 页面实例常驻，树只建一次
        }

        IsLoading = true;
        StatusText = "加载市场分类…";
        try
        {
            var (roots, types) = await Task.Run(BuildTreeAsync);
            _allTypeItems.Clear();
            _allTypeItems.AddRange(types);
            TreeRoots.Clear();
            foreach (var root in roots)
            {
                TreeRoots.Add(root);
            }

            RefreshStaredItems();

            // 默认市场：伏尔戈（The Forge，吉他所在星域）。不用 MarketLocation(MapRegion) 构造，
            // 它对无星系星域会抛异常
            var defaultRegion = await Core.Services.DB.MapRegionService.QueryAsync(MarketOrderService.DefaultMarketRegion);
            if (defaultRegion is not null)
            {
                SelectedMarketLocation = new MarketLocation
                {
                    Type = MarketLocationType.Region,
                    Id = defaultRegion.RegionID,
                    MarketObj = defaultRegion,
                    Name = defaultRegion.RegionName,
                    RegionId = defaultRegion.RegionID,
                    SolarSystemId = Core.Services.DB.MapSolarSystemService.QueryByRegionID(defaultRegion.RegionID).FirstOrDefault()?.SolarSystemID ?? 0,
                };
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            StatusText = string.Empty;
        }
    }

    /// <summary>构建"市场分组 → 物品"两级以上的树；返回根节点与全部物品节点。</summary>
    private static async Task<(List<MarketItem> Roots, List<MarketItem> Types)> BuildTreeAsync()
    {
        var rootGroups = await Core.Services.DB.InvMarketGroupService.QueryRootGroupAsync();
        var subGroups = await Core.Services.DB.InvMarketGroupService.QuerySubGroupAsync();
        var types = await Core.Services.DB.InvTypeService.QueryMarketTypesAsync();

        var nodes = new Dictionary<int, MarketItem>();
        var roots = new List<MarketItem>();
        foreach (var group in rootGroups)
        {
            var node = new MarketItem { InvMarketGroup = group, Children = [] };
            nodes[group.MarketGroupID] = node;
            roots.Add(node);
        }

        var subNodes = new List<MarketItem>();
        foreach (var group in subGroups)
        {
            var node = new MarketItem { InvMarketGroup = group, Children = [] };
            nodes[group.MarketGroupID] = node;
            subNodes.Add(node);
        }

        foreach (var node in subNodes)
        {
            if (node.InvMarketGroup.ParentGroupID is { } parentId && nodes.TryGetValue(parentId, out var parent))
            {
                parent.Children!.Add(node);
                node.ParentGroup = parent;
            }
            else
            {
                roots.Add(node);
            }
        }

        var typeItems = new List<MarketItem>(types.Count);
        foreach (var type in types)
        {
            if (type.MarketGroupID is not { } groupId || !nodes.TryGetValue(groupId, out var groupNode))
            {
                continue;
            }

            var node = new MarketItem { InvType = type, ParentGroup = groupNode };
            groupNode.Children!.Add(node);
            typeItems.Add(node);
        }

        SortRecursive(roots);
        return (roots, typeItems);
    }

    private static void SortRecursive(List<MarketItem> items)
    {
        items.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
        foreach (var item in items)
        {
            if (item.Children is { Count: > 0 })
            {
                SortRecursive(item.Children);
            }
        }
    }

    // ---------- 选择 ----------

    private void ApplySearch()
    {
        SearchResults.Clear();
        var text = SearchText?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        foreach (var item in _allTypeItems)
        {
            if (item.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                SearchResults.Add(item);
                if (SearchResults.Count >= 60)
                {
                    break;
                }
            }
        }
    }

    /// <summary>收藏列表（保存在 Configs/StaredMarketInvType.json）。</summary>
    public void RefreshStaredItems()
    {
        StaredItems.Clear();
        var ids = MarketStarService.Current.GetIds();
        foreach (var id in ids)
        {
            var item = _allTypeItems.FirstOrDefault(p => p.InvType.TypeID == id);
            if (item is not null)
            {
                StaredItems.Add(item);
            }
        }

        if (SelectedInvType is not null)
        {
            Stared = MarketStarService.Current.IsStared(SelectedInvType.TypeID);
        }
    }

    /// <summary>切换当前物品收藏状态。</summary>
    public void ToggleStar()
    {
        if (SelectedInvType is null)
        {
            return;
        }

        var id = SelectedInvType.TypeID;
        Stared = MarketStarService.Current.IsStared(id)
            ? !MarketStarService.Current.Remove(id)
            : MarketStarService.Current.Add(id);
        RefreshStaredItems();
    }

    private async Task SelectInvTypeAsync(InvType type, bool forceRefresh = false)
    {
        SelectedInvType = type;
        Stared = MarketStarService.Current.IsStared(type.TypeID);
        SelectedInvTypeIcon = null;

        if (SelectedMarketLocation is null)
        {
            ErrorMessage = FindString("MarketPage_UnSelectedMarket");
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            StatusText = FindString("MarketPage_GettingOrder");
            List<MarketOrder>? orders;
            switch (SelectedMarketLocation.Type)
            {
                case MarketLocationType.Structure:
                    orders = await MarketOrderService.Current.GetStructureTypeOrdersAsync(SelectedMarketLocation.Id, type.TypeID);
                    break;
                case MarketLocationType.SolarSystem:
                    // 星系 = 星域订单按星系过滤（每物品仍只需该物品的星域页）
                    {
                        var regionOrders = await MarketOrderService.Current.GetRegionOrdersAsync(type.TypeID, SelectedMarketLocation.RegionId);
                        orders = regionOrders?.Where(p => p.SystemId == SelectedMarketLocation.SolarSystemId).ToList();
                    }

                    break;
                default:
                    orders = await MarketOrderService.Current.GetRegionOrdersAsync(type.TypeID, SelectedMarketLocation.RegionId);
                    break;
            }

            SellOrders.Clear();
            BuyOrders.Clear();
            if (orders is not null)
            {
                foreach (var order in orders.Where(p => !p.IsBuyOrder).OrderBy(p => p.Price))
                {
                    SellOrders.Add(order);
                }

                foreach (var order in orders.Where(p => p.IsBuyOrder).OrderByDescending(p => p.Price))
                {
                    BuyOrders.Add(order);
                }
            }
            else if (SelectedMarketLocation.Type == MarketLocationType.Structure)
            {
                // 建筑订单需要角色授权与该建筑的市场访问权
                ErrorMessage = FindString("MarketPage_StructureOrdersFailed");
            }

            SetOrderStatisticalInfo();

            // 历史统计是"星域级"的（与 WinUI 一致）：星系/建筑市场用各自所在星域
            var regionId = SelectedMarketLocation.RegionId;
            if (regionId > 0)
            {
                StatusText = FindString("MarketPage_GettingHistroy");
                _statistics = await MarketOrderService.Current.GetHistoryAsync(type.TypeID, regionId, forceRefresh);
            }
            else
            {
                _statistics = null;
            }

            ApplyStatistics();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            StatusText = string.Empty;
            _ = LoadIconAsync(type.TypeID);
        }

        Recalculate();
    }

    /// <summary>刷新当前物品的订单与历史（历史跳过缓存，订单本就直连 ESI）。</summary>
    public async Task RefreshAsync()
    {
        if (SelectedInvType is not null)
        {
            await SelectInvTypeAsync(SelectedInvType, forceRefresh: true);
        }
    }

    private async Task LoadIconAsync(int typeId)
    {
        try
        {
            var bytes = await Http
                .GetByteArrayAsync($"https://images.evetech.net/types/{typeId}/icon?size=64")
                .ConfigureAwait(false);

            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            if (SelectedInvType?.TypeID == typeId)
            {
                SelectedInvTypeIcon = bitmap;
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex); // 图标失败不影响页面
        }
    }

    // ---------- 统计与图表 ----------

    private void SetOrderStatisticalInfo()
    {
        SetSideStatistic(SellOrders, out var sell5P, out var sellMean, out var sellAmount);
        SetSideStatistic(BuyOrders, out var buy5P, out var buyMean, out var buyAmount);
        Sell5P = sell5P;
        SellMean = sellMean;
        SellAmount = sellAmount;
        Buy5P = buy5P;
        BuyMean = buyMean;
        BuyAmount = buyAmount;
    }

    /// <summary>5% 均价 / 全体均价 / 总量。卖单已按价升序、买单按价降序，故 5% 分别是最低与最高的 5%。</summary>
    private static void SetSideStatistic(IReadOnlyList<MarketOrder> orders, out double top5P, out double mean, out long amount)
    {
        if (orders.Count == 0)
        {
            top5P = 0;
            mean = 0;
            amount = 0;
            return;
        }

        var count = (int)(orders.Count * 0.05);
        top5P = count > 1
            ? orders.Take(count).Average(p => p.Price)
            : orders[0].Price;
        mean = orders.Average(p => p.Price);
        amount = orders.Sum(p => p.VolumeRemain);
    }

    private void ApplyStatistics()
    {
        var source = _statistics;
        if (source is null)
        {
            _highestSeries.Values = [];
            _averageSeries.Values = [];
            _lowestSeries.Values = [];
            _volumeSeries.Values = [];
            return;
        }

        var now = DateTime.Now;
        IEnumerable<EVEStandard.Models.MarketRegionHistory> filtered = HistoryRangeIndex switch
        {
            0 => source.Where(p => p.Date > now.AddMonths(-1)),
            1 => source.Where(p => p.Date > now.AddMonths(-3)),
            2 => source.Where(p => p.Date > now.AddMonths(-6)),
            3 => source.Where(p => p.Date > now.AddMonths(-12)),
            _ => source,
        };

        var rows = filtered.OrderBy(p => p.Date).ToList();
        _highestSeries.Values = rows.Select(p => new DateTimePoint(p.Date, p.Highest)).ToArray();
        _averageSeries.Values = rows.Select(p => new DateTimePoint(p.Date, p.Average)).ToArray();
        _lowestSeries.Values = rows.Select(p => new DateTimePoint(p.Date, p.Lowest)).ToArray();
        _volumeSeries.Values = rows.Select(p => new DateTimePoint(p.Date, p.Volume)).ToArray();
    }

    // ---------- 计算器 ----------

    private void Recalculate()
    {
        CalBuyPrice = SellOrders.Count > 0 ? SellOrders[0].Price : 0;
        CalSellPrice = CalSellImmediately
            ? (BuyOrders.Count > 0 ? BuyOrders[0].Price : 0)
            : CalBuyPrice;

        BuyCalcDetails.Clear();
        BuyCalcShortOfOrders = false;

        if (SellOrders.Count > 0)
        {
            if (AutoSetBuyPrice)
            {
                var remain = CalBuyAmount;
                var total = 0d;
                foreach (var order in SellOrders)
                {
                    if (remain <= 0)
                    {
                        break;
                    }

                    var take = Math.Min(remain, order.VolumeRemain);
                    var amount = take * order.Price;
                    total += amount;
                    remain -= take;
                    BuyCalcDetails.Add($"{take:N0} × {order.Price:N2} = {amount:N2}");
                }

                if (remain > 0)
                {
                    var amount = SellOrders[0].Price * remain;
                    total += amount;
                    BuyCalcDetails.Add($"{remain:N0} × {SellOrders[0].Price:N2} = {amount:N2}");
                    BuyCalcShortOfOrders = true;
                }

                CalBuyResult = total;
            }
            else
            {
                CalBuyResult = CalBuyPrice * CalBuyAmount;
                BuyCalcDetails.Add($"{CalBuyAmount:N0} × {CalBuyPrice:N2} = {CalBuyResult:N2}");
            }
        }

        var totalSell = CalSellPrice * CalSellAmount;
        totalSell *= CalSellImmediately
            ? (100 - CalSalesTax) / 100
            : (100 - CalSalesTax - CalAgentTax) / 100;
        CalSellResult = totalSell;
    }

    // ---------- INotifyPropertyChanged ----------

    public event PropertyChangedEventHandler? PropertyChanged;

    private string _searchText = string.Empty;

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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
