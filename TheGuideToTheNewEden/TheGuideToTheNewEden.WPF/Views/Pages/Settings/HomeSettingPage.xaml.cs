using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services.Home;
using TheGuideToTheNewEden.WPF.ViewModels.Home;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Settings;

/// <summary>
/// 设置 → 主页：显示策略 + 快捷方式的集中管理。
/// <para>
/// 与主页的编辑模式共用同一份数据（<see cref="HomeShortcutService"/>），因此两边改动会互相立刻反映；
/// 本页订阅服务的 <c>Changed</c> 事件重建列表（例如在主页里拖了顺序，切回本页就是新顺序）。
/// </para>
/// </summary>
public partial class HomeSettingPage : Page
{
    private bool _loading;

    public HomeSettingPage()
    {
        InitializeComponent();

        LoadToggles();
        Rebuild();

        WalletToggle.Checked += (_, _) => SetFlag(() => HomeShortcutService.ShowWalletCard = true);
        WalletToggle.Unchecked += (_, _) => SetFlag(() => HomeShortcutService.ShowWalletCard = false);
        MarketToggle.Checked += (_, _) => SetFlag(() => HomeShortcutService.ShowMarketCard = true);
        MarketToggle.Unchecked += (_, _) => SetFlag(() => HomeShortcutService.ShowMarketCard = false);
        RecentToggle.Checked += (_, _) => SetFlag(() => HomeShortcutService.ShowRecent = true);
        RecentToggle.Unchecked += (_, _) => SetFlag(() => HomeShortcutService.ShowRecent = false);
        GroupedToggle.Checked += (_, _) => SetFlag(() => HomeShortcutService.ShowGrouped = true);
        GroupedToggle.Unchecked += (_, _) => SetFlag(() => HomeShortcutService.ShowGrouped = false);

        HomeShortcutService.Changed += OnShortcutsChanged;
        Unloaded += (_, _) => HomeShortcutService.Changed -= OnShortcutsChanged;
    }

    private void LoadToggles()
    {
        _loading = true;
        WalletToggle.IsChecked = HomeShortcutService.ShowWalletCard;
        MarketToggle.IsChecked = HomeShortcutService.ShowMarketCard;
        RecentToggle.IsChecked = HomeShortcutService.ShowRecent;
        GroupedToggle.IsChecked = HomeShortcutService.ShowGrouped;
        ClearRecentButton.IsEnabled = HomeShortcutService.Recent.Count > 0;
        _loading = false;
    }

    private void SetFlag(Action apply)
    {
        if (_loading)
        {
            return;
        }

        apply();
    }

    private void OnShortcutsChanged()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(OnShortcutsChanged);
            return;
        }

        Rebuild();
    }

    /// <summary>按服务当前数据重建列表（复用主页磁贴 VM，标题/副标题的取法完全一致）。</summary>
    private void Rebuild()
    {
        var tiles = new List<HomeShortcutTileViewModel>();
        foreach (var item in HomeShortcutService.Items)
        {
            if (HomeFeatureCatalog.Find(item.Id) is { } feature)
            {
                tiles.Add(new HomeShortcutTileViewModel(item, feature));
            }
        }

        ShortcutList.ItemsSource = tiles;
        ClearRecentButton.IsEnabled = HomeShortcutService.Recent.Count > 0;
    }

    private void OnMoveUpClick(object sender, RoutedEventArgs e) => Move(sender, -1);

    private void OnMoveDownClick(object sender, RoutedEventArgs e) => Move(sender, 1);

    private void Move(object sender, int offset)
    {
        if (sender is not FrameworkElement { Tag: HomeShortcutTileViewModel tile } || tile.Item is null)
        {
            return;
        }

        var items = HomeShortcutService.Items.ToList();
        var index = items.FindIndex(p => p.Key == tile.Item.Key);
        if (index < 0)
        {
            return;
        }

        HomeShortcutService.MoveTo(tile.Id, tile.Param, index + offset);
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: HomeShortcutTileViewModel tile })
        {
            // 用 RemoveExact：带参数的磁贴只删它自己，不影响同功能的其它磁贴。
            HomeShortcutService.RemoveExact(tile.Id, tile.Param);
        }
    }

    private void OnAddClick(object sender, RoutedEventArgs e) => Views.Windows.AddShortcutWindow.ShowWindow();

    private void OnRestoreClick(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            FindString("Home.Quick.RestoreConfirm"),
            FindString("Home.Quick.RestoreDefaults"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            HomeShortcutService.ResetToDefault();
        }
    }

    private void OnClearRecentClick(object sender, RoutedEventArgs e) => HomeShortcutService.ClearRecent();

    private static string FindString(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;
}
