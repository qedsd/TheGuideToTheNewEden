using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.ViewModels;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 「攻略 · 资源」页：左侧列表 + 分类筛选，右侧 Markdown 正文。
/// 数据是 `CombatSite` 里的非战斗站点（战斗遗迹 / 数据站点 / 矿石站点 / 气云站点 / 冰带）。
/// </summary>
public partial class ResourcesPage : Page
{
    private readonly ResourcesPageViewModel _viewModel = new();
    private bool _loaded;

    public ResourcesPage()
    {
        InitializeComponent();

        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // 页面常驻缓存（NavigationCacheMode=Required），数据只查第一次。
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        await _viewModel.InitAsync();
    }
}
