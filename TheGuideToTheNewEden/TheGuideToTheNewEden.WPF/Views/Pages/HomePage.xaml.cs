using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Home;
using TheGuideToTheNewEden.WPF.ViewModels.Home;
using TheGuideToTheNewEden.WPF.Views.Pages.Characters;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 主页：品牌 / 状态条 + 快捷方式磁贴网格（详见 <c>HOME_DESIGN.md</c>）。
/// <para>
/// 阶段 101 起磁贴只剩"点开"一种交互——快捷方式的编辑（增删 / 排序 / 宽窄 / 恢复默认）
/// 全部收进 设置 → 主页；数据本身完全由 <see cref="HomeShortcutService"/> 持有，
/// 任何入口改动后页面重建磁贴。
/// </para>
/// </summary>
public partial class HomePage : Page, IManualAware
{
    private readonly HomePageViewModel _viewModel = new();

    private bool _languageHooked;

    /// <summary>F1：主页看 <c>home</c> 那篇（品牌条 / 快捷方式 / 数据卡的用法）。</summary>
    public string? ManualId => "home";

    public HomePage()
    {
        InitializeComponent();
        DataContext = _viewModel;

        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
    }

    // ---------- 生命周期 ----------

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        // 页面实例常驻（NavigationCacheMode=Required），语言事件按"加载即订阅、离开即退订"配对。
        if (!_languageHooked)
        {
            LanguageService.LanguageChanged += OnLanguageChanged;
            _languageHooked = true;
        }

        _ = _viewModel.InitAsync();
        _viewModel.StartClock();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        if (_languageHooked)
        {
            LanguageService.LanguageChanged -= OnLanguageChanged;
            _languageHooked = false;
        }

        _viewModel.StopClock();
    }

    private void OnLanguageChanged(object? sender, string language) => _viewModel.RefreshTexts();

    /// <summary>
    /// 品牌条的圆角裁剪：<c>ClipToBounds="True"</c> 只按**矩形**裁切，不认 <c>CornerRadius</c>，
    /// 装饰层的出血星门环会从圆角处溢出（视觉上表现为"只有右上角是方的"）。
    /// 这里按实际尺寸重建圆角矩形 Clip 几何；夹具/角半径与 XAML 的 <c>CornerRadius="8"</c> 保持一致。
    /// </summary>
    private void OnHeroBannerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Border banner)
        {
            return;
        }

        double w = banner.ActualWidth;
        double h = banner.ActualHeight;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        double r = banner.CornerRadius.TopLeft;
        banner.Clip = new RectangleGeometry(new Rect(0, 0, w, h), r, r);
    }

    // ---------- 品牌条 ----------

    /// <summary>快捷输入：回车触发路由（简单查询直接跳转，复杂问题转 AI 助手）。</summary>
    private async void OnQuickQueryKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await RunQuickQueryAsync();
    }

    private async void OnQuickQueryGoClick(object sender, RoutedEventArgs e) => await RunQuickQueryAsync();

    /// <summary>占位提示：有输入就藏起来（纯 WPF TextBox 没有 PlaceholderText）。</summary>
    private void OnQuickQueryTextChanged(object sender, TextChangedEventArgs e)
        => QuickQueryPlaceholder.Visibility = string.IsNullOrEmpty(QuickQueryBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;

    private async Task RunQuickQueryAsync()
    {
        var query = QuickQueryBox.Text?.Trim() ?? string.Empty;
        if (query.Length == 0)
        {
            return;
        }

        try
        {
            var result = await HomeQuickQueryRouter.RouteAsync(query);
            switch (result.Kind)
            {
                case HomeQuickQueryRouter.RouteKind.Jumped:
                    QuickQueryBox.Clear();
                    PageNotifyService.Success(result.Message ?? string.Empty);
                    break;

                case HomeQuickQueryRouter.RouteKind.ToAssistant:
                    QuickQueryBox.Clear();
                    PageNotifyService.Info(result.Message ?? string.Empty);
                    break;
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(ex.Message);
        }
    }

    private void OnOpenLogClick(object sender, RoutedEventArgs e)
        => HomeFeatureLauncher.Open(HomeFeatureCatalog.Find("action-open-log"));

    private void OnOpenConfigClick(object sender, RoutedEventArgs e)
        => HomeFeatureLauncher.Open(HomeFeatureCatalog.Find("action-open-config"));

    private void OnOpenManualClick(object sender, RoutedEventArgs e)
        => HomeFeatureLauncher.Open(HomeFeatureCatalog.Find("action-manual"));

    private void OnOpenAboutClick(object sender, RoutedEventArgs e)
        => HomeFeatureLauncher.Open(HomeFeatureCatalog.Find("action-about"));

    private void OnUpdatePillClick(object sender, MouseButtonEventArgs e)
        => HomeFeatureLauncher.Open(HomeFeatureCatalog.Find("action-check-update"));

    private void OnOpenGithubClick(object sender, RoutedEventArgs e)
        => ShellHelper.TryOpen("https://github.com/qedsd/TheGuideToTheNewEden", out _);

    private void OnOpenQqGroupClick(object sender, RoutedEventArgs e)
        => ShellHelper.TryOpen("https://jq.qq.com/?_wv=1027&k=m8Ttv1DX", out _);

    // ---------- 数据卡 ----------

    private async void OnRefreshWalletClick(object sender, RoutedEventArgs e)
        => await _viewModel.Wallet.LoadAsync(forceRefresh: true);

    private void OnGoCharactersClick(object sender, RoutedEventArgs e)
        => Navigation.Navigate(typeof(CharactersShellPage));

    private void OnMarketItemClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is HomeMarketItemViewModel item)
        {
            Navigation.NavigateToMarket(item.TypeId);
        }
    }

    // ---------- 最近使用 ----------

    private void OnRecentClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is HomeShortcutTileViewModel tile)
        {
            _viewModel.OpenTile(tile);
        }
    }

    private void OnClearRecentClick(object sender, RoutedEventArgs e) => HomeShortcutService.ClearRecent();

    // ---------- 快捷方式磁贴 ----------

    private void OnTileClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is HomeShortcutTileViewModel tile)
        {
            _viewModel.OpenTile(tile);
        }
    }

    private static string Resolve(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;
}
