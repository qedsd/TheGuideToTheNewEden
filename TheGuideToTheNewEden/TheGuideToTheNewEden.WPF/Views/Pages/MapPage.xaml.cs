using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TheGuideToTheNewEden.WPF.Services.Map;
using TheGuideToTheNewEden.WPF.ViewModels.Map;
using TheGuideToTheNewEden.WPF.Views.UserControls.Map;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 星图页：SkiaSharp 深空画布 + 顶栏工具（搜索 / 星域定位 / 着色[安等·主权·行星资源·击杀·通行] / 跳桥 / 角色 / 情报 / 工具 / 导航）
/// + HUD 浮层（悬停提示、情报流、选中星系信息卡）。
/// 页面常驻缓存（NavigationCacheMode=Required），数据只装载一次；装载失败可重试（靠 IsLoaded 判定，不再置"已初始化"）。
/// </summary>
public partial class MapPage : Page
{
    private readonly MapPageViewModel _viewModel = new();
    private bool _initialized;

    private ToolWindow? _coverWindow;
    private ToolWindow? _resourceWindow;
    private ToolWindow? _bridgeWindow;
    private ToolWindow? _sovWindow;
    private ToolWindow? _detailWindow;
    private ToolWindow? _intelWindow;

    public MapPage()
    {
        InitializeComponent();
        DataContext = _viewModel;

        MapCanvas.SelectedChanged += MapCanvas_SelectedChanged;
        MapCanvas.HoveredChanged += MapCanvas_HoveredChanged;

        _viewModel.IntelMarkersChanged += (_, markers) => MapCanvas.SetIntel(markers);
        _viewModel.ColorModeChanged += async (_, mode) => await ApplyColorModeAsync(mode);
        _viewModel.ResourceKindChanged += async (_, _) => await ApplyColorModeAsync(_viewModel.ColorMode);
        _viewModel.ShowCharactersChanged += (_, enabled) => SetCharactersEnabled(enabled);
        _viewModel.BridgesChanged += (_, _) => ApplyBridges();
        _viewModel.NodeStatesChanged += (_, _) => MapCanvas.RefreshNodeStates();
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        Services.ThemeService.ThemeChanged += ApplyTheme;
        ApplyTheme();
        _viewModel.CoverChanged += (_, ids) => MapCanvas.SetCover(ids);
        _viewModel.IntelShipImageLoaded += (_, e) => MapCanvas.SetIntelShipImage(e.ShipTypeId, e.Bitmap);
        _viewModel.PortraitLoaded += (_, e) => MapCanvas.SetCharacterImage(e.CharacterId, e.Bitmap);
        _viewModel.NavigationCompleted += (_, _) => MapCanvas.SetRoute(_viewModel.LastPath, _viewModel.LastWaypointIndices);
        _viewModel.StatisticsLoaded += (_, _) => _ = ApplyColorModeAsync(_viewModel.ColorMode);

        CharacterLocationService.Current.LocationsUpdated += CharacterLocationsUpdated;
        Loaded += MapPage_Loaded;
        Unloaded += MapPage_Unloaded;
    }

    private async void MapPage_Loaded(object sender, RoutedEventArgs e)
    {
        // 页面常驻缓存：只装载一次；失败时不置 IsLoaded，下次进入会重试
        if (_viewModel.IsLoaded || _viewModel.IsLoading)
        {
            return;
        }

        await _viewModel.LoadAsync(async (nodes, links) =>
        {
            MapCanvas.SetData(nodes, links);
            _viewModel.AllNodes = nodes;
        });

        if (!_viewModel.IsLoaded)
        {
            return;
        }

        _initialized = true;
        BridgesToggle.IsChecked = _viewModel.ShowBridges;
        var kindIndex = Array.IndexOf(MapPageViewModel.ResourceKinds, _viewModel.ResourceKind);
        if (kindIndex >= 0)
        {
            ResourceKindCombo.SelectedIndex = kindIndex;
        }

        ApplyBridges();
        await ApplyColorModeAsync(_viewModel.ColorMode);
    }

    /// <summary>
    /// 页面切走时关掉"跟着页面上下文"的工具窗（一跳覆盖 / 行星资源清单 / 跳桥 / 主权分组 / 星系详情——
    /// 它们引用页面选中态，留着会陈旧）；可复用窗口先解除"关闭即隐藏"再关。
    /// **情报窗不在这里关**：情报监听是后台能力，切页面要保留（用户要求），窗口与监听一并保留；
    /// 只有用户主动关窗或取消顶栏勾选才会停止监听。
    /// </summary>
    private void MapPage_Unloaded(object sender, RoutedEventArgs e)
    {
        foreach (var window in new[] { _coverWindow, _resourceWindow, _bridgeWindow, _sovWindow, _detailWindow })
        {
            if (window is null)
            {
                continue;
            }

            window.AllowClose();
            window.Close();
        }

        NavPopup.IsOpen = false;
        SearchPopup.IsOpen = false;
        ToolsPopup.IsOpen = false;
        FilterPopup.IsOpen = false;
    }

    // ---------- 画布交互 ----------

    private void MapCanvas_SelectedChanged(object? sender, MapSystemNode? node)
    {
        if (node is null)
        {
            _viewModel.SetSelectedSystem(null, []);
            return;
        }

        var neighbors = MapCanvas.GetNeighbors(node.Id)
            .Select(id => MapCanvas.TryGetNode(id, out var neighbor) ? neighbor : null)
            .Where(p => p is not null)
            .Cast<MapSystemNode>()
            .ToList();
        _viewModel.SetSelectedSystem(node, neighbors);
    }

    private void MapCanvas_HoveredChanged(object? sender, MapSystemNode? node)
    {
        if (node is null)
        {
            HoverHud.Visibility = Visibility.Collapsed;
            return;
        }

        var secText = node.Security <= 0 ? "0.0" : node.Security.ToString("0.0");
        HoverHudText.Text = $"{node.RegionName}  {node.Name}  {secText}";
        HoverHud.Visibility = Visibility.Visible;
    }

    private void Locate(int systemId)
    {
        MapCanvas.ToSystem(systemId);
        if (MapCanvas.TryGetNode(systemId, out var node) && node is not null)
        {
            var neighbors = MapCanvas.GetNeighbors(systemId)
                .Select(id => MapCanvas.TryGetNode(id, out var n) ? n : null)
                .Where(p => p is not null)
                .Cast<MapSystemNode>()
                .ToList();
            _viewModel.SetSelectedSystem(node, neighbors);
        }
    }

    /// <summary>定位到星域（情报表格的"星域"列点击用）。</summary>
    private void LocateRegion(int regionId) => MapCanvas.ToRegion(regionId);

    // ---------- 顶栏 ----------

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _viewModel.Search(SearchBox.Text);
        SearchPopup.IsOpen = SearchBox.Text.Length >= 2 && _viewModel.SearchResults.Count > 0;
    }

    private void SearchList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: MapSystemNode node })
        {
            SearchPopup.IsOpen = false;
            SearchBox.Clear();
            Locate(node.Id);
        }
    }

    private void RegionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RegionCombo.SelectedItem is Core.DBModels.MapRegion region)
        {
            MapCanvas.ToRegion(region.RegionID);
        }
    }

    private void ColorMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var index = ColorModeCombo.SelectedIndex;
        if (!_initialized || index < 0 || index >= MapPageViewModel.ColorModes.Length)
        {
            return;
        }

        _viewModel.ColorMode = MapPageViewModel.ColorModes[index];
    }

    private void ResourceKind_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var index = ResourceKindCombo.SelectedIndex;
        if (!_initialized || index < 0 || index >= MapPageViewModel.ResourceKinds.Length)
        {
            return;
        }

        _viewModel.ResourceKind = MapPageViewModel.ResourceKinds[index];
    }

    /// <summary>按着色模式准备数据（主权要拉 ESI、行星资源要读本地库）后重画。</summary>
    private async Task ApplyColorModeAsync(MapColorMode mode)
    {
        ResourceKindCombo.Visibility = mode == MapColorMode.PlanetResource ? Visibility.Visible : Visibility.Collapsed;

        if (mode == MapColorMode.Sovereignty)
        {
            await _viewModel.ApplySovAsync();
        }
        else if (mode == MapColorMode.PlanetResource)
        {
            await _viewModel.ApplyResourceAsync(_viewModel.ResourceKind);
        }
        else if (mode != MapColorMode.Security)
        {
            _viewModel.FillHeat(_viewModel.AllNodes ?? [], mode);
        }

        MapCanvas.SetColorMode(mode, _viewModel.KillsMax, _viewModel.JumpsMax, _viewModel.ResourceMax);
    }

    private void ResetView_Click(object sender, RoutedEventArgs e)
    {
        MapCanvas.Fit();
    }

    private void BridgesToggle_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.ShowBridges = BridgesToggle.IsChecked == true;
    }

    private void ApplyBridges()
    {
        BridgesToggle.IsChecked = _viewModel.ShowBridges;
        MapCanvas.SetBridges(_viewModel.GetBridgePairs(), _viewModel.ShowBridges);
    }

    // ---------- 星域 / 安等筛选 ----------

    private void FilterButton_Click(object sender, RoutedEventArgs e) => FilterPopup.IsOpen = !FilterPopup.IsOpen;

    private void FilterApply_Click(object sender, RoutedEventArgs e)
    {
        FilterPopup.IsOpen = false;
        _viewModel.ApplySystemFilter();
    }

    private void FilterClear_Click(object sender, RoutedEventArgs e)
    {
        FilterPopup.IsOpen = false;
        _viewModel.ClearSystemFilter();
    }

    // ---------- 工具菜单 ----------

    private void ToolsButton_Click(object sender, RoutedEventArgs e) => ToolsPopup.IsOpen = !ToolsPopup.IsOpen;

    private void OneJumpCover_Click(object sender, RoutedEventArgs e)
    {
        ToolsPopup.IsOpen = false;
        if (_coverWindow is null)
        {
            var view = new OneJumpCoverView();
            view.SetCenter(_viewModel.SelectedSystem);
            view.ViewModel.CoverChanged += (_, ids) => MapCanvas.SetCover(ids);
            _coverWindow = CreateToolWindow(view, "MapPage_Tool_Cover", 900, 620);
            _coverWindow.Closed += (_, _) => _coverWindow = null;
        }
        else if (_coverWindow.GetContent() is OneJumpCoverView existing)
        {
            existing.SetCenter(_viewModel.SelectedSystem);
        }

        _coverWindow.Show();
        _coverWindow.Activate();
    }

    private void PlanetResourceList_Click(object sender, RoutedEventArgs e)
    {
        ToolsPopup.IsOpen = false;
        if (_resourceWindow is null)
        {
            _resourceWindow = CreateToolWindow(new PlanetResourceListView(), "MapPage_Tool_Resource", 1000, 680);
            _resourceWindow.Closed += (_, _) => _resourceWindow = null;
        }

        _resourceWindow.Show();
        _resourceWindow.Activate();
    }

    private void JumpBridgeSetting_Click(object sender, RoutedEventArgs e)
    {
        ToolsPopup.IsOpen = false;
        if (_bridgeWindow is null)
        {
            var view = new JumpBridgeSettingView();
            view.BridgesChanged += (_, _) =>
            {
                _viewModel.RefreshBridges();
                ApplyBridges();
            };
            _bridgeWindow = CreateToolWindow(view, "MapPage_Tool_Bridge", 780, 560);
            _bridgeWindow.Closed += (_, _) => _bridgeWindow = null;
        }

        _bridgeWindow.Show();
        _bridgeWindow.Activate();
    }

    private void SovGroupSetting_Click(object sender, RoutedEventArgs e)
    {
        ToolsPopup.IsOpen = false;
        if (_sovWindow is null)
        {
            var view = new SovGroupSettingView();
            view.GroupsSaved += async (_, _) =>
            {
                await _viewModel.ApplySovAsync();
                if (_viewModel.ColorMode == MapColorMode.Sovereignty)
                {
                    MapCanvas.SetColorMode(MapColorMode.Sovereignty, _viewModel.KillsMax, _viewModel.JumpsMax, _viewModel.ResourceMax);
                }
            };
            _sovWindow = CreateToolWindow(view, "MapPage_Tool_Sov", 760, 620);
            _sovWindow.Closed += (_, _) => _sovWindow = null;
        }

        _sovWindow.Show();
        _sovWindow.Activate();
    }

    private void SystemDetail_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedSystem is not { } node)
        {
            return;
        }

        // 详情窗口内容是"当前星系"，换星系要重建 → 不用 CloseToHide（真关 + 重建）
        _detailWindow?.Close();
        _detailWindow = CreateToolWindow(new MapSystemDetailView(node, _viewModel), "MapPage_Detail", 900, 640, reusable: false);
        _detailWindow.Closed += (_, _) => _detailWindow = null;
        _detailWindow.Show();
        _detailWindow.Activate();
    }

    /// <summary>
    /// 建工具窗（标题栏样式由 <see cref="ToolWindow"/> 自己定义，这里只给标题与尺寸）。
    /// 默认 <see cref="ToolWindow.SetCloseToHide"/>：点 X 只是隐藏，**窗口实例复用**，不再每次点开都 new 一个；
    /// 页面 <c>Unloaded</c> 时统一 <see cref="ToolWindow.AllowClose"/> 再真关。
    /// 内容随对象变化的窗口（星系详情）传 <paramref name="reusable"/>=false，走"真关 + 重建"。
    /// </summary>
    private ToolWindow CreateToolWindow(object content, string titleKey, int width, int height, bool reusable = true)
    {
        var title = FindString(titleKey);
        var window = new ToolWindow(
            content,
            ToolWindowTitleStyle.Default,
            showTopmostButton: false,
            showInTaskbar: true,
            width: width,
            height: height)
        {
            Owner = Window.GetWindow(this),
            DisplayTitle = title,
            SystemTitle = title,
        };

        if (reusable)
        {
            window.SetCloseToHide();
        }

        return window;
    }

    // ---------- 角色标记 ----------

    private void CharactersToggle_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.ShowCharacters = CharactersToggle.IsChecked == true;
    }

    private void SetCharactersEnabled(bool enabled)
    {
        if (enabled)
        {
            _viewModel.ResetPortraits();
            CharacterLocationService.Current.Start();
        }
        else
        {
            CharacterLocationService.Current.Stop();
            MapCanvas.SetCharacters([]);
        }
    }

    private void CharacterLocationsUpdated(object? sender, IReadOnlyList<CharacterLocationService.CharacterLocation> locations)
    {
        _viewModel.OnCharacterLocations(locations);
        var markers = locations.Select(p => new CharacterMarker
        {
            CharacterId = p.Character.CharacterID,
            Name = p.Character.CharacterName,
            SystemId = p.SystemId,
        }).ToList();
        Dispatcher.BeginInvoke(() =>
        {
            if (_viewModel.ShowCharacters)
            {
                MapCanvas.SetCharacters(markers);
            }
        });
    }

    // ---------- 情报 ----------

    /// <summary>顶栏"情报"开关：既是监听总闸，也负责开关情报工具窗口。</summary>
    private void IntelToggle_Changed(object sender, RoutedEventArgs e)
    {
        var enabled = IntelToggle.IsChecked == true;
        _viewModel.RefreshIntelAvailability();

        if (enabled)
        {
            // 没有频道预警会话也能开：ZKB 击杀不依赖会话（工具窗会提示"仅 ZKB"）
            _viewModel.StartIntel();
            ShowIntelTool();
        }
        else
        {
            _viewModel.SaveIntelConfig();
            _viewModel.StopIntel();
            _intelWindow?.Close();
        }
    }

    private void IntelTool_Click(object sender, RoutedEventArgs e)
    {
        ToolsPopup.IsOpen = false;
        _viewModel.RefreshIntelAvailability();
        ShowIntelTool();
    }

    /// <summary>
    /// 打开（或前置）情报工具窗口。窗口是"真关闭"的（不用 CloseToHide）：
    /// 点 X → 真正销毁 → 顺带把顶栏"情报"复选框取消勾选（复选框 = 监听总闸，于是监听一起停），
    /// 反之取消勾选 / 在窗口里点"停止"也会把窗口关掉——窗口与复选框始终一致。
    /// 但**切换页面时两者都保留**（窗口不随页面卸载关闭，监听继续）。
    /// </summary>
    private void ShowIntelTool()
    {
        if (_intelWindow is null)
        {
            _intelWindow = CreateToolWindow(new IntelToolView(_viewModel, Locate, LocateRegion), "MapPage_Intel", 1180, 700, reusable: false);
            // 情报窗不需要最大化/置顶：只留最小化与关闭（ToolWindow 的精细按钮控制）
            _intelWindow.SetVisibleTitleBarButtons(ToolWindowButtons.Minimize | ToolWindowButtons.Close);
            _intelWindow.Closed += (_, _) =>
            {
                _intelWindow = null;
                IntelToggle.IsChecked = false;
            };
        }

        _intelWindow.Show();
        _intelWindow.Activate();
    }

    /// <summary>顶栏"情报"复选框跟随监听状态（例如在情报窗里点了"停止"时）。</summary>
    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapPageViewModel.IntelRunning))
        {
            IntelToggle.IsChecked = _viewModel.IntelRunning;
        }
    }

    // ---------- 导航 ----------

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RefreshIntelAvailability();
        NavPopup.IsOpen = true;
    }

    private void AddWaypoint_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddWaypoint(_viewModel.SelectedSystem);
    }

    private void AddAvoid_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddAvoid(_viewModel.SelectedSystem);
    }

    private void RemoveWaypoint_Click(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is MapSystemNode node)
        {
            _viewModel.RemoveWaypoint(node);
        }
    }

    private void RemoveAvoid_Click(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is MapSystemNode node)
        {
            _viewModel.RemoveAvoid(node);
        }
    }

    private void CapitalToggle_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.CapitalMode = CapitalToggle.IsChecked == true;
    }

    private async void ComputeRoute_Click(object sender, RoutedEventArgs e)
    {
        NavErrorText.Text = string.Empty;
        Services.PageNotifyService.ShowWaiting(FindString("MapPage_Loading"));
        try
        {
            var ok = await _viewModel.NavigateAsync();
            if (!ok)
            {
                NavErrorText.Text = _viewModel.LastNavigationError ?? string.Empty;
            }
        }
        finally
        {
            Services.PageNotifyService.HideWaiting();
        }
    }

    private void ClearRoute_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearNavigation();
        MapCanvas.ClearRoute();
        NavErrorText.Text = string.Empty;
    }

    private void NavResult_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: NavResultItem item })
        {
            Locate(item.Node.Id);
        }
    }

    private async void SetAutopilot_Click(object sender, RoutedEventArgs e)
    {
        NavErrorText.Text = string.Empty;
        Services.PageNotifyService.ShowWaiting(FindString("MapPage_SetInGame"));
        try
        {
            var ok = await _viewModel.SetAutopilotAsync((current, total) =>
                Services.PageNotifyService.UpdateWaiting($"{current}/{total}"));
            if (ok)
            {
                Services.PageNotifyService.Success(FindString("MapPage_AutopilotDone"));
            }
            else
            {
                var message = _viewModel.LastNavigationError ?? FindString("MapPage_AutopilotFail");
                NavErrorText.Text = message;
                Services.PageNotifyService.Error(message);
            }
        }
        finally
        {
            Services.PageNotifyService.HideWaiting();
        }
    }

    // ---------- 信息卡 ----------

    private void Neighbor_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is MapSystemNode node)
        {
            Locate(node.Id);
        }
    }

    /// <summary>
    /// 星图随应用主题切换：画布调色板（<see cref="StarMapCanvas.SetTheme"/>）+ HUD 玻璃面板配色。
    /// HUD 的画刷是代码里新建的固定色（自绘/UI 之外的层拿不到 DynamicResource），必须在主题变化时手动重设。
    /// </summary>
    private void ApplyTheme()
    {
        var light = Services.ThemeService.Theme == Wpf.Ui.Appearance.ApplicationTheme.Light;
        MapCanvas.SetTheme(light);

        Resources["HudBg"] = new SolidColorBrush(light ? Color.FromArgb(0xEA, 0xF2, 0xF5, 0xFB) : Color.FromArgb(0xE0, 0x10, 0x16, 0x26));
        Resources["HudBorder"] = new SolidColorBrush(light ? Color.FromArgb(0x59, 0x0E, 0x74, 0x90) : Color.FromArgb(0x2E, 0x7D, 0xF9, 0xFF));
        Resources["HudAccent"] = new SolidColorBrush(light ? Color.FromRgb(0x0E, 0x74, 0x90) : Color.FromRgb(0x7D, 0xF9, 0xFF));
        Resources["HudText"] = new SolidColorBrush(light ? Color.FromRgb(0x1B, 0x24, 0x37) : Color.FromRgb(0xE6, 0xF0, 0xFF));
        Resources["HudDim"] = new SolidColorBrush(light ? Color.FromRgb(0x5B, 0x6B, 0x85) : Color.FromRgb(0x8F, 0xA3, 0xC8));
        Resources["HudIntel"] = new SolidColorBrush(light ? Color.FromRgb(0xC8, 0x1E, 0x34) : Color.FromRgb(0xFF, 0x5C, 0x74));
        Resources["MapBg"] = new SolidColorBrush(light ? Color.FromRgb(0xE9, 0xEE, 0xF7) : Color.FromRgb(0x05, 0x07, 0x0E));
        Resources["MapLoadingBg"] = new SolidColorBrush(light ? Color.FromArgb(0xAA, 0xE9, 0xEE, 0xF7) : Color.FromArgb(0xAA, 0x05, 0x07, 0x0E));
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
