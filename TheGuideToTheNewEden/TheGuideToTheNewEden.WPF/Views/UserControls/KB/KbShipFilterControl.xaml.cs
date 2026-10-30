using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WPF.Models.KB;
using TheGuideToTheNewEden.WPF.Services.KB;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.KB;

/// <summary>
/// 舰船分类树：勾选父节点会把其下全部具体舰船 TypeID 加入过滤列表。
/// 配置文件仍然只保存具体 TypeID（<see cref="IdName.CategoryEnum.InventoryType"/>）。
/// </summary>
public partial class KbShipFilterControl : UserControl
{
    private readonly Dictionary<int, ShipFilterNode> _leaves = [];
    private bool _suppressSelection;
    private bool _updatingFromTree;
    private bool _syncingMode;
    private bool _treeBuilt;

    public KbShipFilterControl()
    {
        InitializeComponent();
        ModeCombo.SelectionChanged += OnModeSelectionChanged;
        UpdateModeCombo();
        Loaded += OnLoaded;
    }

    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode),
        typeof(ZkbFilterMode),
        typeof(KbShipFilterControl),
        new FrameworkPropertyMetadata(
            ZkbFilterMode.None,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnModeChanged));

    public ZkbFilterMode Mode
    {
        get => (ZkbFilterMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>已选具体舰船 TypeID 列表（双向绑定到配置）。</summary>
    public static readonly DependencyProperty SelectedTypesProperty = DependencyProperty.Register(
        nameof(SelectedTypes),
        typeof(ObservableCollection<IdName>),
        typeof(KbShipFilterControl),
        new PropertyMetadata(null, OnSelectedTypesChanged));

    public ObservableCollection<IdName>? SelectedTypes
    {
        get => (ObservableCollection<IdName>?)GetValue(SelectedTypesProperty);
        set => SetValue(SelectedTypesProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_treeBuilt)
        {
            return;
        }

        _treeBuilt = true;
        try
        {
            var roots = ZkbShipTreeBuilder.Build();
            ShipTree.ItemsSource = roots;
            foreach (var root in roots)
            {
                RegisterLeaves(root);
            }

            ApplySelectedTypes();
            UpdateSelectedCount();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ShipTree.Items.Count > 0
                    && ShipTree.ItemContainerGenerator.ContainerFromIndex(0) is TreeViewItem rootItem)
                {
                    rootItem.IsExpanded = true;
                }
            }));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private static void OnSelectedTypesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (KbShipFilterControl)d;
        if (e.OldValue is ObservableCollection<IdName> oldItems)
        {
            oldItems.CollectionChanged -= control.OnSelectedTypesCollectionChanged;
        }
        if (e.NewValue is ObservableCollection<IdName> newItems)
        {
            newItems.CollectionChanged += control.OnSelectedTypesCollectionChanged;
        }

        control.ApplySelectedTypes();
        control.UpdateSelectedCount();
    }

    private void OnSelectedTypesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_suppressSelection || _updatingFromTree)
        {
            return;
        }

        _suppressSelection = true;
        try
        {
            var ids = SelectedTypes?.Select(p => p.Id).ToHashSet() ?? [];
            foreach (var leaf in _leaves.Values)
            {
                leaf.IsChecked = ids.Contains(leaf.TypeId);
            }
        }
        finally
        {
            _suppressSelection = false;
        }

        UpdateSelectedCount();
    }

    private void RegisterLeaves(ShipFilterNode node)
    {
        if (node.IsLeaf)
        {
            _leaves[node.TypeId] = node;
            node.CheckedChanged += OnLeafCheckedChanged;
            return;
        }

        foreach (var child in node.Children)
        {
            RegisterLeaves(child);
        }
    }

    private void ApplySelectedTypes()
    {
        if (!_treeBuilt)
        {
            return;
        }

        _suppressSelection = true;
        try
        {
            foreach (var leaf in _leaves.Values)
            {
                leaf.IsChecked = false;
            }

            if (SelectedTypes is not null)
            {
                foreach (var item in SelectedTypes)
                {
                    if (_leaves.TryGetValue(item.Id, out var leaf))
                    {
                        leaf.IsChecked = true;
                    }
                }
            }
        }
        finally
        {
            _suppressSelection = false;
        }
    }

    private void OnLeafCheckedChanged(ShipFilterNode node, bool? value)
    {
        if (_suppressSelection || SelectedTypes is null)
        {
            return;
        }

        _updatingFromTree = true;
        try
        {
            if (value == true)
            {
                if (!SelectedTypes.Any(p => p.Id == node.TypeId))
                {
                    SelectedTypes.Add(new IdName(node.TypeId, node.Name, IdName.CategoryEnum.InventoryType));
                }
            }
            else
            {
                var existing = SelectedTypes.FirstOrDefault(p => p.Id == node.TypeId);
                if (existing is not null)
                {
                    SelectedTypes.Remove(existing);
                }
            }
        }
        finally
        {
            _updatingFromTree = false;
        }

        UpdateSelectedCount();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var keyword = SearchBox.Text?.Trim() ?? string.Empty;
        if (keyword.Length == 0)
        {
            SearchResultHost.Visibility = Visibility.Collapsed;
            ShipTree.Visibility = Visibility.Visible;
            return;
        }

        var matches = _leaves.Values
            .Where(p => p.Name.Contains(keyword, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        SearchResultList.ItemsSource = matches;
        SearchResultHost.Visibility = Visibility.Visible;
        ShipTree.Visibility = Visibility.Collapsed;
    }

    private void OnRemoveSelectedClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: IdName item } && SelectedTypes is not null)
        {
            SelectedTypes.Remove(item);
        }
    }

    private void UpdateSelectedCount()
    {
        if (SelectedCountText is null)
        {
            return;
        }

        var count = SelectedTypes?.Count ?? 0;
        SelectedCountText.Text = string.Format(
            FindString("ZKBHomePage_Setting_Filter_ShipSelected"),
            count);
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;

    private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((KbShipFilterControl)d).UpdateModeCombo();

    private void UpdateModeCombo()
    {
        if (ModeCombo is null)
        {
            return;
        }

        _syncingMode = true;
        ModeCombo.SelectedIndex = (int)Mode;
        _syncingMode = false;
    }

    private void OnModeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingMode || ModeCombo.SelectedIndex < 0)
        {
            return;
        }

        Mode = (ZkbFilterMode)ModeCombo.SelectedIndex;
    }
}
