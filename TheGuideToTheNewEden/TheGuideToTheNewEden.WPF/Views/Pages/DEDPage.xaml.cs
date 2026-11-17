using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.ViewModels;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 「攻略 · 死亡远征」页：左侧搜索 + 列表 + 三个筛选，右侧标题 + 元信息 + 正文（社区攻略 / Wiki 数据 双页签）。
/// </summary>
public partial class DEDPage : Page
{
    private readonly DEDPageViewModel _viewModel = new();
    private bool _loaded;

    public DEDPage()
    {
        InitializeComponent();

        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 页面是常驻缓存的（NavigationCacheMode=Required），数据只查第一次。
        if (!_loaded)
        {
            _loaded = true;
            await _viewModel.InitAsync();
        }

        // 「异常」页点关联站点跳过来时带一个待选 Id。
        // 每次 Loaded 都取一次：页面被缓存，第二次进来不会再走上面的初始化。
        var pendingId = Navigation.TakePendingCombatSite();
        if (pendingId is > 0)
        {
            await _viewModel.SelectSiteAsync(pendingId.Value);
        }
    }
}
