using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TheGuideToTheNewEden.WPF.Services.Map;
using TheGuideToTheNewEden.WPF.ViewModels.Map;
using TheGuideToTheNewEden.WPF.Views.UserControls.Map;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 星图页：SkiaSharp 深空画布 + 顶栏工具（搜索/星域定位/着色/角色/情报/导航）
/// + HUD 浮层（悬停提示、情报流、选中星系信息卡）。
/// 页面常驻缓存（NavigationCacheMode=Required），数据只装载一次。
/// </summary>
public partial class MapPage : Page
{
    private readonly MapPageViewModel _viewModel = new();
    private bool _initialized;

    public MapPage()
    {
        InitializeComponent();
        DataContext = _viewModel;

        MapCanvas.SelectedChanged += MapCanvas_SelectedChanged;
        MapCanvas.HoveredChanged += MapCanvas_HoveredChanged;

        _viewModel.IntelMarkersChanged += (_, markers) => MapCanvas.SetIntel(markers);
        _viewModel.ColorModeChanged += (_, mode) => ApplyColorMode(mode);
        _viewModel.ShowCharactersChanged += (_, enabled) => SetCharactersEnabled(enabled);
        _viewModel.PortraitLoaded += (_, e) => MapCanvas.SetCharacterImage(e.CharacterId, e.Bitmap);
        _viewModel.NavigationCompleted += (_, _) =>
        {
            MapCanvas.SetRoute(_viewModel.LastPath, _viewModel.LastWaypointIndices);
        };
        _viewModel.StatisticsLoaded += (_, _) => ApplyColorMode(_viewModel.ColorMode);

        CharacterLocationService.Current.LocationsUpdated += CharacterLocationsUpdated;
        Loaded += MapPage_Loaded;
    }

    private async void MapPage_Loaded(object sender, RoutedEventArgs e)
    {
        // 页面常驻缓存，只装载一次数据
        if (_initialized || _viewModel.IsLoading)
        {
            return;
        }

        _initialized = true;
        await _viewModel.LoadAsync(async (nodes, links) =>
        {
            MapCanvas.SetData(nodes, links);
            _viewModel.AllNodes = nodes;
        });
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
        if (!_initialized)
        {
            return;
        }

        _viewModel.ColorMode = (MapColorMode)ColorModeCombo.SelectedIndex;
    }

    private void ApplyColorMode(MapColorMode mode)
    {
        if (mode != MapColorMode.Security)
        {
            _viewModel.FillHeat(_viewModel.AllNodes ?? [], mode);
        }
        MapCanvas.SetColorMode(mode, _viewModel.KillsMax, _viewModel.JumpsMax);
    }

    private void ResetView_Click(object sender, RoutedEventArgs e)
    {
        MapCanvas.Fit();
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

    private void IntelToggle_Changed(object sender, RoutedEventArgs e)
    {
        var enabled = IntelToggle.IsChecked == true;
        IntelPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        if (!_viewModel.IntelAvailable)
        {
            return;
        }

        if (enabled)
        {
            _viewModel.RefreshIntelAvailability();
            _viewModel.StartIntel();
        }
        else
        {
            _viewModel.SaveIntelKeywords();
            _viewModel.StopIntel();
        }
    }

    private void IntelFilter_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IntelRunning)
        {
            _viewModel.SaveIntelKeywords();
        }
    }

    private void IntelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: IntelMsgItem item })
        {
            IntelList.SelectedItem = null;
            if (item.SystemId > 0)
            {
                Locate(item.SystemId);
            }
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
        var ok = await _viewModel.NavigateAsync();
        if (!ok)
        {
            NavErrorText.Text = _viewModel.LastNavigationError ?? string.Empty;
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
        var ok = await _viewModel.SetAutopilotAsync();
        if (ok)
        {
            Services.PageNotifyService.Success(FindString("MapPage_AutopilotDone"));
        }
        else
        {
            Services.PageNotifyService.Error(FindString("MapPage_AutopilotFail"));
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

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
