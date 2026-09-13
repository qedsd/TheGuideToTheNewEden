using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TheGuideToTheNewEden.WPF.ViewModels.Translation;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>
/// 翻译面板：左侧输入与匹配列表、右侧译文详情。
/// 页面（<see cref="Pages.TranslationPage"/>）与"弹窗"（<see cref="ToolWindow"/> 承载本控件的另一个实例）
/// 共用这一份标记与逻辑，弹窗实例不再显示"弹窗"按钮。
/// </summary>
public partial class TranslationPanelView : UserControl
{
    private readonly TranslationPageViewModel _viewModel = new();
    private ToolWindow? _window;

    public TranslationPanelView()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += (_, _) => _viewModel.Init();
        Unloaded += (_, _) => _viewModel.Dispose();
    }

    /// <summary>是否显示"弹窗"按钮（由弹窗承载时置 false）。</summary>
    public bool ShowPopWindowButton
    {
        get => _viewModel.CanPopWindow;
        set => _viewModel.CanPopWindow = value;
    }

    private void OnTranslateClick(object sender, RoutedEventArgs e) => _ = _viewModel.SearchAsync();

    private void OnClearClick(object sender, RoutedEventArgs e) => _viewModel.Clear();

    private void OnCopyClick(object sender, RoutedEventArgs e) => _viewModel.CopyTranslation();

    private void OnCopyQueryClick(object sender, RoutedEventArgs e) => _viewModel.CopyQuery();

    private void OnCopyTranslationClick(object sender, RoutedEventArgs e) => _viewModel.CopyTranslation();

    /// <summary>右键点在匹配列表的某一行时先选中它，保证右键菜单作用于鼠标下的那条。</summary>
    private void OnMatchesPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (ItemsControl.ContainerFromElement(listBox, source) is ListBoxItem item)
        {
            item.IsSelected = true;
        }
    }

    /// <summary>回车立即查询（输入框是单行，KeyDown 不会被 TextBox 吞掉）。</summary>
    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        _ = _viewModel.SearchAsync();
    }

    /// <summary>把翻译面板弹到独立工具窗口（单实例，重复点击仅激活）。</summary>
    private void OnPopWindowClick(object sender, RoutedEventArgs e)
    {
        if (_window is null)
        {
            _window = new ToolWindow(
                new TranslationPanelView { ShowPopWindowButton = false },
                ToolWindowTitleStyle.Default,
                showTopmostButton: true,
                showInTaskbar: true,
                width: 900,
                height: 620)
            {
                // **不设 Owner**：被拥有的窗口永远压在宿主窗口之上，"取消置顶"看起来和置顶一样
                // （用户实测反馈）。不拥有 + 手动居中：未置顶时是普通窗口，置顶时才浮在最前。
                DisplayTitle = FindString("TranslationPage"),
            };
            _window.Closed += (_, _) => _window = null;
            CenterOverHost(_window);
        }

        _window.Show();
        _window.Activate();
    }

    /// <summary>居中到宿主主窗口（不设 Owner，所以 WindowStartupLocation.CenterOwner 不适用）。</summary>
    private void CenterOverHost(Window window)
    {
        var host = Window.GetWindow(this);
        if (host is null)
        {
            return;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        var workArea = SystemParameters.WorkArea;
        var left = host.Left + (host.ActualWidth - window.Width) / 2;
        var top = host.Top + (host.ActualHeight - window.Height) / 2;
        window.Left = Math.Clamp(left, workArea.Left, Math.Max(workArea.Left, workArea.Right - window.Width));
        window.Top = Math.Clamp(top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - window.Height));
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
