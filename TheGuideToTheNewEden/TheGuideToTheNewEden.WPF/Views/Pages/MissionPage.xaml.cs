using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.ViewModels;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 任务页：左侧"等级筛选 + 名称搜索"的任务列表，右侧是选中任务的攻略正文（Markdown）。
/// 对应 WinUI 版 <c>Views/MissionPage</c>，数据取自 Core <c>MissionService</c>（<c>Resources/Database/static.db</c>）。
/// </summary>
public partial class MissionPage : Page
{
    private readonly MissionPageViewModel _viewModel = new();
    private bool _loaded;

    public MissionPage()
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
