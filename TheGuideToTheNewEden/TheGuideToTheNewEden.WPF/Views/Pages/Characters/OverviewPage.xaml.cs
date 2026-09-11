using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.ViewModels.Characters;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Characters;

/// <summary>角色总览：军团/联盟、舰船与位置、在线状态、技能队列。</summary>
public partial class OverviewPage : Page, ICharacterSubPage
{
    private readonly CharacterContext _context;
    private readonly OverviewPageViewModel _viewModel = new();
    private bool _loaded;

    public OverviewPage(CharacterContext context)
    {
        InitializeComponent();

        _context = context;
        DataContext = _viewModel;
        Loaded += async (_, _) =>
        {
            await LoadAsync();

            // 页面实例常驻，ScrollViewer 的滚动位置会跨"进入"保留；且异步布局/焦点处理
            // 可能触发 BringIntoView 把视图带到下方。等队列排空后强制回到顶部。
            await Dispatcher.InvokeAsync(() => ScrollHost.ScrollToTop(), DispatcherPriority.ContextIdle);
        };
    }

    /// <summary>工作区刷新按钮调用；forceRefresh=true 时绕过缓存重新拉取。</summary>
    public Task RefreshAsync(bool forceRefresh = true) => LoadAsync(forceRefresh);

    private async Task LoadAsync(bool forceRefresh = false)
    {
        // Page 被 Frame 长期托管，Loaded 可能重复触发；首屏之外由 ICharacterSubPage 驱动刷新。
        if (_loaded && !forceRefresh)
        {
            return;
        }

        _loaded = true;
        await _viewModel.LoadAsync(_context, forceRefresh);
    }
}
