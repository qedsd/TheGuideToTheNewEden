using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TheGuideToTheNewEden.WPF.ViewModels.Map;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.Map;

/// <summary>
/// 导航工具窗（原为星图页上的锚定浮层，现改为独立窗口）：航点 / 规避 / 旗舰参数 / 计算与结果 / 在游戏中设置航点。
/// DataContext 是星图页的 <see cref="MapPageViewModel"/>；需要画布配合的动作（定位、清航线）通过回调交回星图页。
/// </summary>
public partial class MapNavigationView : UserControl
{
    private readonly MapPageViewModel _viewModel;
    private readonly Action<int>? _locateSystem;
    private readonly Action? _clearRoute;

    public MapNavigationView(MapPageViewModel viewModel, Action<int>? locateSystem = null, Action? clearRoute = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _locateSystem = locateSystem;
        _clearRoute = clearRoute;
        DataContext = viewModel;
        CapitalToggle.IsChecked = viewModel.CapitalMode;
    }

    private void AddWaypoint_Click(object sender, RoutedEventArgs e) => _viewModel.AddWaypoint(_viewModel.SelectedSystem);

    private void AddAvoid_Click(object sender, RoutedEventArgs e) => _viewModel.AddAvoid(_viewModel.SelectedSystem);

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
        => _viewModel.CapitalMode = CapitalToggle.IsChecked == true;

    private async void ComputeRoute_Click(object sender, RoutedEventArgs e)
    {
        NavErrorText.Text = string.Empty;
        Services.PageNotifyService.ShowWaiting(FindString("MapPage_Loading"));
        try
        {
            if (!await _viewModel.NavigateAsync())
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
        _clearRoute?.Invoke();
        NavErrorText.Text = string.Empty;
    }

    private void NavResult_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: NavResultItem item })
        {
            _locateSystem?.Invoke(item.Node.Id);
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

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
