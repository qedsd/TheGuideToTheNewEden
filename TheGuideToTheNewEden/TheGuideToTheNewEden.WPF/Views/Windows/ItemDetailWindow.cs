using System.Windows;
using TheGuideToTheNewEden.WPF.ViewModels;
using TheGuideToTheNewEden.WPF.Views.UserControls;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 物品详情窗口（阶段 283）：**复用统一的 <see cref="ToolWindow"/> 外壳**——
/// logo、标题、最小化/最大化/关闭、以及标题栏的置顶图钉都是外壳自带的（阶段 25 的实现），
/// 这里只负责把 <see cref="ItemDetailView"/> 装进去、设标题键、默认置顶与单实例复用。
/// </summary>
public static class ItemDetailWindow
{
    private static ToolWindow? _window;
    private static ItemDetailView? _view;

    /// <summary>打开（或复用）物品详情窗口；已经开着时只切换内容并激活。</summary>
    /// <param name="showMarketButton">是否显示底部「查看市场详情」按钮（从市场入口打开时传 false）。</param>
    public static void ShowItem(int typeId, bool showMarketButton = true)
    {
        if (typeId <= 0)
        {
            return;
        }

        var viewModel = new ItemDetailViewModel(typeId);

        if (_window is { IsLoaded: true } existing && _view is not null)
        {
            _view.DataContext = viewModel;
            _view.ShowMarketButton = showMarketButton;
            if (existing.WindowState == WindowState.Minimized)
            {
                existing.WindowState = WindowState.Normal;
            }

            existing.Activate();
            return;
        }

        _view = new ItemDetailView { DataContext = viewModel, ShowMarketButton = showMarketButton };

        var window = new ToolWindow(
            _view,
            ToolWindowTitleStyle.Default,
            showTopmostButton: true,
            showInTaskbar: false,
            width: 760,
            height: 700)
        {
            // 默认置顶：必须在 Show 之前设好——ToolWindow 在 Loaded 里按当前 Topmost 决定图钉图标与提示。
            Topmost = true,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };

        // 标题跟随语言（动态资源引用，切语言即时生效）；窗口不再显示物品名。
        window.SetResourceReference(ToolWindow.DisplayTitleProperty, "ItemDetail_Title");
        window.SetResourceReference(ToolWindow.SystemTitleProperty, "ItemDetail_Title");

        window.Closed += (_, _) =>
        {
            _window = null;
            _view = null;
        };

        _window = window;
        window.Show();
        window.Activate();
    }
}
