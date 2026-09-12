using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.Core.Models.Market;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>
/// 可勾选的市场物品树（分组三态 → 物品）。选中项以 <see cref="SelectedItems"/>（物品 TypeId 列表）对外暴露，
/// <see cref="SelectedItemsCount"/> 为已选数量。移植自 WinUI 版 <c>MarketSelecteTreeControl</c>。
/// </summary>
public partial class MarketSelecteTreeView : UserControl
{
    private readonly Dictionary<int, SelectableMarketItem> _marketItemsDict = [];
    private readonly List<SelectableMarketItem> _marketTypes = [];

    private bool _initStarted;
    private bool _initDone;
    private bool _stopSelectedChanged;

    /// <summary>根分组（供树绑定）。</summary>
    public ObservableCollection<SelectableMarketItem> Roots { get; } = [];

    /// <summary>搜索命中的物品（供搜索列表绑定）。</summary>
    public ObservableCollection<SelectableMarketItem> SearchResults { get; } = [];

    public MarketSelecteTreeView()
    {
        InitializeComponent();
        // 不设置 DataContext：本控件自身的列表用直接赋值，元素上的 SelectedItems 绑定需沿用外部（页面）的 DataContext
        TypeTree.ItemsSource = Roots;
        SearchList.ItemsSource = SearchResults;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initStarted)
        {
            return;
        }

        _initStarted = true;
        try
        {
            await InitAsync();
            _initDone = true;
            ApplySelectedItems(SelectedItems);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private async Task InitAsync()
    {
        var rootGroup = (await Core.Services.DB.InvMarketGroupService.QueryRootGroupAsync())
            .Select(p => new SelectableMarketItem { InvMarketGroup = p }).ToList();
        var subGroup = (await Core.Services.DB.InvMarketGroupService.QuerySubGroupAsync())
            .Select(p => new SelectableMarketItem { InvMarketGroup = p }).ToList();

        var allGroup = rootGroup.Concat(subGroup).ToList();
        var allGroupDic = allGroup.ToDictionary(p => p.InvMarketGroup.MarketGroupID);

        // 子分组挂到父分组
        foreach (var item in subGroup)
        {
            if (item.InvMarketGroup.ParentGroupID is { } parentId
                && allGroupDic.TryGetValue((int)parentId, out var parent))
            {
                parent.Children ??= [];
                parent.Children.Add(item);
                item.ParentGroup = parent;
            }
        }

        // 物品挂到"没有子分组的分组"（即叶子分组）
        var leafGroups = allGroup.Where(p => p.Children is null).ToDictionary(p => p.InvMarketGroup.MarketGroupID);
        foreach (var marketType in await Core.Services.DB.InvTypeService.QueryMarketTypesAsync())
        {
            if (marketType.MarketGroupID is not { } groupId || !leafGroups.TryGetValue(groupId, out var group))
            {
                continue;
            }

            group.Children ??= [];
            var node = new SelectableMarketItem { InvType = marketType, ParentGroup = group };
            group.Children.Add(node);
            _marketItemsDict[marketType.TypeID] = node;
            _marketTypes.Add(node);
        }

        Roots.Clear();
        foreach (var root in rootGroup)
        {
            Roots.Add(root);
        }

        foreach (var type in _marketTypes)
        {
            type.SelectedChanged += OnTypeSelectedChanged;
        }
    }

    private void OnTypeSelectedChanged(object? sender, bool? e)
    {
        if (_stopSelectedChanged || sender is not SelectableMarketItem { IsType: true } item)
        {
            return;
        }

        if (e == true)
        {
            if (!SelectedItems.Contains(item.Id))
            {
                SelectedItems.Add(item.Id);
            }
        }
        else
        {
            SelectedItems.Remove(item.Id);
        }

        SelectedItemsCount = SelectedItems.Count;
    }

    private void ApplySelectedItems(List<int>? types)
    {
        _stopSelectedChanged = true;
        try
        {
            foreach (var item in _marketTypes)
            {
                item.Selected = false;
            }

            if (types is not null)
            {
                foreach (var id in types)
                {
                    if (_marketItemsDict.TryGetValue(id, out var item))
                    {
                        item.Selected = true;
                    }
                    else
                    {
                        Core.Log.Error($"MarketSelecteTreeView: 未知物品类型 {id}");
                    }
                }
            }
        }
        finally
        {
            _stopSelectedChanged = false;
        }

        SelectedItemsCount = types?.Count ?? 0;
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var text = SearchBox.Text;
        SearchResults.Clear();
        if (string.IsNullOrEmpty(text))
        {
            SearchList.Visibility = Visibility.Collapsed;
            TypeTree.Visibility = Visibility.Visible;
            return;
        }

        foreach (var type in _marketTypes)
        {
            if (type.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                SearchResults.Add(type);
            }
        }

        SearchList.Visibility = Visibility.Visible;
        TypeTree.Visibility = Visibility.Collapsed;
    }

    private void OnSearchSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SearchList.SelectedItem is SelectableMarketItem item)
        {
            item.Selected = item.Selected != true;
            SearchList.SelectedIndex = -1;
        }
    }

    // ---------- 依赖属性 ----------

    public static readonly DependencyProperty SelectedItemsProperty = DependencyProperty.Register(
        nameof(SelectedItems),
        typeof(List<int>),
        typeof(MarketSelecteTreeView),
        new PropertyMetadata(new List<int>(), OnSelectedItemsChanged));

    /// <summary>已选物品的 TypeId 列表（TwoWay 绑定用；就地增删并回调数量变化）。</summary>
    public List<int> SelectedItems
    {
        get => (List<int>)GetValue(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
    }

    private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MarketSelecteTreeView)d;
        // 树尚未建好时跳过：InitAsync 完成后会用当前 DP 值补齐
        if (control._initDone)
        {
            control.ApplySelectedItems(e.NewValue as List<int>);
        }
    }

    public static readonly DependencyProperty SelectedItemsCountProperty = DependencyProperty.Register(
        nameof(SelectedItemsCount),
        typeof(int),
        typeof(MarketSelecteTreeView),
        new PropertyMetadata(0));

    public int SelectedItemsCount
    {
        get => (int)GetValue(SelectedItemsCountProperty);
        set => SetValue(SelectedItemsCountProperty, value);
    }
}
