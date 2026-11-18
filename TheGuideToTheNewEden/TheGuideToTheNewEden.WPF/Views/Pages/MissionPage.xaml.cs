using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.ViewModels;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 「攻略 · 任务」页：左侧搜索 + 列表 + 分类/等级/势力筛选，右侧标题与 Markdown 正文。
/// 数据来自 static.db 的 Mission 表（EVE University Wiki 的 Missions 体系，1313 条）。
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
        // 页面常驻缓存（NavigationCacheMode=Required），数据只查第一次。
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        await _viewModel.InitAsync();
    }
}
