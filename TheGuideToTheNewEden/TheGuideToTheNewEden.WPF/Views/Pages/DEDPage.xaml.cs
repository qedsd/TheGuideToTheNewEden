using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.ViewModels;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// DED 页：左侧"势力筛选 + 名称搜索"的 DED 列表，右侧是选中项的攻略正文（Markdown）。
/// 对应 WinUI 版 <c>Views/DEDPage</c>，数据取自 Core <c>DEDService</c>（<c>Resources/Database/ded.db</c>）。
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
