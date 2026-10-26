using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>
/// 星系选择器（与「价格市场」选择器同款交互）：按钮 + 悬浮弹层，弹层内是搜索框 + 虚拟化星系列表。
/// 选中后经 <see cref="SelectedItemChanged"/> 通知调用方（行星开发页收到后把该星系的行星全部加入候选池）。
/// 控件不自设 DataContext（REFACTORING.md §9 第 21 条），弹层内绑定直接在代码后置里赋值。
/// </summary>
public partial class SolarSystemPickerView : UserControl
{
    private readonly List<MapSolarSystem> _allSystems = [];
    private readonly Dictionary<int, string> _englishNames = new();
    private ICollectionView? _systemsView;
    private bool _loaded;
    private bool _suppress;

    public SolarSystemPickerView()
    {
        InitializeComponent();
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
            // 主库英文名一份（本地化后 SolarSystemName 会变成中文，搜索要中/英文都能命中）。
            foreach (var system in await MapSolarSystemService.QueryAllEnglishAsync())
            {
                _englishNames[system.SolarSystemID] = system.SolarSystemName;
            }

            var systems = await MapSolarSystemService.QueryAllAsync();
            _allSystems.Clear();
            // 默认与「价格市场」选择器一致：排除虫洞/特殊星系；需要虫洞的调用方显式设 IncludeSpecialSystems=true。
            _allSystems.AddRange(systems
                .Where(p => IncludeSpecialSystems || !p.IsSpecial())
                .OrderBy(p => p.SolarSystemID));
            _systemsView = new CollectionViewSource { Source = _allSystems }.View;
            _systemsView.Filter = o => o is MapSolarSystem system && Matches(system, SystemSearch.Text);
            SystemList.ItemsSource = _systemsView;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private bool Matches(MapSolarSystem system, string? keyword)
    {
        var text = keyword?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        _englishNames.TryGetValue(system.SolarSystemID, out var english);
        return (system.SolarSystemName ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase)
               || (english ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    private void OnSystemSearchChanged(object sender, TextChangedEventArgs e) => _systemsView?.Refresh();

    private void OnSystemSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress)
        {
            return;
        }

        if (e.AddedItems.Count > 0 && e.AddedItems[0] is MapSolarSystem system)
        {
            SelectedItem = system;
            PickerToggle.IsChecked = false;
            // 总是通知（同一星系重复选取时 DP 不变，仍要允许"重新加入"；调用方会按 PlanetId 去重）。
            SelectedItemChanged?.Invoke(system);

            // 清掉列表选中态，这样下次点同一星系仍会触发 SelectionChanged。
            _suppress = true;
            SystemList.SelectedItem = null;
            _suppress = false;
        }
    }

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem),
        typeof(MapSolarSystem),
        typeof(SolarSystemPickerView),
        new PropertyMetadata(null, OnSelectedItemChanged));

    public MapSolarSystem? SelectedItem
    {
        get => (MapSolarSystem?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SolarSystemPickerView)d;
        control.SetValue(HasSelectionPropertyKey, e.NewValue is MapSolarSystem);
    }

    private static readonly DependencyPropertyKey HasSelectionPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasSelection),
        typeof(bool),
        typeof(SolarSystemPickerView),
        new PropertyMetadata(false));

    /// <summary>是否已选过星系（决定按钮里显示占位文案还是星系名）。</summary>
    public bool HasSelection => (bool)GetValue(HasSelectionPropertyKey.DependencyProperty);

    /// <summary>是否把虫洞/特殊星系（<see cref="MapSolarSystem.IsSpecial"/>）也列出来；默认 false。</summary>
    public bool IncludeSpecialSystems
    {
        get => (bool)GetValue(IncludeSpecialSystemsProperty);
        set => SetValue(IncludeSpecialSystemsProperty, value);
    }

    public static readonly DependencyProperty IncludeSpecialSystemsProperty = DependencyProperty.Register(
        nameof(IncludeSpecialSystems),
        typeof(bool),
        typeof(SolarSystemPickerView),
        new PropertyMetadata(false));

    private static readonly DependencyProperty PopupWidthProperty = DependencyProperty.Register(
        nameof(PopupWidth), typeof(double), typeof(SolarSystemPickerView), new PropertyMetadata(300d));
    /// <summary>弹层宽度。</summary>
    public double PopupWidth
    {
        get => (double)GetValue(PopupWidthProperty);
        set => SetValue(PopupWidthProperty, value);
    }

    private static readonly DependencyProperty PopupHeightProperty = DependencyProperty.Register(
        nameof(PopupHeight), typeof(double), typeof(SolarSystemPickerView), new PropertyMetadata(400d));

    /// <summary>弹层高度。</summary>
    public double PopupHeight
    {
        get => (double)GetValue(PopupHeightProperty);
        set => SetValue(PopupHeightProperty, value);
    }

    /// <summary>选中星系变化（调用方据此把该星系的行星加入候选池）。</summary>
    public event Action<MapSolarSystem?>? SelectedItemChanged;
}
