using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Home;
using TheGuideToTheNewEden.WPF.ViewModels.Home;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Settings;

/// <summary>
/// 主页设置：① 卡片显示——钱包 / 行情 / 最近使用三张卡片的显示开关（阶段 99 从主页「⋯」菜单移入）；
/// ② 快捷方式编辑（阶段 100 加入）——拖动排序、移除、添加、宽窄切换、恢复默认。
/// <para>
/// 阶段 101 起本页是快捷方式<b>全局唯一</b>的编辑入口：主页的编辑模式、「⋯」菜单和
/// 「磁贴按分组分段显示」都已移除，主页磁贴只剩"点开"一种交互。与主页共用
/// <see cref="HomeShortcutService"/> 一份数据（Changed 事件即时同步）。
/// </para>
/// </summary>
public partial class HomeSettingPage : Page
{
    /// <summary>拖动载荷格式：与主页磁贴同一"功能 + 参数"键（同一份数据，语义一致）。</summary>
    private const string RowDragFormat = "TheGuideToTheNewEden/HomeShortcut";

    private readonly ObservableCollection<HomeShortcutTileViewModel> _rows = [];
    private HomeShortcutTileViewModel? _dragCandidate;
    private Point _dragStartPoint;
    private DispatcherTimer? _autoScrollTimer;
    private double _autoScrollDelta;

    public HomeSettingPage()
    {
        InitializeComponent();

        WireCardToggles();
        RowList.ItemsSource = _rows;
        EnsureAutoScrollTimer();

        // 子页被设置壳缓存，Loaded/Unloaded 成对订阅（项目惯例），重进时重建列表。
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        HomeShortcutService.Changed += OnShortcutsChanged;
        LanguageService.LanguageChanged += OnLanguageChanged;
        RebuildRows();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        HomeShortcutService.Changed -= OnShortcutsChanged;
        LanguageService.LanguageChanged -= OnLanguageChanged;
        _autoScrollTimer?.Stop();
    }

    // ---------- 卡片显示 ----------

    private void WireCardToggles()
    {
        // 初始化赋值若与当前值不同会触发 Checked/Unchecked，但 setter 内 SetFlag 对同值短路、
        // 异值本就是本次要落盘的改动——无论顺序都安全。
        WalletToggle.Checked += (_, _) => HomeShortcutService.ShowWalletCard = true;
        WalletToggle.Unchecked += (_, _) => HomeShortcutService.ShowWalletCard = false;
        WalletToggle.IsChecked = HomeShortcutService.ShowWalletCard;

        MarketToggle.Checked += (_, _) => HomeShortcutService.ShowMarketCard = true;
        MarketToggle.Unchecked += (_, _) => HomeShortcutService.ShowMarketCard = false;
        MarketToggle.IsChecked = HomeShortcutService.ShowMarketCard;

        RecentToggle.Checked += (_, _) => HomeShortcutService.ShowRecent = true;
        RecentToggle.Unchecked += (_, _) => HomeShortcutService.ShowRecent = false;
        RecentToggle.IsChecked = HomeShortcutService.ShowRecent;
    }

    // ---------- 快捷方式列表 ----------

    private void OnShortcutsChanged() => RebuildRows();

    private void OnLanguageChanged(object? sender, string language)
    {
        foreach (var row in _rows)
        {
            row.RefreshTexts();
        }
    }

    private void RebuildRows()
    {
        _rows.Clear();
        foreach (var item in HomeShortcutService.Items)
        {
            if (HomeFeatureCatalog.Find(item.Id) is not { } feature)
            {
                continue; // 未知 id 已在服务层规范化时丢弃，这里仅防御
            }

            _rows.Add(new HomeShortcutTileViewModel(item, feature));
        }

        CountText.Text = string.Format(FindString("Home.Quick.Count"), _rows.Count, HomeShortcutService.MaxItems);
        AddButton.IsEnabled = !HomeShortcutService.IsFull;
        EmptyHint.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnAddClick(object sender, RoutedEventArgs e) => AddShortcutWindow.ShowWindow();

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: HomeShortcutTileViewModel row })
        {
            // 带参数磁贴只移除自身（与旧主页 ✕ 同规则）。
            HomeShortcutService.RemoveExact(row.Id, row.Param);
        }
    }

    /// <summary>行内宽窄切换：小 → 宽磁贴、宽 → 小磁贴（主页旧右键菜单同规则，入口移到本页）。</summary>
    private void OnSizeToggleClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: HomeShortcutTileViewModel row })
        {
            HomeShortcutService.SetSize(row.Id, row.Param, !row.IsWide);
        }
    }

    /// <summary>恢复默认快捷方式（带确认；入口从旧主页编辑提示条移到本页）。</summary>
    private void OnRestoreDefaultsClick(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var result = owner is null
            ? MessageBox.Show(FindString("Home.Quick.RestoreConfirm"), FindString("Home.Quick.RestoreDefaults"),
                MessageBoxButton.YesNo, MessageBoxImage.Question)
            : MessageBox.Show(owner, FindString("Home.Quick.RestoreConfirm"), FindString("Home.Quick.RestoreDefaults"),
                MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            HomeShortcutService.ResetToDefault();
        }
    }

    // ---------- 拖动排序（阶段 101 起是全局唯一的排序入口，交互沿用旧主页编辑模式） ----------

    private void OnRowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragCandidate = null;

        if (sender is not Border border
            || border.DataContext is not HomeShortcutTileViewModel row)
        {
            return;
        }

        // 按在 ✕ 上时不要开始拖动（子按钮的鼠标事件在隧道阶段还没被标记为已处理，与主页磁贴同规则）。
        if (IsInsideButton(e.OriginalSource as DependencyObject))
        {
            return;
        }

        _dragStartPoint = e.GetPosition(this);
        _dragCandidate = row;
    }

    private void OnRowMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragCandidate is not { } row || e.LeftButton != MouseButtonState.Pressed)
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
        row.IsDragging = true;

        var key = row.Item?.Key ?? row.Id;

        try
        {
            DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(RowDragFormat, key), DragDropEffects.Move);
        }
        finally
        {
            ClearRowDragVisuals();
            _autoScrollTimer?.Stop();
        }
    }

    private void OnRowDragOver(object sender, DragEventArgs e)
    {
        if (!TryGetDraggedRow(e, out _)
            || sender is not Border border
            || border.DataContext is not HomeShortcutTileViewModel target)
        {
            return;
        }

        ClearRowDropTargets();
        target.IsDropTarget = true;
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
        UpdateAutoScroll(e);
    }

    private void OnRowDragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border { DataContext: HomeShortcutTileViewModel target })
        {
            target.IsDropTarget = false;
        }
    }

    private void OnRowDrop(object sender, DragEventArgs e)
    {
        if (!TryGetDraggedRow(e, out var dragged)
            || dragged is null
            || sender is not Border border
            || border.DataContext is not HomeShortcutTileViewModel target)
        {
            return;
        }

        // 指针在上半段 → 插到该行之前；下半段 → 之后（与常见列表拖放一致）。
        var rowIndex = _rows.IndexOf(target);
        var gap = e.GetPosition(border).Y < border.ActualHeight / 2 ? rowIndex : rowIndex + 1;
        MoveRow(dragged, gap);
        e.Handled = true;
    }

    private void OnListDragOver(object sender, DragEventArgs e)
    {
        if (!TryGetDraggedRow(e, out _))
        {
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
        UpdateAutoScroll(e);
    }

    private void OnListDrop(object sender, DragEventArgs e)
    {
        if (!TryGetDraggedRow(e, out var dragged) || dragged is null)
        {
            return;
        }

        // 行间缝隙之外（列表空白处 / 末行下半段落到列表层）→ 移到末尾。
        MoveRow(dragged, _rows.Count);
        e.Handled = true;
    }

    /// <summary>
    /// 把拖动行移到"插入缝隙" <paramref name="gap"/>（0..N，指可见列表的缝隙位置）。
    /// <see cref="HomeShortcutService.MoveTo"/> 的下标基于移除前的原列表：
    /// 缝隙在拖动行之前 → 直接用缝隙下标；之后 → 移除自身后左移一格。
    /// </summary>
    private void MoveRow(HomeShortcutTileViewModel dragged, int gap)
    {
        var current = _rows.IndexOf(dragged);
        if (current < 0)
        {
            return;
        }

        var target = gap <= current ? gap : gap - 1;
        HomeShortcutService.MoveTo(dragged.Id, dragged.Param, target);
    }

    private bool TryGetDraggedRow(DragEventArgs e, out HomeShortcutTileViewModel? row)
    {
        if (e.Data.GetDataPresent(RowDragFormat) && e.Data.GetData(RowDragFormat) is string key)
        {
            row = _rows.FirstOrDefault(r => (r.Item?.Key ?? r.Id) == key);
            return row is not null;
        }

        row = null;
        return false;
    }

    private void ClearRowDragVisuals()
    {
        foreach (var row in _rows)
        {
            row.IsDragging = false;
            row.IsDropTarget = false;
        }
    }

    private void ClearRowDropTargets()
    {
        foreach (var row in _rows)
        {
            row.IsDropTarget = false;
        }
    }

    // ---------- 拖到视口边缘时自动滚动（列表最多 24 行，会超出视口） ----------

    private void EnsureAutoScrollTimer()
    {
        _autoScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _autoScrollTimer.Tick += (_, _) =>
            RootScroll.ScrollToVerticalOffset(RootScroll.VerticalOffset + _autoScrollDelta);
    }

    private void UpdateAutoScroll(DragEventArgs e)
    {
        var position = e.GetPosition(RootScroll);
        const double edge = 40;
        if (position.Y < edge)
        {
            _autoScrollDelta = -5;
        }
        else if (position.Y > RootScroll.ActualHeight - edge)
        {
            _autoScrollDelta = 5;
        }
        else
        {
            _autoScrollDelta = 0;
        }

        if (_autoScrollDelta != 0)
        {
            _autoScrollTimer?.Start();
        }
        else
        {
            _autoScrollTimer?.Stop();
        }
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Button)
            {
                return true;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private static string FindString(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;
}
