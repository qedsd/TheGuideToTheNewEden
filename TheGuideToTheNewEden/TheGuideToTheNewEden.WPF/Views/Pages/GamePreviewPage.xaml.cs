using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.WPF.Converters;
using TheGuideToTheNewEden.WPF.Services.GamePreview;
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
public partial class GamePreviewPage : Page
{
    /// <summary>源画面比例未知时（未选中进程）预览区的默认比例。</summary>
    private const double DefaultPreviewAspect = 16.0 / 9.0;

    private readonly GamePreviewViewModel _viewModel = new();
    private readonly SelectionPreview _selectionPreview = new();

    private bool _initialized;
    private bool _exitHooked;
    private PreviewItem? _subscribedItem;
    private ScrollViewer? _viewport;

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
        PreviewArea.SizeChanged += (_, _) => UpdatePreviewLayout();
        // 参数条显隐/换行会改变它占的高度（画面高度要给让位），这里跟着重算
        PreviewInfo.SizeChanged += (_, _) => UpdatePreviewLayout();
    }

    // ---------- 生命周期 ----------

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachViewportClamp();

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
            _viewModel.FillOrderFromProcesses();
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
        _selectionPreview.Dispose();
        _viewModel.Dispose();
    }

    private void OnSelectionPreviewStateChanged() => UpdatePreviewLayout();

    /// <summary>
    /// 预览区"宽度占满、高度按源画面比例"：这样画面不会被拉伸，也不会出现黑边。
    /// 高度夹在卡片可用高度内，超出时由缩略图按比例在内部留边（不会溢出被裁）。
    /// <para>
    /// 参数条与分隔线也在"画面 + 参数条"这一组里（垂直居中），所以高瘦的源窗口（比例 &lt; 1）必须先把它们的高度扣掉，
    /// 否则画面会占满整列、把参数条顶出可视区。
    /// </para>
    /// </summary>
    private void UpdatePreviewLayout()
    {
        var aspect = _selectionPreview.SourceAspect > 0 ? _selectionPreview.SourceAspect : DefaultPreviewAspect;
        var width = PreviewArea.ActualWidth;
        var availableHeight = PreviewArea.ActualHeight;

        if (PreviewInfo.Visibility == Visibility.Visible)
        {
            // 上方分隔线（1px）+ 它的上外边距（10px）
            availableHeight = Math.Max(0, availableHeight - PreviewInfo.ActualHeight - 11);
        }

        if (width > 0 && aspect > 0)
        {
            var height = width / aspect;
            if (availableHeight > 0)
            {
                height = Math.Min(height, availableHeight);
            }

            PreviewHost.Height = height;
        }

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
        _viewModel.FillOrderFromProcesses();
    }

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
            _viewModel.FillOrderFromProcesses();
        }
    }

    private void OnMoveDownClick(object sender, RoutedEventArgs e)
    {
        if (SelectFromSender(sender))
        {
            _viewModel.MoveSelected(1);
            _viewModel.FillOrderFromProcesses();
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

    private void OnItemHotkeyLostFocus(object sender, RoutedEventArgs e) => _viewModel.NotifySettingChanged(true);

    // ---------- 分组 ----------

    private void OnAddGroupClick(object sender, RoutedEventArgs e) => _viewModel.AddGroup();

    private void OnRemoveGroupClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is PreviewHotKeyGroup group)
        {
            _viewModel.RemoveGroup(group);
        }
    }

    private void OnApplyGroupClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is PreviewHotKeyGroup group)
        {
            _viewModel.UpdateGroup(group);
        }
    }

    // ---------- 分组角色点选 ----------

    private void OnGroupCharacterListLoaded(object sender, RoutedEventArgs e)
        => UpdateGroupMemberCount(sender as DependencyObject);

    private void OnGroupCharacterChipLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton chip
            && chip.DataContext is CharacterCandidate candidate
            && TryGetGroup(chip) is { } group)
        {
            chip.IsChecked = ContainsMember(group, candidate.Name);
        }
    }

    /// <summary>点胶囊 = 加入/移出该分组。成员关系写回 <see cref="PreviewHotKeyGroup.GameNames"/>，切换逻辑立即生效。</summary>
    private void OnGroupCharacterChipClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton chip
            || chip.DataContext is not CharacterCandidate candidate
            || TryGetGroup(chip) is not { } group)
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

        UpdateGroupMemberCount(chip);
        _viewModel.Save();
    }

    private void OnSelectAllGroupCharactersClick(object sender, RoutedEventArgs e)
    {
        if (TryGetGroup(sender) is not { } group)
        {
            return;
        }

        foreach (var candidate in _viewModel.CharacterCandidates)
        {
            group.GameNames.Add(candidate.Name);
        }

        SyncGroupChips(sender, group);
        UpdateGroupMemberCount(sender);
        _viewModel.Save();
    }

    private void OnClearGroupCharactersClick(object sender, RoutedEventArgs e)
    {
        if (TryGetGroup(sender) is not { } group)
        {
            return;
        }

        group.GameNames.Clear();
        SyncGroupChips(sender, group);
        UpdateGroupMemberCount(sender);
        _viewModel.Save();
    }

    /// <summary>手工加一个不在候选列表里的角色（未登录 / 未保存配置），加完立即出现胶囊并选中。</summary>
    private void OnAddGroupCharacterClick(object sender, RoutedEventArgs e)
    {
        if (TryGetGroup(sender) is not { } group
            || FindGroupRoot(sender as DependencyObject) is not { } root
            || FindByName<TextBox>(root, "GroupNewCharacter") is not { } input)
        {
            return;
        }

        var name = input.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        group.GameNames.Add(name);
        input.Text = string.Empty;
        _viewModel.AddCharacterCandidate(name);
        UpdateGroupMemberCount(sender);
        _viewModel.Save();
    }

    /// <summary>把成员关系同步到该卡片上的点选胶囊（HashSet 没有变更通知，只能手动刷）。</summary>
    private static void SyncGroupChips(object? element, PreviewHotKeyGroup group)
    {
        if (FindGroupRoot(element) is not { } root)
        {
            return;
        }

        foreach (var chip in FindDescendants<ToggleButton>(root))
        {
            if (chip.DataContext is CharacterCandidate candidate)
            {
                chip.IsChecked = ContainsMember(group, candidate.Name);
            }
        }
    }

    private static bool ContainsMember(PreviewHotKeyGroup group, string name)
        => group.GameNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

    private void UpdateGroupMemberCount(object? element)
    {
        if (FindGroupRoot(element) is not { } root
            || root.DataContext is not PreviewHotKeyGroup group
            || FindByName<TextBlock>(root, "GroupSelectedCount") is not { } count)
        {
            return;
        }

        var format = Application.Current?.TryFindResource("GamePreviewPage_GroupHotkey_Count") as string ?? "{0} / {1}";
        count.Text = string.Format(format, group.GameNames.Count, _viewModel.CharacterCandidates.Count);
    }

    /// <summary>
    /// 分组卡片（DataTemplate 生成）的最高一层"DataContext 是分组"的元素。
    /// 胶囊自身的 DataContext 是候选角色，所以要往上找到卡片根才能取分组、找同卡片的控件。
    /// </summary>
    private static FrameworkElement? FindGroupRoot(object? element)
    {
        FrameworkElement? root = null;
        var current = element as DependencyObject;
        while (current is not null)
        {
            if (current is FrameworkElement frameworkElement && frameworkElement.DataContext is PreviewHotKeyGroup)
            {
                root = frameworkElement;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return root;
    }

    private static PreviewHotKeyGroup? TryGetGroup(object? sender)
        => sender is FrameworkElement element
            ? element.DataContext as PreviewHotKeyGroup ?? FindGroupRoot(element)?.DataContext as PreviewHotKeyGroup
            : null;

    /// <summary>模板里的 x:Name 没有可靠的 namescope 入口，直接按 <see cref="FrameworkElement.Name"/> 在可视化树里找。</summary>
    private static T? FindByName<T>(DependencyObject root, string name)
        where T : FrameworkElement
        => FindDescendants<T>(root).FirstOrDefault(element => element.Name == name);

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

    // ---------- 顺序 ----------

    private void OnFillOrderClick(object sender, RoutedEventArgs e) => _viewModel.FillOrderFromProcesses();

    private void OnApplyOrderClick(object sender, RoutedEventArgs e) => _viewModel.ApplyOrderText();
}
