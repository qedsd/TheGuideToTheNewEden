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
using TheGuideToTheNewEden.WPF.Services.Translation;
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
            if (Set(ref _selectedMarketLocation, value))
            {
                OnPropertyChanged(nameof(SelectedInvTypeMetaText));
                if (value is not null && SelectedInvType is not null)
                {
                    _ = SelectInvTypeAsync(SelectedInvType);
                }
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
                OnPropertyChanged(nameof(SelectedInvTypeMetaText));
            }
        }
    }

    public bool HasSelectedInvType => SelectedInvType is not null;

    public string SelectedInvTypeName => SelectedInvType?.TypeName ?? string.Empty;

    /// <summary>
    /// 物品描述（SDE 原文带 EVE 富文本标签，如 <c>&lt;color=…&gt;</c>/<c>&lt;br&gt;</c>），
    /// 用 <see cref="ChatMarkupProtector.ToPlainText"/> 剥离标签、解码实体后展示纯文本
    /// （与频道翻译/邮件正文同一套预清洗思路，REFACTORING 阶段 47 S5）。
    /// </summary>
    public string SelectedInvTypeDescription => ChatMarkupProtector.ToPlainText(SelectedInvType?.Description);

    public string SelectedInvTypeVolumeText => SelectedInvType is null ? string.Empty : $"{SelectedInvType.Volume:N2} m³";

    /// <summary>
    /// 物品头左侧的元信息行：体积 · 当前市场 · 买卖价差（"一眼可读"的附加信息，语言切换后重算）。
    /// </summary>
    public string SelectedInvTypeMetaText
    {
        get
        {
            if (SelectedInvType is null)
            {
                return string.Empty;
            }

            var market = SelectedMarketLocation is { Name: { Length: > 0 } name } ? name : string.Empty;
            var spread = _sellTop > 0 && _buyTop > 0
                ? $"{FindString("MarketPage_Spread")} {_sellTop - _buyTop:N0}"
                : string.Empty;

            return string.Join(" · ", new[] { SelectedInvTypeVolumeText, market, spread }.Where(p => p.Length > 0));
        }
    }

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
    private double _sellTop, _buyTop;

    public double Sell5P { get => _sell5P; private set => Set(ref _sell5P, value); }
    public double Buy5P { get => _buy5P; private set => Set(ref _buy5P, value); }
    public double SellMean { get => _sellMean; private set => Set(ref _sellMean, value); }
    public double BuyMean { get => _buyMean; private set => Set(ref _buyMean, value); }
    public long SellAmount { get => _sellAmount; private set => Set(ref _sellAmount, value); }
    public long BuyAmount { get => _buyAmount; private set => Set(ref _buyAmount, value); }

    /// <summary>最低卖价（列表按价升序，首行即最优卖价）。</summary>
    public double SellTop { get => _sellTop; private set => Set(ref _sellTop, value); }

    /// <summary>最高买价（列表按价降序，首行即最优买价）。</summary>
    public double BuyTop { get => _buyTop; private set => Set(ref _buyTop, value); }

    /// <summary>卖单笔数（订单池大小，与"数量"（总量）区分）。</summary>
    public int SellOrderCount => SellOrders.Count;

    /// <summary>买单笔数。</summary>
    public int BuyOrderCount => BuyOrders.Count;

    // ---------- 历史图表 ----------

    private readonly LineSeries<DateTimePoint> _highestSeries = new() { Name = "Highest", GeometrySize = 0 };
    private readonly LineSeries<DateTimePoint> _averageSeries = new() { Name = "Average", GeometrySize = 0 };
    private readonly LineSeries<DateTimePoint> _lowestSeries = new() { Name = "Lowest", GeometrySize = 0 };
    private readonly LineSeries<DateTimePoint> _volumeSeries = new() { Name = "Volume", GeometrySize = 0 };
    private readonly CandlesticksSeries<FinancialPoint> _candleSeries = new() { IsVisibleAtLegend = false };
    private readonly LineSeries<DateTimePoint> _ma5Series = new() { Name = "MA5", GeometrySize = 0 };
    private readonly LineSeries<DateTimePoint> _ma20Series = new() { Name = "MA20", GeometrySize = 0 };
    private readonly Axis _xAxis;
    private readonly Axis _yAxis;

    private List<EVEStandard.Models.MarketRegionHistory>? _statistics;

    public ISeries[] PriceSeries { get; }

    public ISeries[] VolumeSeries { get; }

    /// <summary>箱体（K线）视图：蜡烛 + MA5/MA20 均线。</summary>
    public ISeries[] CandleSeries { get; }

    public Axis[] XAxes { get; }

    public Axis[] YAxes { get; }

    // ---------- 价格页签图表显示模式（曲线 / 箱体） ----------

    private int _priceChartModeIndex;

    /// <summary>价格页签图表模式：0 = 高低平均曲线，1 = 箱体（K线）。</summary>
    public int PriceChartModeIndex
    {
        get => _priceChartModeIndex;
        set
        {
            if (Set(ref _priceChartModeIndex, value))
            {
                OnPropertyChanged(nameof(ActivePriceSeries));
            }
        }
    }

    /// <summary>价格页签当前显示的系列集合（随 <see cref="PriceChartModeIndex"/> 切换）。</summary>
    public ISeries[] ActivePriceSeries => PriceChartModeIndex == 1 ? CandleSeries : PriceSeries;

    // ---------- 历史区间统计（参考行情软件：涨跌幅 / 最高 / 最低 / 均价 / 累计成交） ----------

    private double _historyChangePercent;
    private double _historyHigh;
    private double _historyLow;
    private double _historyMean;
    private long _historyVolumeTotal;

    /// <summary>区间涨跌幅百分比（区间首日均价 → 末日均价，范围跟随时间下拉）。</summary>
    public double HistoryChangePercent { get => _historyChangePercent; private set => Set(ref _historyChangePercent, value); }

    /// <summary>涨跌方向：涨（≥0）显示成功色，跌显示危险色。</summary>
    public bool HistoryRising => HistoryChangePercent >= 0;

    public string HistoryChangePercentText => $"{HistoryChangePercent:+0.0;-0.0;0.0}%";

    public double HistoryHigh { get => _historyHigh; private set => Set(ref _historyHigh, value); }

    public double HistoryLow { get => _historyLow; private set => Set(ref _historyLow, value); }

    public double HistoryMean { get => _historyMean; private set => Set(ref _historyMean, value); }

    public long HistoryVolumeTotal { get => _historyVolumeTotal; private set => Set(ref _historyVolumeTotal, value); }

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
        _ma5Series.Fill = null;
        _ma20Series.Fill = null;

        // 箱体悬浮提示默认用 :C2（货币格式），中文区域会带 ￥ 前缀；改纯数字格式
        _candleSeries.YToolTipLabelFormatter = p =>
        {
            var c = p.Coordinate;
            return
                $"H {c.PrimaryValue:N2}{Environment.NewLine}" +
                $"O {c.TertiaryValue:N2}{Environment.NewLine}" +
                $"C {c.QuaternaryValue:N2}{Environment.NewLine}" +
                $"L {c.QuinaryValue:N2}";
        };

        PriceSeries = [_highestSeries, _averageSeries, _lowestSeries];
        VolumeSeries = [_volumeSeries];
        CandleSeries = [_candleSeries, _ma5Series, _ma20Series];
        XAxes = [_xAxis];
        YAxes = [_yAxis];

        ApplyThemeColors();

        // LiveCharts 的 Paint 是 SkiaSharp 对象，无法用 DynamicResource 跟随主题，
        // 因此订阅应用的主题切换事件重新着色（见 ThemeService.ThemeChanged）。
        ThemeService.ThemeChanged += ApplyThemeColors;

        // 其他界面（频道查价结果窗等）请求“跳到市场页并选中某物品”
        Navigation.MarketTypeSelectionRequested += ApplyMarketTypeSelection;
    }

    /// <summary>
    /// 选中指定物品（跨页面跳转用）。市场树还没建好时先记下，等 <see cref="LoadAsync"/> 建完树再应用。
    /// </summary>
    public void ApplyMarketTypeSelection(long typeId)
    {
        if (typeId <= 0)
        {
            return;
        }

        if (_allTypeItems.Count == 0)
        {
            _pendingMarketTypeId = typeId;
            return;
        }

        var item = _allTypeItems.FirstOrDefault(p => p.InvType?.TypeID == typeId);
        if (item is not null)
        {
            SelectedInvTypeItem = item;
        }
    }

    private long? _pendingMarketTypeId;

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

        // 箱体（K线）：涨=成功色、跌=危险色；均线 MA5=强调色、MA20=次要色
        if (MakePaint("SystemFillColorSuccessBrush", 1) is { } up)
        {
            _candleSeries.UpStroke = up;
        }

        if (MakePaint("SystemFillColorCriticalBrush", 1) is { } down)
        {
            _candleSeries.DownStroke = down;
        }

        if (MakePaint("SystemAccentColorPrimaryBrush", 2) is { } ma5)
        {
            _ma5Series.Stroke = ma5;
        }

        if (MakePaint("TextFillColorSecondaryBrush", 2) is { } ma20)
        {
            _ma20Series.Stroke = ma20;
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

            // 树已就绪：应用"从其他界面跳转过来时待选中的物品"（市场页首次创建时事件已错过，这里兜底）
            var pending = _pendingMarketTypeId ?? Navigation.TakePendingMarketType();
            if (pending is > 0)
            {
                _pendingMarketTypeId = null;
                ApplyMarketTypeSelection(pending.Value);
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

        // 集合是 ObservableCollection，属性是计算/字段投影，必须显式通知（§9 第 20 条）
        SellTop = SellOrders.Count > 0 ? SellOrders[0].Price : 0;
        BuyTop = BuyOrders.Count > 0 ? BuyOrders[0].Price : 0;
        OnPropertyChanged(nameof(SellOrderCount));
        OnPropertyChanged(nameof(BuyOrderCount));
        OnPropertyChanged(nameof(SelectedInvTypeMetaText));
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
            _candleSeries.Values = [];
            _ma5Series.Values = [];
            _ma20Series.Values = [];
            ResetHistoryStats();
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

        // 箱体（K线）：收盘=当日均价，开盘=前一日均价（首日取其自身均价），高/低=当日最高/最低
        _candleSeries.Values = rows.Select((p, i) => new FinancialPoint
        {
            Date = p.Date,
            High = p.Highest,
            Low = p.Lowest,
            Close = p.Average,
            Open = i == 0 ? p.Average : rows[i - 1].Average,
        }).ToArray();

        _ma5Series.Values = MovingAverageSeries(rows, 5);
        _ma20Series.Values = MovingAverageSeries(rows, 20);

        if (rows.Count == 0)
        {
            ResetHistoryStats();
            return;
        }

        var first = rows[0];
        var last = rows[^1];
        HistoryChangePercent = first.Average != 0
            ? (last.Average - first.Average) / first.Average * 100
            : 0;
        HistoryHigh = rows.Max(p => p.Highest);
        HistoryLow = rows.Min(p => p.Lowest);
        HistoryMean = rows.Average(p => p.Average);
        HistoryVolumeTotal = rows.Sum(p => p.Volume);
        OnPropertyChanged(nameof(HistoryRising));
        OnPropertyChanged(nameof(HistoryChangePercentText));
    }

    /// <summary>简单移动平均：窗口不满时取 null（折线断开，不显示半截均线）。</summary>
    private static DateTimePoint[] MovingAverageSeries(IReadOnlyList<EVEStandard.Models.MarketRegionHistory> rows, int window)
    {
        var result = new DateTimePoint[rows.Count];
        var sum = 0d;
        for (var i = 0; i < rows.Count; i++)
        {
            sum += rows[i].Average;
            if (i >= window)
            {
                sum -= rows[i - window].Average;
            }

            result[i] = i + 1 >= window
                ? new DateTimePoint(rows[i].Date, sum / window)
                : new DateTimePoint(rows[i].Date, null);
        }

        return result;
    }

    private void ResetHistoryStats()
    {
        HistoryChangePercent = 0;
        HistoryHigh = 0;
        HistoryLow = 0;
        HistoryMean = 0;
        HistoryVolumeTotal = 0;
        OnPropertyChanged(nameof(HistoryRising));
        OnPropertyChanged(nameof(HistoryChangePercentText));
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
