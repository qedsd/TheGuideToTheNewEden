using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.ViewModels.Map;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.Map;

/// <summary>
/// 导航工具窗（星图页独立窗口）：窗口内搜索添加航点/规避、旗舰参数、计算与结果、在游戏中设置航点。
/// DataContext 是星图页的 <see cref="MapPageViewModel"/>；画布动作（定位、清航线）通过回调交回星图页。
/// </summary>
public partial class MapNavigationView : UserControl
{
    private readonly MapPageViewModel _viewModel;
    private readonly Action<int>? _locateSystem;
    private readonly Action? _clearRoute;
    private readonly ObservableCollection<MapSystemNode> _navSearchResults = [];
    private DependencyObject? _themeSource;

    public MapNavigationView(MapPageViewModel viewModel, Action<int>? locateSystem = null, Action? clearRoute = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _locateSystem = locateSystem;
        _clearRoute = clearRoute;
        DataContext = viewModel;
        NavSearchResultsList.ItemsSource = _navSearchResults;
        CapitalToggle.IsChecked = viewModel.CapitalMode;

        // 工具窗是独立 FluentWindow：部分主题画刷切换后仍停留在深色/黑色，
        // 这里只把“深色前景”改成当前窗口前景，保留安全等级色和主按钮对比度。
        AddHandler(FrameworkElement.LoadedEvent, new RoutedEventHandler(OnElementLoaded));
        Loaded += OnViewLoaded;
        Unloaded += OnViewUnloaded;
    }

    private void OnViewLoaded(object sender, RoutedEventArgs e)
    {
        _themeSource = Window.GetWindow(this)?.Content as DependencyObject ?? this;
        ThemeService.ThemeChanged -= OnThemeChanged;
        ThemeService.ThemeChanged += OnThemeChanged;
        ApplyReadableForegrounds(this);
    }

    private void OnViewUnloaded(object sender, RoutedEventArgs e)
        => ThemeService.ThemeChanged -= OnThemeChanged;

    private void OnThemeChanged()
    {
        Dispatcher.BeginInvoke(() =>
        {
            _themeSource = Window.GetWindow(this)?.Content as DependencyObject ?? _themeSource ?? this;
            ApplyReadableForegrounds(this);
        });
    }

    private void OnElementLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBlock text)
        {
            FixTextForeground(text);
        }
        else if (sender is Wpf.Ui.Controls.Button button)
        {
            FixButtonForeground(button);
        }
        else if (sender is TextBox textBox)
        {
            FixControlForeground(textBox);
        }
    }

    private void ApplyReadableForegrounds(DependencyObject root)
    {
        if (root is TextBlock text)
        {
            FixTextForeground(text);
        }
        else if (root is Wpf.Ui.Controls.Button button)
        {
            FixButtonForeground(button);
        }
        else if (root is TextBox textBox)
        {
            FixControlForeground(textBox);
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            ApplyReadableForegrounds(VisualTreeHelper.GetChild(root, i));
        }
    }

    private void FixTextForeground(TextBlock text)
    {
        if (!IsDarkBrush(text.Foreground) || IsSecurityColor(text.Foreground))
        {
            return;
        }

        // 主按钮（浅色底）继续用深色文字，避免变成白字看不清。
        if (FindAncestor<Wpf.Ui.Controls.Button>(text) is
            { Appearance: Wpf.Ui.Controls.ControlAppearance.Primary })
        {
            return;
        }

        BindThemeForeground(text, TextBlock.ForegroundProperty);
    }

    private void FixButtonForeground(Wpf.Ui.Controls.Button button)
    {
        if (button.Appearance == Wpf.Ui.Controls.ControlAppearance.Primary
            || !IsDarkBrush(button.Foreground))
        {
            return;
        }

        BindThemeForeground(button, Control.ForegroundProperty);
    }

    private void FixControlForeground(TextBox textBox)
    {
        if (IsDarkBrush(textBox.Foreground))
        {
            BindThemeForeground(textBox, Control.ForegroundProperty);
        }
    }

    private void BindThemeForeground(FrameworkElement element, DependencyProperty property)
    {
        _themeSource ??= Window.GetWindow(this)?.Content as DependencyObject ?? this;
        element.SetBinding(property, new Binding("(TextElement.Foreground)")
        {
            Source = _themeSource,
        });
    }

    private static bool IsDarkBrush(Brush? brush)
    {
        if (brush is not SolidColorBrush solid || solid.Color.A < 40)
        {
            return false;
        }

        var color = solid.Color;
        return color.R < 80 && color.G < 80 && color.B < 80;
    }

    private static bool IsSecurityColor(Brush? brush)
    {
        if (brush is not SolidColorBrush solid)
        {
            return false;
        }

        var color = solid.Color;
        return StarMapCanvas.Palette.Any(p => p.Red == color.R && p.Green == color.G && p.Blue == color.B);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T target)
            {
                return target;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    // ---------- 窗口内搜索 ----------

    private void NavSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var text = NavSearchBox.Text?.Trim() ?? string.Empty;
        _navSearchResults.Clear();
        if (text.Length < 2)
        {
            NavSearchPopup.IsOpen = false;
            return;
        }

        _viewModel.Search(text, 8);
        foreach (var node in _viewModel.SearchResults)
        {
            _navSearchResults.Add(node);
        }

        NavSearchPopup.IsOpen = _navSearchResults.Count > 0;
    }

    private void NavSearchResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavSearchResultsList.SelectedItem is MapSystemNode node)
        {
            AddNode(node);
            NavSearchResultsList.SelectedItem = null;
            NavSearchBox.Text = string.Empty;
            NavSearchPopup.IsOpen = false;
        }
    }

    private void AddFromMap_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedSystem is { } node)
        {
            AddNode(node);
        }
    }

    private void AddNode(MapSystemNode node)
    {
        if (NodeTabs.SelectedIndex == 1)
        {
            _viewModel.AddAvoid(node);
        }
        else
        {
            _viewModel.AddWaypoint(node);
        }

        MarkPending();
    }

    private void RemoveNavNode_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not MapSystemNode node)
        {
            return;
        }

        if (NodeTabs.SelectedIndex == 1)
        {
            _viewModel.RemoveAvoid(node);
        }
        else
        {
            _viewModel.RemoveWaypoint(node);
        }

        MarkPending();
    }

    private void NodeTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavSearchPopup is null)
        {
            return;
        }

        NavSearchPopup.IsOpen = false;
        NavSearchBox.Text = string.Empty;
    }

    // ---------- 路线 ----------

    private void CapitalToggle_Changed(object sender, RoutedEventArgs e)
        => _viewModel.CapitalMode = CapitalToggle.IsChecked == true;

    private async void ComputeRoute_Click(object sender, RoutedEventArgs e)
    {
        NavErrorText.Text = string.Empty;
        NavWaiting.Show(FindString("MapPage_Loading"), null);
        try
        {
            if (!await _viewModel.NavigateAsync())
            {
                NavErrorText.Text = _viewModel.LastNavigationError ?? string.Empty;
            }
        }
        finally
        {
            NavWaiting.Hide();
        }
    }

    private void ClearRoute_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearNavigation();
        _clearRoute?.Invoke();
        NavErrorText.Text = string.Empty;
    }

    private void MarkPending()
    {
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
        NavWaiting.Show(FindString("MapPage_SetInGame"), null);
        try
        {
            var ok = await _viewModel.SetAutopilotAsync((current, total) =>
                NavWaiting.UpdateText($"{current}/{total}"));
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
            NavWaiting.Hide();
        }
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
