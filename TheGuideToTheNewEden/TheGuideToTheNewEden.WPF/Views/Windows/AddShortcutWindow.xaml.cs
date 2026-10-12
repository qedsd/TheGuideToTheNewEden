using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.ViewModels.Home;
using Wpf.Ui.Controls;
// WPF-UI 也有自己的 Button，代码里判断点击来源时用 WPF 原生的那个。
using Button = System.Windows.Controls.Button;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 「添加功能」窗口：左侧分组 + 右侧搜索与功能列表，逐个添加 / 移除主页快捷方式。
/// <para>
/// 单实例（重复点击只激活已开的那个）。改动直接落到 <c>HomeShortcutService</c>，
/// 主页通过服务的 <c>Changed</c> 事件实时刷新，所以这个窗口<b>不需要</b>"确定 / 应用"按钮。
/// </para>
/// <para>
/// 与设计稿的一处有意偏差：这里设了 <c>Owner</c>（设计稿写的是"不设 Owner"）。那条规则的背景是
/// ToolWindow 的**置顶按钮**语义（被拥有的窗口本来就恒在宿主之上，会让图钉看起来失效）；
/// 本窗口没有置顶按钮，设 Owner 反而能保证它不被主窗口盖住、并随主窗口一起最小化。
/// </para>
/// </summary>
public partial class AddShortcutWindow : FluentWindow
{
    private static AddShortcutWindow? _instance;

    private readonly AddShortcutViewModel _viewModel = new();

    public AddShortcutWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        // 主窗口此时必然已显示（只能从主页 / 导航右键菜单打开本窗口）。
        if (App.MainWindow is { } mainWindow)
        {
            Owner = mainWindow;
        }

        Loaded += OnWindowLoaded;
        Closed += OnWindowClosed;
    }

    /// <summary>打开（或激活）单实例窗口。</summary>
    public static void ShowWindow()
    {
        if (_instance is { IsLoaded: true } existing)
        {
            if (existing.WindowState == WindowState.Minimized)
            {
                existing.WindowState = WindowState.Normal;
            }

            existing.Activate();
            return;
        }

        _instance = new AddShortcutWindow();
        _instance.Show();
        _instance.Activate();
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        LanguageService.LanguageChanged += OnLanguageChanged;
        SearchBox.Focus();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        LanguageService.LanguageChanged -= OnLanguageChanged;
        _viewModel.Dispose();

        if (ReferenceEquals(_instance, this))
        {
            _instance = null;
        }
    }

    private void OnLanguageChanged(object? sender, string language) => _viewModel.RefreshTexts();

    private void OnGroupSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 分组集合重建时 SelectedItem 会短暂归零，忽略这种回调（真正的选中项随后会补上）。
        if (GroupList.SelectedItem is AddShortcutGroup group)
        {
            _viewModel.SelectedGroup = group;
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        => _viewModel.SearchText = SearchBox.Text;

    /// <summary>点整行 = 添加 / 移除。点在行内那颗按钮上时交给 <see cref="OnRowActionClick"/>。</summary>
    private void OnRowClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button
            && button.Tag is AddShortcutRow row
            && !VisualTreeSearch.IsInsideChildButton(e.OriginalSource as DependencyObject, button))
        {
            _viewModel.Toggle(row);
        }
    }

    private void OnRowActionClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is AddShortcutRow row)
        {
            _viewModel.Toggle(row);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
