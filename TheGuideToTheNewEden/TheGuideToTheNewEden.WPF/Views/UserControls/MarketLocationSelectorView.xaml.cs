using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.Core.Models.Universe;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>
/// 市场位置选择器（独立控件）：按钮 + 下拉弹层，弹层内是 星域 / 星系 / 建筑三个页签（各带搜索），
/// 选中的位置经 <see cref="SelectedItem"/>（TwoWay）回写给调用方，选择后弹层自动收起。
/// 移植自 WinUI 版 <c>MarketLocationSelectorControl</c>（WinUI 由三个独立选择控件组合，这里内联实现）：
/// 三个列表都**直接列出全部可选项**（星系排除虫洞/希拉等特殊星系，与 WinUI 的 <c>ShowSpecial=False</c> 一致），
/// 搜索框只做就地过滤。星系有 8000+ 条，所以过滤用 <see cref="ICollectionView"/> 而不是重建集合。
/// <para>
/// 使用方式：<c>&lt;uc:MarketLocationSelectorView SelectedItem="{Binding Xxx, Mode=TwoWay}" /&gt;</c>，
/// 控件不得自设 DataContext（见 REFACTORING.md §9 第 21 条），弹层内列表就地绑定、外部 DP 绑定沿用页面上下文。
/// 建筑列表每次展开弹层都会重新读取（设置页增删建筑后无需重建页面）。
/// </para>
/// </summary>
public partial class MarketLocationSelectorView : UserControl
{
    private readonly List<MapRegion> _allRegions = [];
    private readonly List<MapSolarSystem> _allSystems = [];
    private readonly List<Structure> _allStructures = [];

    private ICollectionView? _regionsView;
    private ICollectionView? _systemsView;
    private ICollectionView? _structuresView;

    private bool _loaded;
    private bool _suppress;

    public MarketLocationSelectorView()
    {
        InitializeComponent();
        // 不设置 DataContext：列表在加载后直接赋给控件，SelectedItem 绑定需沿用外部（页面）的 DataContext
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        try
        {
            _allRegions.AddRange(await Core.Services.DB.MapRegionService.QueryAllAsync());

            var systems = await Core.Services.DB.MapSolarSystemService.QueryAllAsync();
            _allSystems.AddRange(systems.Where(p => !p.IsSpecial()).OrderBy(p => p.SolarSystemID));

            ReloadStructures();

            _regionsView = CreateView(_allRegions, o => o is MapRegion region && Matches(region.RegionName, RegionSearch.Text));
            _systemsView = CreateView(_allSystems, o => o is MapSolarSystem system && Matches(system.SolarSystemName, SystemSearch.Text));
            _structuresView = CreateView(_allStructures, o => o is Structure structure && Matches(structure.Name, StructureSearch.Text));

            RegionList.ItemsSource = _regionsView;
            SystemList.ItemsSource = _systemsView;
            StructureList.ItemsSource = _structuresView;

            PushSelectedIntoLists();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private static ICollectionView CreateView<T>(IEnumerable<T> source, Predicate<object> filter)
    {
        var view = new CollectionViewSource { Source = source }.View;
        view.Filter = filter;
        return view;
    }

    /// <summary>搜索词为空视为全部命中（列表默认展示全部）。</summary>
    private static bool Matches(string? name, string? keyword)
    {
        var text = keyword?.Trim();
        return string.IsNullOrEmpty(text)
            || (name ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>重新读取市场建筑列表（设置 → 玩家建筑可随时增删；列表就地刷新保持搜索与选中）。</summary>
    private void ReloadStructures()
    {
        _allStructures.Clear();
        _allStructures.AddRange(StructureService.GetMarketStrutures());
        _structuresView?.Refresh();
        SetValue(HasNoStructurePropertyKey, _allStructures.Count == 0);
    }

    /// <summary>把外部已选位置在对应列表里选中/高亮（_suppress 期间不会回写 SelectedItem）。</summary>
    private void PushSelectedIntoLists()
    {
        var selected = SelectedItem;
        _suppress = true;
        try
        {
            // 先清搜索，避免过滤把要高亮的项藏起来
            RegionSearch.Text = string.Empty;
            SystemSearch.Text = string.Empty;
            StructureSearch.Text = string.Empty;

            switch (selected?.Type)
            {
                case MarketLocationType.Region:
                    RegionList.SelectedItem = _allRegions.FirstOrDefault(p => p.RegionID == selected.Id);
                    break;
                case MarketLocationType.SolarSystem:
                    SystemList.SelectedItem = _allSystems.FirstOrDefault(p => p.SolarSystemID == selected.Id);
                    break;
                case MarketLocationType.Structure:
                    StructureList.SelectedItem = _allStructures.FirstOrDefault(p => p.Id == selected.Id);
                    break;
            }
        }
        finally
        {
            _suppress = false;
        }
    }

    private void SetSelected(MarketLocation location)
    {
        if (_suppress)
        {
            return;
        }

        SelectedItem = location;
        LocationToggle.IsChecked = false; // 选完自动收起弹层
    }

    private void OnRegionSearchChanged(object sender, TextChangedEventArgs e) => _regionsView?.Refresh();

    private void OnSystemSearchChanged(object sender, TextChangedEventArgs e) => _systemsView?.Refresh();

    private void OnStructureSearchChanged(object sender, TextChangedEventArgs e) => _structuresView?.Refresh();

    private void OnRegionSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is MapRegion region)
        {
            // 不用 MarketLocation(MapRegion) 构造：它对无星系星域会抛异常（跳数计算只需要一个代表星系）
            SetSelected(new MarketLocation
            {
                Type = MarketLocationType.Region,
                Id = region.RegionID,
                MarketObj = region,
                Name = region.RegionName,
                RegionId = region.RegionID,
                SolarSystemId = Core.Services.DB.MapSolarSystemService.QueryByRegionID(region.RegionID).FirstOrDefault()?.SolarSystemID ?? 0,
            });
        }
    }

    private void OnSystemSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is MapSolarSystem system)
        {
            SetSelected(new MarketLocation(system));
        }
    }

    private void OnStructureSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is Structure structure)
        {
            SetSelected(new MarketLocation(structure));
        }
    }

    private void OnLocationToggleClick(object sender, RoutedEventArgs e)
    {
        // 每次展开弹层都重读建筑列表（设置页可能刚增删过建筑）
        if (LocationToggle.IsChecked == true)
        {
            ReloadStructures();
        }
    }

    // ---------- 依赖属性 ----------

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem),
        typeof(MarketLocation),
        typeof(MarketLocationSelectorView),
        new PropertyMetadata(null, OnSelectedItemChanged));

    /// <summary>当前选中的市场位置。</summary>
    public MarketLocation? SelectedItem
    {
        get => (MarketLocation?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MarketLocationSelectorView)d;
        control.SetValue(HasSelectionPropertyKey, e.NewValue is MarketLocation);
        control.PushSelectedIntoLists();
        control.SelectedItemChanged?.Invoke(e.NewValue as MarketLocation);
    }

    private static readonly DependencyPropertyKey HasSelectionPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasSelection),
        typeof(bool),
        typeof(MarketLocationSelectorView),
        new PropertyMetadata(false));

    /// <summary>是否已选择市场（按钮占位文案与所选名称按它切换）。</summary>
    public bool HasSelection => (bool)GetValue(HasSelectionPropertyKey.DependencyProperty);

    private static readonly DependencyPropertyKey HasNoStructurePropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasNoStructure),
        typeof(bool),
        typeof(MarketLocationSelectorView),
        new PropertyMetadata(false));

    /// <summary>建筑列表为空（显示"请前往设置添加建筑"的引导）。</summary>
    public bool HasNoStructure => (bool)GetValue(HasNoStructurePropertyKey.DependencyProperty);

    private static readonly DependencyProperty PopupWidthProperty = DependencyProperty.Register(
        nameof(PopupWidth), typeof(double), typeof(MarketLocationSelectorView), new PropertyMetadata(320d));

    /// <summary>弹层宽度。</summary>
    public double PopupWidth
    {
        get => (double)GetValue(PopupWidthProperty);
        set => SetValue(PopupWidthProperty, value);
    }

    private static readonly DependencyProperty PopupHeightProperty = DependencyProperty.Register(
        nameof(PopupHeight), typeof(double), typeof(MarketLocationSelectorView), new PropertyMetadata(420d));

    /// <summary>弹层高度。</summary>
    public double PopupHeight
    {
        get => (double)GetValue(PopupHeightProperty);
        set => SetValue(PopupHeightProperty, value);
    }

    /// <summary>选中位置变化（TwoWay 绑定已足够回写，此事件供调用方做联动刷新）。</summary>
    public event Action<MarketLocation?>? SelectedItemChanged;
}
