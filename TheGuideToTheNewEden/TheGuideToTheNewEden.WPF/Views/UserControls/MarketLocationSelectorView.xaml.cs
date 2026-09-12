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
/// 市场位置选择器：星域 / 星系 / 建筑三个页签（各带搜索），选中的位置以 <see cref="SelectedItem"/> 暴露。
/// 移植自 WinUI 版 <c>MarketLocationSelectorControl</c>（WinUI 由三个独立选择控件组合，这里内联实现）：
/// 三个列表都**直接列出全部可选项**（星系排除虫洞/希拉等特殊星系，与 WinUI 的 <c>ShowSpecial=False</c> 一致），
/// 搜索框只做就地过滤。星系有 8000+ 条，所以过滤用 <see cref="ICollectionView"/> 而不是重建集合。
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

            _allStructures.AddRange(StructureService.GetMarketStrutures());

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

    /// <summary>把外部已选位置在列表中高亮（不触发 SelectedItem 回写）。</summary>
    private void PushSelectedIntoLists()
    {
        var selected = SelectedItem;
        if (selected is null)
        {
            return;
        }

        _suppress = true;
        try
        {
            switch (selected.Type)
            {
                case MarketLocationType.Region:
                    _regionsView?.Refresh();
                    break;
                case MarketLocationType.SolarSystem:
                    SystemSearch.Text = selected.Name;
                    _systemsView?.Refresh();
                    break;
                case MarketLocationType.Structure:
                    _structuresView?.Refresh();
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
        => ((MarketLocationSelectorView)d).SelectedItemChanged?.Invoke(e.NewValue as MarketLocation);

    /// <summary>选中位置变化（供外部收起弹层）。</summary>
    public event Action<MarketLocation?>? SelectedItemChanged;
}
