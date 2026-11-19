using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.ViewModels;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 「攻略 · 入侵」页：左侧搜索 + 列表 + 类型筛选，右侧标题与 Markdown 正文。
/// 数据来自 static.db 的 IncursionSite 表（EVE University Wiki 的 Incursions sites，15 个站点）。
/// </summary>
public partial class IncursionPage : Page
{
    private readonly IncursionPageViewModel _viewModel = new();
    private bool _loaded;

    public IncursionPage()
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
