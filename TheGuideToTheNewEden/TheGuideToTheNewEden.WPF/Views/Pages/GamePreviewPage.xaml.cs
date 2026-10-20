using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.WPF.Converters;
using TheGuideToTheNewEden.WPF.Services.GamePreview;
using TheGuideToTheNewEden.WPF.Services.Assistant.Features;
using TheGuideToTheNewEden.WPF.ViewModels.GamePreview;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 多开页面（对齐 WinUI 版 GamePreviewMgrPage，但已移除 IPC 预览模式）：
/// 左进程列表 / 中设置（选中项 · 全局）/ 右选中进程实时预览。
/// <para>
/// 预览窗口由 <see cref="PreviewWindowManager"/> 独立持有，本页只负责展示与转发操作——
/// 因此切到别的页面时预览窗口与全局快捷键继续工作。页面被导航缓存，实例只创建一次。
/// </para>
/// </summary>
public partial class GamePreviewPage : Page, IAssistantFeatureRuntime
{
    /// <summary>源画面比例未知时（未选中进程）预览区的默认比例。</summary>
    private const double DefaultPreviewAspect = 16.0 / 9.0;

    /// <summary>页面内画面只是辅助视图：高度上限 220，不再让它把设置区挤走（阶段 135）。</summary>
    private const double MaxPreviewHeight = 220;

    /// <summary>画面再小也要保证空状态提示能完整显示。</summary>
    private const double MinPreviewHeight = 120;

    /// <summary>宽视口首次进入才展开画面侧栏；窄视口先收起，把宽度让给设置。</summary>
    private const double AutoShowPreviewWidth = 900;

    private readonly GamePreviewViewModel _viewModel = new();
    private readonly SelectionPreview _selectionPreview = new();

    private bool _initialized;
    private bool _previewVisibilityInitialized;
    private bool _exitHooked;
    private PreviewItem? _subscribedItem;
    private ScrollViewer? _viewport;

    /// <summary>已经出现过的候选角色名：用于区分"新增的客户端"与整表重建，避免每次重建都滚动胶囊区。</summary>
    private readonly HashSet<string> _knownCandidates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>把"视口值 − 内边距"的换算集中在一处（与 XAML 时代的写法同一语义）。</summary>
    private static readonly ViewportMaxHeightConverter ViewportInset = new();

    public GamePreviewPage()
    {
        InitializeComponent();
        DataContext = _viewModel;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.Setting.PropertyChanged += OnGlobalSettingPropertyChanged;
        // 候选角色每秒可能刷新：胶囊重建后要重新同步选中态、计数与顺序列表
        _viewModel.CharacterCandidates.CollectionChanged += OnCharacterCandidatesChanged;
        PreviewArea.SizeChanged += (_, _) => UpdatePreviewLayout();

        // 阶段 155：把"有运行态的多开"注册给 AI 的 feature_restart
        AssistantFeatureRuntime.Register(this);
    }

    // ---------- AI 功能重启（阶段 155） ----------

    public string FeatureKey => "gamepreview";

    public Task<string> RestartAsync() => _viewModel.RestartFromSettingsAsync();

    // ---------- 生命周期 ----------

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachViewportClamp();

        // 首次进入决定画面侧栏是否展开：宽视口才给画面留位置，窄视口优先保证设置区宽度。
        // 之后用户点"收起/展开"完全按用户选择，不再随窗口尺寸自动改变（避免来回跳）。
        if (!_previewVisibilityInitialized)
        {
            _previewVisibilityInitialized = true;
            var available = _viewport?.ViewportWidth ?? 0;
            if (available <= 0)
            {
                available = ActualWidth;
            }

            // 取不到宽度时保持展开（宁可让用户点一下收起，也不要默认藏掉功能）
            _viewModel.IsPreviewVisible = available <= 0 || available >= AutoShowPreviewWidth;
        }

        if (!_exitHooked)
        {
            _exitHooked = true;
            Application.Current.Exit += OnApplicationExit;
        }

        if (Window.GetWindow(this) is { } owner)
        {
            _selectionPreview.Attach(PreviewHost, owner);
            _selectionPreview.StateChanged += OnSelectionPreviewStateChanged;
            UpdateSelectionPreviewSource();
            UpdatePreviewLayout();
        }

        if (!_initialized)
        {
            _initialized = true;
            await _viewModel.InitializeAsync();
            _viewModel.RebuildSwitchOrder();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // 缩略图是直接画在主窗口上的，离开页面必须注销，否则那块区域会留下游戏画面
        _selectionPreview.StateChanged -= OnSelectionPreviewStateChanged;
        _selectionPreview.Detach();
        DetachViewportClamp();
    }

    // ---------- 视口夹取 ----------

    /// <summary>
    /// 把页面夹在承载页面的外层 <see cref="ScrollViewer"/> 视口内。
    /// <para>
    /// 为什么不能写在 XAML 里：页面被 <c>Frame</c> 承载时，<c>RelativeSource=FindAncestor,
    /// AncestorType=ScrollViewer</c> 实测解析不到（日志里只有一条 "Cannot find source for binding"），
    /// 页面于是被以无限高度测量——设置区自己的 ScrollViewer 永远拿不到受限高度（滚轮滚的是整页），
    /// 老的三列布局也会横向溢出。这里在 <c>Loaded</c> 后用可视树找一次，并在视口尺寸变化时刷新。
    /// </para>
    /// </summary>
    private void AttachViewportClamp()
    {
        if (_viewport is not null)
        {
            return;
        }

        _viewport = FindVisualAncestor<ScrollViewer>(this);
        if (_viewport is null)
        {
            return;
        }

        _viewport.SizeChanged += OnViewportSizeChanged;
        ApplyViewportClamp();
    }

    private void DetachViewportClamp()
    {
        if (_viewport is null)
        {
            return;
        }

        _viewport.SizeChanged -= OnViewportSizeChanged;
        _viewport = null;
    }

    private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e) => ApplyViewportClamp();

    private void ApplyViewportClamp()
    {
        if (_viewport is null)
        {
            return;
        }

        PageRoot.MaxHeight = Clamp(_viewport.ViewportHeight, 20);
        PageRoot.MaxWidth = Clamp(_viewport.ViewportWidth, 28);

        static double Clamp(double viewport, double inset)
            => (double)ViewportInset.Convert(viewport, typeof(double), inset.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    private static T? FindVisualAncestor<T>(DependencyObject child)
        where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent is not null)
        {
            if (parent is T match)
            {
                return match;
            }

            parent = VisualTreeHelper.GetParent(parent);
        }

        return null;
    }

    private void OnApplicationExit(object? sender, ExitEventArgs e)
    {
        AssistantFeatureRuntime.Unregister(this);
        _selectionPreview.Dispose();
        _viewModel.Dispose();
    }

    private void OnSelectionPreviewStateChanged() => UpdatePreviewLayout();

    /// <summary>
    /// 画面宽度占满侧栏、高度按源画面比例计算，并夹在
    /// <see cref="MinPreviewHeight"/>–<see cref="MaxPreviewHeight"/> 之间。
    /// <para>
    /// 阶段 135 起画面只是右侧辅助侧栏：不再"按可用高度铺满"，而是固定高度上限，
    /// 让设置区成为页面主体；超高/超宽源画面在缩略图内部按比例留边，不会拉伸。
    /// </para>
    /// </summary>
    private void UpdatePreviewLayout()
    {
        var aspect = _selectionPreview.SourceAspect > 0 ? _selectionPreview.SourceAspect : DefaultPreviewAspect;
        var width = PreviewArea.ActualWidth;
        if (width <= 0 || aspect <= 0)
        {
            return;
        }

        var height = Math.Clamp(width / aspect, MinPreviewHeight, MaxPreviewHeight);
        PreviewHost.Height = height;

        // 没有可用画面（未选中 / 客户端已退出 / 客户端已最小化）时显示占位提示。
        // "已最小化"由 SelectionPreview 归入"没有画面"，文案沿用既有键（其原文已含"已最小化"）。
        if (_selectionPreview.IsSourceAvailable)
        {
            PreviewHint.Visibility = Visibility.Collapsed;
        }
        else
        {
            var hasSelection = _viewModel.SelectedProcess is not null;
            PreviewHint.Visibility = Visibility.Visible;
            PreviewHint.SetResourceReference(
                TextBlock.TextProperty,
                hasSelection ? "GamePreviewPage_PreviewUnavailable" : "GamePreviewPage_PreviewTip");
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GamePreviewViewModel.SelectedProcess)
            or nameof(GamePreviewViewModel.SelectedSetting))
        {
            SubscribeSelectedItem();
            UpdateSelectionPreviewSource();
            UpdatePreviewLayout();
        }
        else if (e.PropertyName is nameof(GamePreviewViewModel.SelectedHotKeyGroup))
        {
            ScheduleGroupEditorSync();
        }
        else if (e.PropertyName is nameof(GamePreviewViewModel.SettingsTabIndex)
                 && _viewModel.SettingsTabIndex == 3)
        {
            // 切到"切换顺序"页签时按当前进程/顺序重建一次（页面常驻，不会重新 Loaded）
            _viewModel.RebuildSwitchOrder();
        }
    }

    /// <summary>选中项的设置改动 → 立即套用到预览窗口并保存。</summary>
    private void SubscribeSelectedItem()
    {
        if (ReferenceEquals(_subscribedItem, _viewModel.SelectedSetting))
        {
            return;
        }

        if (_subscribedItem is not null)
        {
            _subscribedItem.PropertyChanged -= OnSelectedItemPropertyChanged;
        }

        _subscribedItem = _viewModel.SelectedSetting;
        if (_subscribedItem is not null)
        {
            _subscribedItem.PropertyChanged += OnSelectedItemPropertyChanged;
        }
    }

    private void OnSelectedItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        => _viewModel.NotifySettingChanged(e.PropertyName == nameof(PreviewItem.HotKey));

    /// <summary>全局设置改动 → 保存（快捷键由 VM 的属性自行处理重新注册）。</summary>
    private void OnGlobalSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
        => _viewModel.Save();

    private void UpdateSelectionPreviewSource()
        => _selectionPreview.SetSource(_viewModel.SelectedProcess?.MainWindowHandle ?? IntPtr.Zero);

    // ---------- 进程列表 ----------

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshAsync();
        _viewModel.RebuildSwitchOrder();
    }

    /// <summary>收起 / 展开右侧画面侧栏（收起后设置区拿回全部宽度）。</summary>
    private void OnTogglePreviewClick(object sender, RoutedEventArgs e)
    {
        _viewModel.IsPreviewVisible = !_viewModel.IsPreviewVisible;

        // 展开时 PreviewArea 要等这一轮布局才有宽度；等布局完成再按新尺寸算画面高度。
        Dispatcher.BeginInvoke(new Action(UpdatePreviewLayout), DispatcherPriority.Loaded);
    }

    private void OnClearFilterClick(object sender, RoutedEventArgs e) => _viewModel.ClientFilter = string.Empty;

    private void OnStartAllClick(object sender, RoutedEventArgs e) => _viewModel.StartAll();

    private void OnStopAllClick(object sender, RoutedEventArgs e) => _viewModel.StopAll();

    /// <summary>右键菜单的 DataContext 不一定是当前选中项，先把它设为选中项再操作。</summary>
    private bool SelectFromSender(object sender)
    {
        if ((sender as FrameworkElement)?.DataContext is ProcessInfo process)
        {
            _viewModel.SelectedProcess = process;
            return true;
        }

        return _viewModel.SelectedProcess is not null;
    }

    private void OnStartProcessClick(object sender, RoutedEventArgs e)
    {
        if (SelectFromSender(sender))
        {
            _viewModel.StartSelected();
        }
    }

    private void OnStopProcessClick(object sender, RoutedEventArgs e)
    {
        if (SelectFromSender(sender))
        {
            _viewModel.StopSelected();
        }
    }

    private void OnMoveUpClick(object sender, RoutedEventArgs e)
    {
        if (SelectFromSender(sender))
        {
            _viewModel.MoveSelected(-1);
        }
    }

    private void OnMoveDownClick(object sender, RoutedEventArgs e)
    {
        if (SelectFromSender(sender))
        {
            _viewModel.MoveSelected(1);
        }
    }

    private void OnActivateSourceClick(object sender, RoutedEventArgs e)
    {
        if (SelectFromSender(sender) && _viewModel.SelectedProcess is { } process)
        {
            GameClientService.Activate(process.MainWindowHandle, _viewModel.Setting.SetForegroundWindowMode);
        }
    }

    // ---------- 启停 / 批量 ----------

    private void OnStartClick(object sender, RoutedEventArgs e) => _viewModel.StartSelected();

    private void OnStopClick(object sender, RoutedEventArgs e) => _viewModel.StopSelected();

    private void OnApplyUniformSizeClick(object sender, RoutedEventArgs e) => _viewModel.ApplyUniformSize();

    private void OnApplyAutoLayoutClick(object sender, RoutedEventArgs e) => _viewModel.ApplyAutoLayout();

    private void OnRestorePositionClick(object sender, RoutedEventArgs e)
    {
        if (SelectFromSender(sender))
        {
            _viewModel.RestorePosition();
        }
    }

    private void OnApplyToAllClick(object sender, RoutedEventArgs e) => _viewModel.ApplySelectedToAll();

    private void OnItemHotkeyApplied(object? sender, EventArgs e) => _viewModel.NotifySettingChanged(true);

    // ---------- 分组（独立页签：顶部选择器 + 编辑当前分组） ----------

    private void OnAddGroupClick(object sender, RoutedEventArgs e) => _viewModel.AddGroup();

    private void OnRemoveSelectedGroupClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedHotKeyGroup is not { } group)
        {
            return;
        }

        var text = string.Format(FindString("GamePreviewPage_GroupHotkey_RemoveConfirm"), group.GroupName);
        var title = FindString("GamePreviewPage_GroupHotkey_Remove");
        var owner = Window.GetWindow(this);
        var result = owner is null
            ? MessageBox.Show(text, title, MessageBoxButton.YesNo, MessageBoxImage.Warning)
            : MessageBox.Show(owner, text, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _viewModel.RemoveGroup(group);
        }
    }

    private void OnApplyGroupClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedHotKeyGroup is { } group)
        {
            _viewModel.UpdateGroup(group);
        }
    }

    // ---------- 分组单独排序 ----------

    /// <summary>
    /// "单独排序"开关：绑定先回写 UseGlobalOrder，等这一轮布局/绑定完成后再按最终模式重建并保存。
    /// 关闭时只保存模式，保留之前编辑过的自定义顺序（下次打开还能接着用）。
    /// </summary>
    private void OnGroupOrderModeClick(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _viewModel.RebuildGroupOrder();
            _viewModel.Save();
        }), DispatcherPriority.Loaded);
    }

    private void OnGroupOrderMoveUpClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is GroupOrderEntry entry)
        {
            _viewModel.MoveGroupOrderEntry(entry, -1);
        }
    }

    private void OnGroupOrderMoveDownClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is GroupOrderEntry entry)
        {
            _viewModel.MoveGroupOrderEntry(entry, 1);
        }
    }

    private void OnGroupOrderFillGlobalClick(object sender, RoutedEventArgs e) => _viewModel.FillGroupOrderFromGlobal();

    private void OnGroupOrderSortClick(object sender, RoutedEventArgs e) => _viewModel.SortGroupOrderByName();

    // ---------- 分组角色点选 ----------

    private void OnGroupCharacterListLoaded(object sender, RoutedEventArgs e) => SyncGroupEditor();

    private void OnGroupCharacterChipLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton chip
            && chip.DataContext is CharacterCandidate candidate
            && _viewModel.SelectedHotKeyGroup is { } group)
        {
            chip.IsChecked = ContainsMember(group, candidate.Name);
        }
    }

    /// <summary>点胶囊 = 加入/移出当前分组。成员关系写回 <see cref="PreviewHotKeyGroup.GameNames"/>，切换逻辑立即生效。</summary>
    private void OnGroupCharacterChipClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton chip
            || chip.DataContext is not CharacterCandidate candidate
            || _viewModel.SelectedHotKeyGroup is not { } group)
        {
            return;
        }

        if (chip.IsChecked == true)
        {
            // 先按忽略大小写清掉同名的旧写法，再写入候选列表里的标准写法（避免大小写不同的两份）
            group.GameNames.RemoveWhere(n => string.Equals(n, candidate.Name, StringComparison.OrdinalIgnoreCase));
            group.GameNames.Add(candidate.Name);
        }
        else
        {
            group.GameNames.RemoveWhere(n => string.Equals(n, candidate.Name, StringComparison.OrdinalIgnoreCase));
        }

        UpdateGroupMemberCount();
        _viewModel.RebuildGroupOrder();
        _viewModel.Save();
    }

    private void OnSelectAllGroupCharactersClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedHotKeyGroup is not { } group)
        {
            return;
        }

        foreach (var candidate in _viewModel.CharacterCandidates)
        {
            group.GameNames.Add(candidate.Name);
        }

        SyncGroupChips();
        UpdateGroupMemberCount();
        _viewModel.RebuildGroupOrder();
        _viewModel.Save();
    }

    private void OnClearGroupCharactersClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedHotKeyGroup is not { } group)
        {
            return;
        }

        group.GameNames.Clear();
        SyncGroupChips();
        UpdateGroupMemberCount();
        _viewModel.RebuildGroupOrder();
        _viewModel.Save();
    }

    /// <summary>手工加一个不在候选列表里的角色（未登录 / 未保存配置），加完立即出现胶囊并选中。</summary>
    private void OnAddGroupCharacterClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedHotKeyGroup is not { } group)
        {
            return;
        }

        var name = GroupNewCharacter.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        group.GameNames.Add(name);
        GroupNewCharacter.Text = string.Empty;
        _viewModel.AddCharacterCandidate(name);
        UpdateGroupMemberCount();
        SyncGroupChips();
        _viewModel.RebuildGroupOrder();
        _viewModel.Save();
    }

    /// <summary>把成员关系同步到编辑区的点选胶囊（HashSet 没有变更通知，只能手动刷）。</summary>
    private void SyncGroupChips()
    {
        if (_viewModel.SelectedHotKeyGroup is not { } group)
        {
            return;
        }

        foreach (var chip in FindDescendants<ToggleButton>(GroupCharacterList))
        {
            if (chip.DataContext is CharacterCandidate candidate)
            {
                chip.IsChecked = ContainsMember(group, candidate.Name);
            }
        }
    }

    private static bool ContainsMember(PreviewHotKeyGroup group, string name)
        => group.GameNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

    private void UpdateGroupMemberCount()
    {
        if (_viewModel.SelectedHotKeyGroup is not { } group)
        {
            return;
        }

        var format = FindString("GamePreviewPage_GroupHotkey_Count");
        GroupSelectedCount.Text = string.Format(format, group.GameNames.Count, _viewModel.CharacterCandidates.Count);
    }

    /// <summary>
    /// 候选角色集合变化：新客户端解析出角色名后，把对应胶囊滚进可视区
    /// （成员胶囊区最高 180 DIP，客户端多时新胶囊可能在折叠区下面，看起来像"没刷新"）。
    /// </summary>
    private void OnCharacterCandidatesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        CharacterCandidate? added = null;
        if (e.Action == NotifyCollectionChangedAction.Add
            && e.NewItems?[0] is CharacterCandidate candidate
            && _knownCandidates.Add(candidate.Name))
        {
            added = candidate;
        }

        // 整表重建时会有多次 Add：把当前所有名字记为"已出现"，后续重建就不会误判为新客户端
        foreach (var current in _viewModel.CharacterCandidates)
        {
            _knownCandidates.Add(current.Name);
        }

        if (added is not null && SettingsTabs.SelectedIndex == 2)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!GroupCharacterList.IsLoaded)
                {
                    return;
                }

                var chip = FindDescendants<ToggleButton>(GroupCharacterList)
                    .FirstOrDefault(c => c.DataContext is CharacterCandidate candidate
                        && string.Equals(candidate.Name, added.Name, StringComparison.OrdinalIgnoreCase));
                chip?.BringIntoView();
            }), DispatcherPriority.Loaded);
        }

        ScheduleGroupEditorSync();
    }

    /// <summary>
    /// 选中分组变了 / 候选集合变了：等 ItemsControl 生成完胶囊后再同步选中态与计数。
    /// 绑定刷新是异步的，必须排到 Loaded 优先级之后。
    /// </summary>
    private void ScheduleGroupEditorSync()
        => Dispatcher.BeginInvoke(new Action(SyncGroupEditor), DispatcherPriority.Loaded);

    private void SyncGroupEditor()
    {
        UpdateGroupMemberCount();
        SyncGroupChips();
        _viewModel.RebuildGroupOrder();
    }

    private static string FindString(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in FindDescendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    // ---------- 切换顺序 ----------

    private void OnOrderFillClick(object sender, RoutedEventArgs e) => _viewModel.FillSwitchOrderFromProcesses();

    private void OnOrderSortByNameClick(object sender, RoutedEventArgs e) => _viewModel.SortSwitchOrderByName();

    private void OnOrderRefreshClick(object sender, RoutedEventArgs e) => _viewModel.RebuildSwitchOrder();

    private void OnOrderMoveUpClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is SwitchOrderEntry entry)
        {
            _viewModel.MoveSwitchOrderEntry(entry, -1);
        }
    }

    private void OnOrderMoveDownClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is SwitchOrderEntry entry)
        {
            _viewModel.MoveSwitchOrderEntry(entry, 1);
        }
    }

    private void OnOrderRemoveClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is SwitchOrderEntry entry)
        {
            _viewModel.RemoveSwitchOrderEntry(entry);
        }
    }
}
