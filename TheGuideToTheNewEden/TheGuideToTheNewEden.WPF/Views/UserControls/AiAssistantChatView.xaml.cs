using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TheGuideToTheNewEden.WPF.Services.Assistant;
using TheGuideToTheNewEden.WPF.ViewModels.Assistant;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>
/// 「AI 助手」对话视图：左侧对话列表、右侧记录（提问气泡 + 工具步骤 + Markdown 回答 + 确认卡片）、底部输入。
/// <para>
/// **VM 生命周期由宿主页面负责**（<c>Init</c>/<c>Dispose</c>）——弹窗与页面**共享同一个 VM 实例**，
/// 本视图只负责自己的滚动订阅，避免弹窗关闭时把共享 VM 的语言事件退订掉。
/// </para>
/// <para>
/// 阶段 133 按"常见 harness"的观感重排：去卡片化（侧栏浅色底 + 1px 分隔线）、回答不套气泡、
/// 工具调用压成暗色小字、单一圆角输入区；控件名与事件处理函数保持不变。
/// </para>
/// </summary>
public partial class AiAssistantChatView : UserControl
{
    public AiAssistantChatView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private AiAssistantViewModel? ViewModel => DataContext as AiAssistantViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.ScrollToEndRequested -= OnScrollToEndRequested;
            vm.ScrollToEndRequested += OnScrollToEndRequested;
        }

        ((INotifyCollectionChanged)Transcript.Items).CollectionChanged -= OnTranscriptChanged;
        ((INotifyCollectionChanged)Transcript.Items).CollectionChanged += OnTranscriptChanged;
        UpdatePopWindowButton();
        ApplySidebarState(AiAssistantSettingService.SidebarCollapsed);
        ScrollToEnd();
    }

    /// <summary>
    /// 应用"对话列表收起 / 展开"（阶段 133 追加：用户要求能把左栏收起来）。
    /// <para>
    /// 收起 = 侧栏 <see cref="Visibility.Collapsed"/>（左列是 Auto，自然变 0）+ 顶栏按钮换成"展开"图标与提示。
    /// 用"Auto 列 + 侧栏定宽"而不是改 <c>ColumnDefinition.Width</c>：收起/展开只动一个可见性，少一层联动。
    /// </para>
    /// <para>
    /// ⚠ 离屏出图核验时曾误判"改 ColumnDefinition.Width 无效"——实际是 <c>RenderTargetBitmap</c> 在
    /// Collapsed 之后没重排就渲染了（出图前要 <c>UpdateLayout()</c>），不是产品问题。
    /// </para>
    /// </summary>
    private void ApplySidebarState(bool collapsed)
    {
        SidebarHost.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;

        SidebarToggleCollapseIcon.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        SidebarToggleExpandIcon.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;

        var text = FindString(collapsed ? "Assistant_Chat_SidebarExpand" : "Assistant_Chat_SidebarCollapse");
        SidebarToggle.ToolTip = text;
        AutomationProperties.SetName(SidebarToggle, text);
    }

    /// <summary>顶栏按钮：收起 / 展开对话列表（状态记住，见 <see cref="AiAssistantSettingService.SidebarCollapsed"/>）。</summary>
    private void OnToggleSidebarClick(object sender, RoutedEventArgs e)
    {
        var collapsed = SidebarHost.Visibility == Visibility.Visible;   // 现在是展开 → 这次点成收起
        ApplySidebarState(collapsed);
        AiAssistantSettingService.SaveSidebarCollapsed(collapsed);
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;

    /// <summary>
    /// 「弹窗」按钮**是否该显示**：只有宿主窗口不是"AI 独立窗口"时才显示。
    /// <para>
    /// 抽成纯函数是为了可离线断言（离屏的 <c>ToolWindow</c> 内容不进可视树，探针查不到里面的按钮——
    /// 与 §9 第 101 条同类的环境限制）。
    /// </para>
    /// </summary>
    public static bool ShouldShowPopWindowButton(Window? host, Window? assistantWindow)
        => host is null || !ReferenceEquals(host, assistantWindow);

    /// <summary>
    /// 「弹窗」按钮只在**主窗口里的对话页**显示：本视图被"AI 独立窗口"（<see cref="Windows.AssistantWindow"/>）
    /// 承载时，点它只会把当前这个窗口关掉，容易误导（阶段 128）。
    /// </summary>
    private void UpdatePopWindowButton()
    {
        PopWindowButton.Visibility = ShouldShowPopWindowButton(
            Window.GetWindow(this), Windows.AssistantWindow.CurrentWindow)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.ScrollToEndRequested -= OnScrollToEndRequested;
        }

        ((INotifyCollectionChanged)Transcript.Items).CollectionChanged -= OnTranscriptChanged;
    }

    private void OnScrollToEndRequested(object? sender, EventArgs e) => ScrollToEnd();

    private void OnTranscriptChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScrollToEnd();

    private void ScrollToEnd() => Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
    {
        try
        {
            TranscriptScroll.ScrollToEnd();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }));

    private void OnSendClick(object sender, RoutedEventArgs e) => _ = ViewModel?.SendAsync();

    private void OnStopClick(object sender, RoutedEventArgs e) => ViewModel?.StopAll();

    private void OnNewSessionClick(object sender, RoutedEventArgs e) => ViewModel?.NewSession();

    /// <summary>空态里的示例：点一下就把示例当提问发出去（阶段 133 的 harness 式空态）。</summary>
    private void OnExampleClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.ButtonBase { Content: string text } && !string.IsNullOrWhiteSpace(text))
        {
            ViewModel?.SubmitQuery(text);
        }
    }

    private void OnRenameSessionClick(object sender, RoutedEventArgs e) =>
        (SessionList.SelectedItem as AiChatSessionViewModel)?.BeginRename();

    private void OnDeleteSessionClick(object sender, RoutedEventArgs e) =>
        ViewModel?.DeleteSession(SessionList.SelectedItem as AiChatSessionViewModel);

    private void OnClearSessionClick(object sender, RoutedEventArgs e) => ViewModel?.ClearCurrentSession();

    private void OnConfigureClick(object sender, RoutedEventArgs e) => ViewModel?.OpenSettings();

    private void OnRecheckClick(object sender, RoutedEventArgs e) => ViewModel?.RefreshConfiguration();

    /// <summary>「弹窗」：打开/收起独立工具窗口（与页面共享同一个 VM）。</summary>
    private void OnPopWindowClick(object sender, RoutedEventArgs e) => Windows.AssistantWindow.Toggle(this);

    /// <summary>请求气泡的「展开 / 收起」。</summary>
    private void OnToggleUserClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AiChatTurnViewModel turn })
        {
            turn.IsUserExpanded = !turn.IsUserExpanded;
        }
    }

    /// <summary>回答气泡的「展开 / 收起」（点过之后这条不再被自动收起）。</summary>
    private void OnToggleAnswerClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AiChatTurnViewModel turn })
        {
            turn.ToggleAnswerExpansion();
        }
    }

    /// <summary>确认卡片：应用挂起的写操作。</summary>
    private void OnConfirmApplyClick(object sender, RoutedEventArgs e) => ViewModel?.ResolveConfirmation(true);

    /// <summary>确认卡片：拒绝挂起的写操作。</summary>
    private void OnConfirmRejectClick(object sender, RoutedEventArgs e) => ViewModel?.ResolveConfirmation(false);

    private void OnCopyAnswerClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AiChatTurnViewModel turn })
        {
            AiAssistantViewModel.CopyToClipboard(turn.AnswerText);
        }
    }

    /// <summary>双击标题开始重命名。</summary>
    private void OnTitleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ViewModel?.BeginRenameSelectedSession();
            e.Handled = true;
        }
    }

    /// <summary>改名输入框：回车确认、Esc 取消。</summary>
    private void OnTitleKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel?.SelectedSession is not { } session)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            session.CommitRename();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            session.CancelRename();
            e.Handled = true;
        }
    }

    private void OnTitleLostFocus(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedSession is { IsRenaming: true } session)
        {
            session.CommitRename();
        }
    }

    /// <summary>右键点在某个对话上时先选中它。</summary>
    private void OnSessionPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (ItemsControl.ContainerFromElement(listBox, source) is ListBoxItem item)
        {
            item.IsSelected = true;
        }
    }

    /// <summary>回车发送；Shift+回车换行（多行 TextBox 会在 KeyDown 类处理器里吞掉回车，所以用 Preview）。</summary>
    private void OnInputPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            return;
        }

        e.Handled = true;
        _ = ViewModel?.SendAsync();
    }
}
