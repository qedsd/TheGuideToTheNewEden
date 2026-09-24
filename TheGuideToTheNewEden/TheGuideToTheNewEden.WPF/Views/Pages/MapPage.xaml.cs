using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TheGuideToTheNewEden.Core.Models.Map;
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
    private ToolWindow? _navWindow;

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
        Services.ThemeService.ThemeChanged += ApplyTheme;
        ApplyTheme();
        UpdateLegend();
        _viewModel.CoverChanged += (_, ids) => MapCanvas.SetCover(ids);
        _viewModel.SovIconLoaded += (_, e) => MapCanvas.SetSovIcon(e.AllianceId, e.Bitmap);
        // 主权强刷完成（分组窗"重新拉取"/失败后重试成功）：数据版本变了，主权模式下重画着色
        _viewModel.SovReloaded += (_, _) =>
        {
            if (_viewModel.ColorMode == MapColorMode.Sovereignty)
            {
                MapCanvas.SetColorMode(MapColorMode.Sovereignty, _viewModel.KillsMax, _viewModel.JumpsMax, _viewModel.ResourceMax);
            }

            MapCanvas.SetHeatBySovereignty(HeatSovToggle.IsChecked == true);
        };
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
        // 回填持久化的各模式热力色块开关（左下角图例面板，逐着色类型独立记忆）
        var heat = MapSettingService.Value.Canvas ??= new MapCanvasConfig();
        MapCanvas.SetHeatMapVisible(MapColorMode.Kills, heat.ShowHeatKills);
        MapCanvas.SetHeatMapVisible(MapColorMode.Jumps, heat.ShowHeatJumps);
        MapCanvas.SetHeatMapVisible(MapColorMode.PlanetResource, heat.ShowHeatPlanetResource);
        MapCanvas.SetSovShadingVisible(heat.ShowSovShading);
        MapCanvas.SetHeatGridSize(heat.HeatGridSize);
        MapCanvas.SetHeatGridOffset(heat.HeatGridOffsetX, heat.HeatGridOffsetY);
        if (heat.HeatBySovereignty && !_viewModel.IsSovLoaded)
        {
            // 配置记忆了主权聚合：启动时先拉主权数据（磁盘缓存通常瞬时），再设画布开关——
            // 否则 Set 时节点分组号全 0，主权分支空跑回退几何格子，且后续无人触发重建（用户实测"默认勾上但显示方块"）。
            await _viewModel.ApplySovAsync();
        }
        MapCanvas.SetHeatBySovereignty(heat.HeatBySovereignty);
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
        foreach (var window in new[] { _coverWindow, _resourceWindow, _bridgeWindow, _sovWindow, _detailWindow, _navWindow })
        {
            if (window is null)
            {
                continue;
            }

            window.AllowClose();
            window.Close();
        }

        SearchPopup.IsOpen = false;
        ToolsFlyout.Hide();
        FilterFlyout.Hide();
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

        var secText = Helpers.MapTextHelper.FormatSecurity(node.Security);
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
            // 「全部」哨兵（RegionID = 0）：飞回全图概览；其余星域正常定位
            if (region.RegionID == 0)
            {
                MapCanvas.ToOverview();
            }
            else
            {
                MapCanvas.ToRegion(region.RegionID);
            }
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
        UpdateLegend();
    }

    // ---------- 色阶图例 ----------

    private bool? _legendFromHigh;

    /// <summary>程序化回显 <see cref="HeatToggle"/> 状态时置位，避免触发一次多余的落盘。</summary>
    private bool _suppressHeatToggle;

    /// <summary>程序化回显 <see cref="HeatSizeSlider"/> 时置位（滑条拖动才落盘）。</summary>
    private bool _suppressHeatSize;

    /// <summary>程序化回显 <see cref="HeatSovToggle"/> 时置位（用户点击才落盘/切换）。</summary>
    private bool _suppressHeatSov;
    private bool _suppressSovShading;

    /// <summary>
    /// 更新左下角的色阶图例：标题取当前着色模式，色条与两端标签按模式的语义解释——
    /// 安等 = 左「1.0 高安」→ 右「0.0 / 负 低安」；行星资源 / 击杀 / 通行 = 左「低」→ 右「高」；
    /// 主权 = 不画色条，只给"同组同色"的文字说明（分组号是散列色，无固定色阶）。
    /// </summary>
    private void UpdateLegend()
    {
        var mode = _viewModel.ColorMode;
        var fromHigh = mode == MapColorMode.Security;
        if (_legendFromHigh != fromHigh)
        {
            BuildLegendStrips(fromHigh);
            _legendFromHigh = fromHigh;
        }

        LegendTitle.Text = mode switch
        {
            MapColorMode.Sovereignty => FindString("MapPage_ColorSov"),
            MapColorMode.PlanetResource =>
                $"{FindString("MapPage_ColorPlanetResource")} · {FindString(ResourceKindKey(_viewModel.ResourceKind))}",
            MapColorMode.Kills => FindString("MapPage_ColorKills"),
            MapColorMode.Jumps => FindString("MapPage_ColorJumps"),
            _ => FindString("MapPage_ColorSecurity"),
        };

        var isSecurity = mode == MapColorMode.Security;
        LegendScale.Visibility = mode == MapColorMode.Sovereignty ? Visibility.Collapsed : Visibility.Visible;
        LegendLow.Text = FindString(isSecurity ? "MapPage_Legend_HighSec" : "MapPage_Legend_Low");
        LegendHigh.Text = FindString(isSecurity ? "MapPage_Legend_LowSec" : "MapPage_Legend_High");
        LegendNote.Text = FindString(mode switch
        {
            MapColorMode.Sovereignty => "MapPage_Legend_SovNote",
            MapColorMode.Security => "MapPage_Legend_SecNote",
            _ => "MapPage_Legend_HeatNote",
        });

        // 热力色块开关：只在行星资源 / 击杀 / 通行三种模式下显示，随模式回显各自的记忆状态
        var isHeatMode = mode is MapColorMode.Kills or MapColorMode.Jumps or MapColorMode.PlanetResource;
        LegendHeatRow.Visibility = isHeatMode ? Visibility.Visible : Visibility.Collapsed;
        // 主权晕染开关：只在主权着色模式下显示
        var isSovMode = mode == MapColorMode.Sovereignty;
        LegendSovShadingRow.Visibility = isSovMode ? Visibility.Visible : Visibility.Collapsed;
        if (isSovMode)
        {
            _suppressSovShading = true;
            SovShadingToggle.IsChecked = MapCanvas.SovShadingVisible;
            _suppressSovShading = false;
        }

        // 大小滑条与偏移按钮只对几何格子聚合有意义 → 主权聚合开启时隐藏
        var bySov = MapCanvas.HeatBySov;
        LegendHeatSizeRow.Visibility = isHeatMode && !bySov ? Visibility.Visible : Visibility.Collapsed;
        LegendHeatOffsetRow.Visibility = isHeatMode && !bySov ? Visibility.Visible : Visibility.Collapsed;
        if (isHeatMode)
        {
            _suppressHeatToggle = true;
            _suppressHeatSize = true;
            _suppressHeatSov = true;
            HeatToggle.IsChecked = MapCanvas.GetHeatMapVisible(mode);
            HeatSovToggle.IsChecked = bySov;
            // 滑条显示"大小档位"（1..100，越大块越大）：格数按同一线性映射反解（越界值由滑条自身钳到 [Min,Max]）
            var sliderSpan = HeatSizeSlider.Maximum - HeatSizeSlider.Minimum;
            var cellSpan = (double)(StarMapCanvas.MaxHeatGridCells - StarMapCanvas.MinHeatGridCells);
            HeatSizeSlider.Value = HeatSizeSlider.Maximum - sliderSpan * (MapCanvas.HeatGridCells - StarMapCanvas.MinHeatGridCells) / cellSpan;
            HeatSizeText.Text = ((int)Math.Round(HeatSizeSlider.Value)).ToString();
            _suppressHeatToggle = false;
            _suppressHeatSize = false;
            _suppressHeatSov = false;
        }
    }

    /// <summary>色条用画布的同一份调色板（安等从"高安"侧开始，其余模式从"低值"侧开始）；
    /// 容器是 UniformGrid，每格不设宽度自动等分整行 → 随图例面板自适应占满。</summary>
    private void BuildLegendStrips(bool fromHigh)
    {
        LegendStrips.Children.Clear();
        var palette = StarMapCanvas.Palette;
        for (var i = 0; i < palette.Count; i++)
        {
            var color = palette[fromHigh ? palette.Count - 1 - i : i];
            LegendStrips.Children.Add(new System.Windows.Shapes.Rectangle
            {
                Height = 10,
                Fill = new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue)),
            });
        }
    }

    private static string ResourceKindKey(ResourceKind kind) => kind switch
    {
        ResourceKind.Workforce => "MapPage_ResourceWorkforce",
        ResourceKind.MagmaticGas => "MapPage_ResourceMagmaticGas",
        ResourceKind.SuperionicIce => "MapPage_ResourceSuperionicIce",
        _ => "MapPage_ResourcePower",
    };

    private void ResetView_Click(object sender, RoutedEventArgs e)
    {
        MapCanvas.Fit();
    }

    private void BridgesToggle_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.ShowBridges = BridgesToggle.IsChecked == true;
    }

    /// <summary>
    /// 热力色块开关（左下角图例面板，只影响行星资源 / 击杀 / 通行三种模式）：
    /// 每个着色类型独立记忆——画布按模式存运行态，MapSettings.json 的 Canvas 节做持久化。
    /// </summary>
    private void HeatToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressHeatToggle)
        {
            return;
        }

        var visible = HeatToggle.IsChecked == true;
        MapCanvas.SetHeatMapVisible(visible);
        var canvas = MapSettingService.Value.Canvas ??= new MapCanvasConfig();
        switch (_viewModel.ColorMode)
        {
            case MapColorMode.Kills:
                canvas.ShowHeatKills = visible;
                break;
            case MapColorMode.Jumps:
                canvas.ShowHeatJumps = visible;
                break;
            case MapColorMode.PlanetResource:
                canvas.ShowHeatPlanetResource = visible;
                break;
        }

        MapSettingService.Save();
    }

    /// <summary>
    /// 色块大小滑条：滑条值是"大小档位"（XAML 定义 1..100，值越大块越大），**线性映射**到实际网格格数
    /// [<see cref="StarMapCanvas.MinHeatGridCells"/>, <see cref="StarMapCanvas.MaxHeatGridCells"/>]（范围见 StarMapCanvas 常量，格多块小）。
    /// 两端解耦：调滑条手感改 XAML 的 Minimum/Maximum，调实际块大小范围改 StarMapCanvas 常量，映射自动适配；
    /// 存 <see cref="MapCanvasConfig.HeatGridSize"/> 的仍是格数，配置兼容。
    /// </summary>
    private void HeatSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // XAML 解析期 Minimum 一生效就把 Value 钳到下限 → 触发本事件，此时同面板里排在滑条后面的
        // HeatSizeText（以及极端情况下的 MapCanvas）还没创建，必须防空
        if (_suppressHeatSize || HeatSizeText is null || MapCanvas is null)
        {
            return;
        }

        var size = (int)Math.Round(HeatSizeSlider.Value);
        // 线性映射：档位 100（块最大）→ MinHeatGridCells（40 格）；档位 1（块最小）→ MaxHeatGridCells（200 格）
        var sliderSpan = HeatSizeSlider.Maximum - HeatSizeSlider.Minimum;
        var cellSpan = StarMapCanvas.MaxHeatGridCells - StarMapCanvas.MinHeatGridCells;
        var cells = (int)Math.Round(StarMapCanvas.MinHeatGridCells + cellSpan * (HeatSizeSlider.Maximum - size) / sliderSpan);
        HeatSizeText.Text = size.ToString();
        MapCanvas.SetHeatGridSize(cells);
        var canvas = MapSettingService.Value.Canvas ??= new MapCanvasConfig();
        canvas.HeatGridSize = cells;
        MapSettingService.Save();
    }

    /// <summary>
    /// 热力网格偏移按钮（图例面板）：四个方向按 1/4 格步进平移网格原点，「复位」回到默认左上角对齐；
    /// 偏移随 <see cref="MapCanvasConfig.HeatGridOffsetX"/> / <see cref="MapCanvasConfig.HeatGridOffsetY"/> 持久化。
    /// </summary>
    private void HeatOffset_Click(object sender, RoutedEventArgs e)
    {
        if (MapCanvas is null)
        {
            return;
        }

        var step = MapCanvas.HeatGridNudgeStep;
        switch (((FrameworkElement)sender).Tag as string)
        {
            case "L": MapCanvas.NudgeHeatGridOffset(-step, 0); break;
            case "R": MapCanvas.NudgeHeatGridOffset(+step, 0); break;
            case "U": MapCanvas.NudgeHeatGridOffset(0, -step); break;
            case "D": MapCanvas.NudgeHeatGridOffset(0, +step); break;
            default: MapCanvas.ResetHeatGridOffset(); break;
        }

        var canvas = MapSettingService.Value.Canvas ??= new MapCanvasConfig();
        canvas.HeatGridOffsetX = MapCanvas.HeatGridOffsetX;
        canvas.HeatGridOffsetY = MapCanvas.HeatGridOffsetY;
        MapSettingService.Save();
    }

    /// <summary>
    /// 热力聚合方式切换（图例「主权聚合」）：true = 按主权联盟疆域凸包着色，false = 几何格子。
    /// 首次勾选时若主权数据未加载，先 <see cref="MapPageViewModel.ApplySovAsync"/>（有磁盘缓存，通常瞬时；首拉 ESI 完成后写节点 GroupId 再重建）。
    /// 主权数据不可用（未加载/该图无主权星系）时画布自动回退几何格子。
    /// </summary>
    private async void HeatSovToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressHeatSov || MapCanvas is null)
        {
            return;
        }

        var bySov = HeatSovToggle.IsChecked == true;
        if (bySov && !_viewModel.IsSovLoaded)
        {
            await _viewModel.ApplySovAsync();
            if (MapCanvas is null)   // 等待期间页面可能已卸载
            {
                return;
            }
        }

        MapCanvas.SetHeatBySovereignty(bySov);
        var canvas = MapSettingService.Value.Canvas ??= new MapCanvasConfig();
        canvas.HeatBySovereignty = bySov;
        MapSettingService.Save();
        UpdateLegend();   // 按聚合方式重排大小/偏移行显隐
    }

    /// <summary>
    /// 主权晕染开关（左下角图例面板，只影响主权着色模式的疆域晕染层；主权名标签不受影响）。
    /// 运行态由画布记忆并立即重画，MapSettings.json 的 Canvas 节持久化。
    /// </summary>
    private void SovShadingToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressSovShading || MapCanvas is null)
        {
            return;
        }

        var visible = SovShadingToggle.IsChecked == true;
        MapCanvas.SetSovShadingVisible(visible);
        var canvas = MapSettingService.Value.Canvas ??= new MapCanvasConfig();
        canvas.ShowSovShading = visible;
        MapSettingService.Save();
    }

    private void ApplyBridges()
    {
        BridgesToggle.IsChecked = _viewModel.ShowBridges;
        MapCanvas.SetBridges(_viewModel.GetBridgePairs(), _viewModel.ShowBridges);
    }

    // ---------- 星域 / 安等筛选 ----------

    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (FilterFlyout.IsOpen)
        {
            FilterFlyout.Hide();
        }
        else
        {
            FilterFlyout.Show();
        }
    }

    private void FilterApply_Click(object sender, RoutedEventArgs e)
    {
        FilterFlyout.Hide();
        _viewModel.ApplySystemFilter();
    }

    private void FilterClear_Click(object sender, RoutedEventArgs e)
    {
        FilterFlyout.Hide();
        _viewModel.ClearSystemFilter();
    }

    // ---------- 工具菜单 ----------

    private void ToolsButton_Click(object sender, RoutedEventArgs e)
    {
        if (ToolsFlyout.IsOpen)
        {
            ToolsFlyout.Hide();
        }
        else
        {
            ToolsFlyout.Show();
        }
    }

    private void OneJumpCover_Click(object sender, RoutedEventArgs e)
    {
        ToolsFlyout.Hide();
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
        ToolsFlyout.Hide();
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
        ToolsFlyout.Hide();
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
        ToolsFlyout.Hide();
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
    /// **不设 Owner**：工具窗与主窗口相互独立——主窗口最小化不会连带最小化工具窗（设置 Owner 的话 Windows 会把它们绑在一起），
    /// 应用退出时统一由 Shutdown 关闭它们。
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
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
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

    /// <summary>
    /// 工具菜单的"情报"入口：打开（或前置）情报工具窗口，未在监听时顺手开始监听。
    /// 入口只保留这一处——顶栏那个"情报"复选框已按用户要求删除。
    /// </summary>
    private void IntelTool_Click(object sender, RoutedEventArgs e)
    {
        ToolsFlyout.Hide();
        _viewModel.RefreshIntelAvailability();
        if (!_viewModel.IntelRunning)
        {
            _viewModel.StartIntel();
        }

        ShowIntelTool();
    }

    /// <summary>
    /// 打开（或前置）情报工具窗口。窗口是"真关闭"的（不用 CloseToHide）：
    /// 点 X → 真正销毁 → **顺手停止监听**（窗口已是唯一的情报 UI，关掉即停）；
    /// 窗口内的"停止"按钮照旧；切换页面时窗口与监听都保留。
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
                _viewModel.SaveIntelConfig();
                _viewModel.StopIntel();
            };
        }

        _intelWindow.Show();
        _intelWindow.Activate();
    }

    // ---------- 导航 ----------

    /// <summary>工具菜单的"导航"入口：打开（或前置）导航工具窗口（顶栏那个按钮已按用户要求删除）。</summary>
    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        ToolsFlyout.Hide();
        ShowNavigation();
    }

    private void ShowNavigation()
    {
        if (_navWindow is null)
        {
            _navWindow = CreateToolWindow(new MapNavigationView(_viewModel, Locate, ClearRouteOnCanvas), "MapPage_Tool_Navigate", 980, 720);
            _navWindow.Closed += (_, _) => _navWindow = null;
        }

        _navWindow.Show();
        _navWindow.Activate();
    }

    /// <summary>导航窗里点"清除"时，把画布上的航线一起抹掉。</summary>
    private void ClearRouteOnCanvas() => MapCanvas.ClearRoute();

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
