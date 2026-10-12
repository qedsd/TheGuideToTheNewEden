using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Home;
using TheGuideToTheNewEden.WPF.ViewModels.Home;
using TheGuideToTheNewEden.WPF.Views.Pages.Characters;
using TheGuideToTheNewEden.WPF.Views.Pages.Settings;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 主页：品牌 / 状态条 + 快捷方式磁贴网格（详见 <c>HOME_DESIGN.md</c>）。
/// <para>
/// 磁贴的点击 / 拖动排序 / 右键菜单都在这里处理（与本项目其余页面一致：用事件处理器而不是 Command），
/// 数据本身完全由 <see cref="HomeShortcutService"/> 持有——「导航右键固定」与「添加功能」窗口
/// 改的是同一份数据，页面只负责重建磁贴。
/// </para>
/// </summary>
public partial class HomePage : Page
{
    /// <summary>拖动磁贴时放进 DataObject 的自定义格式（带应用前缀，避免与外部拖放混淆）。</summary>
    private const string TileDragFormat = "TheGuideToTheNewEden/HomeShortcut";

    private readonly HomePageViewModel _viewModel = new();

    private bool _languageHooked;
    private Point _dragStartPoint;
    private HomeShortcutTileViewModel? _dragCandidate;

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

        // 离开主页就退出编辑态，切回来时是正常视图。
        _viewModel.EndEdit();
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
        if (HeroImage.Source is not null)
        {
            return;
        }

        try
        {
            HeroImage.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/home.jpg", UriKind.Absolute));
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

    // ---------- 「⋯」显示策略菜单 ----------

    private void OnMoreClick(object sender, RoutedEventArgs e)
    {
        if (MoreButton.ContextMenu is { } menu)
        {
            menu.PlacementTarget = MoreButton;
            menu.IsOpen = true;
        }
    }

    private void OnMoreMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu)
        {
            return;
        }

        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            item.Header = item.Tag switch
            {
                "grouped" => Resolve("Home.Setting.Grouped"),
                "wallet" => Resolve("Home.Setting.ShowWallet"),
                "market" => Resolve("Home.Setting.ShowMarket"),
                "recent" => Resolve("Home.Setting.ShowRecent"),
                "manage" => Resolve("Home.Quick.Manage"),
                "setting" => Resolve("SettingPage_Home"),
                _ => item.Header,
            };

            item.IsChecked = item.Tag switch
            {
                "grouped" => HomeShortcutService.ShowGrouped,
                "wallet" => HomeShortcutService.ShowWalletCard,
                "market" => HomeShortcutService.ShowMarketCard,
                "recent" => HomeShortcutService.ShowRecent,
                _ => false,
            };
        }
    }

    private void OnToggleGroupedClick(object sender, RoutedEventArgs e)
        => HomeShortcutService.ShowGrouped = !HomeShortcutService.ShowGrouped;

    private void OnToggleWalletClick(object sender, RoutedEventArgs e)
        => HomeShortcutService.ShowWalletCard = !HomeShortcutService.ShowWalletCard;

    private void OnToggleMarketClick(object sender, RoutedEventArgs e)
        => HomeShortcutService.ShowMarketCard = !HomeShortcutService.ShowMarketCard;

    private void OnToggleRecentClick(object sender, RoutedEventArgs e)
        => HomeShortcutService.ShowRecent = !HomeShortcutService.ShowRecent;

    private void OnMenuManageClick(object sender, RoutedEventArgs e) => AddShortcutWindow.ShowWindow();

    private void OnMenuHomeSettingClick(object sender, RoutedEventArgs e)
    {
        SettingsPage.RequestCategory(typeof(HomeSettingPage));
        Navigation.Navigate(typeof(SettingsPage));
    }

    // ---------- 快捷方式卡 ----------

    private void OnEditClick(object sender, RoutedEventArgs e) => _viewModel.ToggleEdit();

    private void OnAddClick(object sender, RoutedEventArgs e) => AddShortcutWindow.ShowWindow();

    private void OnRestoreDefaultsClick(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var result = owner is null
            ? MessageBox.Show(Resolve("Home.Quick.RestoreConfirm"), Resolve("Home.Quick.RestoreDefaults"),
                MessageBoxButton.YesNo, MessageBoxImage.Question)
            : MessageBox.Show(owner, Resolve("Home.Quick.RestoreConfirm"), Resolve("Home.Quick.RestoreDefaults"),
                MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _viewModel.RestoreDefaults();
        }
    }

    private void OnRootPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _viewModel.IsEditing)
        {
            _viewModel.EndEdit();
            e.Handled = true;
        }
    }

    // ---------- 磁贴：点击与移除 ----------

    private void OnTileClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is HomeShortcutTileViewModel tile)
        {
            _viewModel.OpenTile(tile);
        }
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is HomeShortcutTileViewModel tile)
        {
            _viewModel.RemoveTile(tile);
        }
    }

    private void OnTileKeyDown(object sender, KeyEventArgs e)
    {
        if (!_viewModel.IsEditing
            || Keyboard.Modifiers != ModifierKeys.Alt
            || (sender as FrameworkElement)?.DataContext is not HomeShortcutTileViewModel tile)
        {
            return;
        }

        if (e.Key == Key.Left)
        {
            _viewModel.MoveTileByOffset(tile, -1);
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            _viewModel.MoveTileByOffset(tile, 1);
            e.Handled = true;
        }
    }

    // ---------- 磁贴：右键菜单 ----------

    private void OnTileContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // 「＋ 添加功能」槽位没有"打开 / 移除"这类动作，直接不给菜单。
        if ((sender as FrameworkElement)?.DataContext is HomeShortcutTileViewModel { IsAddSlot: true })
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// 菜单文案在这里现取（不放 XAML 里的静态文本）：既能跟随语言切换，
    /// 又能按磁贴当前尺寸显示"切换为宽磁贴 / 切换为小磁贴"。
    /// </summary>
    private void OnTileMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu
            || menu.PlacementTarget is not FrameworkElement { DataContext: HomeShortcutTileViewModel tile })
        {
            return;
        }

        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            item.Header = item.Tag switch
            {
                "open" => Resolve("Home.Quick.Open"),
                "first" => Resolve("Home.Quick.MoveFirst"),
                "size" => Resolve(tile.IsWide ? "Home.Quick.ToSmall" : "Home.Quick.ToWide"),
                "remove" => Resolve("Home.Quick.Remove"),
                _ => item.Header,
            };
        }
    }

    private void OnMenuOpenClick(object sender, RoutedEventArgs e)
    {
        // 右键的"打开"是明确意图，编辑态下也照常打开（与左键点击不同）。
        if (TileFromMenu(sender) is { Feature: { } feature } tile)
        {
            HomeFeatureLauncher.Open(feature, tile.Param);
        }
    }

    private void OnMenuMoveFirstClick(object sender, RoutedEventArgs e)
    {
        if (TileFromMenu(sender) is { } tile)
        {
            _viewModel.MoveTileToFront(tile);
        }
    }

    private void OnMenuSizeClick(object sender, RoutedEventArgs e)
    {
        if (TileFromMenu(sender) is { } tile)
        {
            _viewModel.ToggleTileSize(tile);
        }
    }

    private void OnMenuRemoveClick(object sender, RoutedEventArgs e)
    {
        if (TileFromMenu(sender) is { } tile)
        {
            _viewModel.RemoveTile(tile);
        }
    }

    private static HomeShortcutTileViewModel? TileFromMenu(object sender)
        => sender is MenuItem { Parent: ContextMenu { PlacementTarget: FrameworkElement target } }
            ? target.DataContext as HomeShortcutTileViewModel
            : null;

    // ---------- 磁贴：拖动排序 ----------

    private void OnTileMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragCandidate = null;

        if (!_viewModel.IsEditing
            || sender is not Button button
            || button.DataContext is not HomeShortcutTileViewModel { IsAddSlot: false } tile)
        {
            return;
        }

        // 按在 ✕ 上时不要开始拖动（子按钮的鼠标事件在隧道阶段还没被标记为已处理）。
        if (VisualTreeSearch.IsInsideChildButton(e.OriginalSource as DependencyObject, button))
        {
            return;
        }

        _dragStartPoint = e.GetPosition(this);
        _dragCandidate = tile;
    }

    private void OnTileMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragCandidate is not { } tile || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(position.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _dragCandidate = null;
        tile.IsDragging = true;

        // 拖动的载荷用"功能 + 参数"的唯一键：同一功能可以有多块不同参数的磁贴。
        var key = tile.Item?.Key ?? tile.Id;

        try
        {
            DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(TileDragFormat, key), DragDropEffects.Move);
        }
        finally
        {
            ClearDragVisuals();
        }
    }

    private void OnTileDragOver(object sender, DragEventArgs e)
    {
        if (!TryGetDraggedId(e, out _)
            || sender is not FrameworkElement { DataContext: HomeShortcutTileViewModel target })
        {
            return;
        }

        ClearDropTargets();
        target.IsDropTarget = true;
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnTileDragLeave(object sender, DragEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is HomeShortcutTileViewModel tile)
        {
            tile.IsDropTarget = false;
        }
    }

    private void OnTileDrop(object sender, DragEventArgs e)
    {
        if (!TryGetDraggedId(e, out var draggedKey)
            || sender is not FrameworkElement element
            || element.DataContext is not HomeShortcutTileViewModel target)
        {
            return;
        }

        if (target.IsAddSlot || target.Item is null)
        {
            // 落在「＋」上 = 排到最后。
            _viewModel.DropTileAtEnd(draggedKey);
        }
        else
        {
            var position = e.GetPosition(element);
            _viewModel.DropTile(
                draggedKey, _viewModel.IndexOf(target.Item.Key), position.X < element.ActualWidth / 2);
        }

        ClearDragVisuals();
        e.Handled = true;
    }

    private static bool TryGetDraggedId(DragEventArgs e, out string id)
    {
        id = string.Empty;
        if (!e.Data.GetDataPresent(TileDragFormat) || e.Data.GetData(TileDragFormat) is not string draggedId)
        {
            return false;
        }

        id = draggedId;
        return true;
    }

    private void ClearDropTargets()
    {
        foreach (var tile in _viewModel.Tiles)
        {
            tile.IsDropTarget = false;
        }
    }

    private void ClearDragVisuals()
    {
        foreach (var tile in _viewModel.Tiles)
        {
            tile.IsDragging = false;
            tile.IsDropTarget = false;
        }
    }

    private static string Resolve(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;
}
