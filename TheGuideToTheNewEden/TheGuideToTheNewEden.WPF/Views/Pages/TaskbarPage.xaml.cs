using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.ViewModels;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 任务栏工具页（菜单名「分屏」）：把 EVE 客户端窗口的任务栏按钮**拆分**（每角色一个按钮）或**合并**（并成一个）。
/// 对应 WinUI 版 <c>Views/Tools/TaskbarPage</c>（那里也是独立工具窗，原尺寸 200×140），
/// 实现见 <see cref="TaskbarPageViewModel"/> 与 <c>Helpers/TaskbarHelper</c>。
/// <para>页面在**独立工具窗口**里打开（见 <see cref="IToolPage"/>），不占主窗口内容区。</para>
/// </summary>
public partial class TaskbarPage : Page, IToolPage
{
    private readonly TaskbarPageViewModel _viewModel = new();

    public TaskbarPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    /// <summary>默认尺寸与 WinUI 版 <c>GetWindowSize</c> 一致（200×140）。</summary>
    public int PreferredWidth => 300;

    public int PreferredHeight => 140;

    /// <summary>没有后台工作，关闭即结束。</summary>
    public void OnToolClosed()
    {
    }

    /// <summary>
    /// 这个工具窗**只留关闭按钮**（用户要求）：它只有两个动作、窗口又小，置顶图钉与最小化/最大化都是多余的；
    /// 置顶本身仍是默认开启的（只是不显示那颗开关，同测速迷你模式的口径）。
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is IToolWindowShell shell)
        {
            shell.SetVisibleTitleBarButtons(ToolWindowButtons.Close);
        }
    }

    private async void OnSeparateClick(object sender, RoutedEventArgs e)
        => await _viewModel.SeparateAsync();

    private async void OnMergeClick(object sender, RoutedEventArgs e)
        => await _viewModel.MergeAsync();
}
