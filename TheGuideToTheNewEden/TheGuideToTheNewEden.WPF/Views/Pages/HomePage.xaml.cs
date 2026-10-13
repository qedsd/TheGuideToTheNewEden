using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
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
public partial class HomePage : Page
{
    private readonly HomePageViewModel _viewModel = new();

    private bool _languageHooked;

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

        EnsureHeroImage();
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
    /// 品牌图（<c>Assets/home.jpg</c>）在代码里加载而不是写在 XAML 里：
    /// 资源万一没打进程序集，XAML 里的 pack URI 会在页面构造时抛异常（导航直接失败）；
    /// 放在这里失败只是退化成深色渐变底色。
    /// </summary>
    private void EnsureHeroImage()
    {
        if (HeroImage.ImageSource is not null)
        {
            return;
        }

        try
        {
            HeroImage.ImageSource = new BitmapImage(new Uri("pack://application:,,,/Assets/home.jpg", UriKind.Absolute));
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"[主页] 品牌图加载失败，改用渐变底色：{ex.Message}");
        }
    }

    // ---------- 品牌条 ----------

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

    private void OnGoMarketClick(object sender, RoutedEventArgs e)
        => Navigation.Navigate(typeof(MarketPage));

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
