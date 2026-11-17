using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.ViewModels;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 「攻略 · 死亡远征」页：左侧"分类 / 势力 / 难度 + 名称搜索"的站点列表，右侧是选中项的攻略正文（Markdown）。
/// 数据取自 Core <c>CombatSiteService</c>（<c>Resources/Database/static.db</c> 的 <c>CombatSite</c> 表）。
/// 右侧正文双轨：社区攻略（本站整理）与 Wiki 数据（EVE University Wiki 抓取并翻译）。
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
        // 页面是常驻缓存的（NavigationCacheMode=Required），只有第一次进来时查库。
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        await _viewModel.InitAsync();
    }
}
