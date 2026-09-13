using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TheGuideToTheNewEden.WPF.ViewModels.Translation;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>
/// 「AI 翻译」页：像 AI 桌面端那样的对话记录界面（左侧多个对话、右侧完整记录 + 底部输入）。
/// <para>
/// DataContext 由承载它的页面 / 弹窗赋值为 <see cref="AiChatTranslationViewModel"/>。
/// 记录区用 <see cref="ItemsControl"/> 而不是 <c>ListBox</c>：列表项的选中语义会跟"在气泡里拖选文字"打架。
/// </para>
/// </summary>
public partial class AiTranslationChatView : UserControl
{
    public AiTranslationChatView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private AiChatTranslationViewModel? ViewModel => DataContext as AiChatTranslationViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel?.Init();
        if (ViewModel is { } vm)
        {
            vm.ScrollToEndRequested -= OnScrollToEndRequested;
            vm.ScrollToEndRequested += OnScrollToEndRequested;
        }

        // 记录条数变化（新记录）时滚到底部，模拟聊天窗口的行为
        ((INotifyCollectionChanged)Transcript.Items).CollectionChanged -= OnTranscriptChanged;
        ((INotifyCollectionChanged)Transcript.Items).CollectionChanged += OnTranscriptChanged;
        ScrollToEnd();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.ScrollToEndRequested -= OnScrollToEndRequested;
        }

        ((INotifyCollectionChanged)Transcript.Items).CollectionChanged -= OnTranscriptChanged;
        ViewModel?.Dispose();
    }
    private void OnScrollToEndRequested(object? sender, EventArgs e) => ScrollToEnd();

    private void OnTranscriptChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScrollToEnd();

    /// <summary>排到布局之后再滚，否则新气泡还没测量、滚不到真正的底部。</summary>
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

    private void OnTranslateClick(object sender, RoutedEventArgs e) => _ = ViewModel?.TranslateAsync();

    private void OnStopClick(object sender, RoutedEventArgs e) => ViewModel?.StopAll();

    private void OnClearInputClick(object sender, RoutedEventArgs e) => ViewModel?.ClearInput();

    /// <summary>原文气泡的「展开 / 收起」。</summary>
    private void OnToggleOriginalClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AiChatTurnViewModel turn })
        {
            turn.IsOriginalExpanded = !turn.IsOriginalExpanded;
        }
    }

    /// <summary>译文气泡的「展开 / 收起」（点过之后这条就不再被自动收起）。</summary>
    private void OnToggleTranslationClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AiChatTurnViewModel turn })
        {
            turn.ToggleTranslationExpansion();
        }
    }

    private void OnNewSessionClick(object sender, RoutedEventArgs e) => ViewModel?.NewSession();

    /// <summary>标题位置的铅笔按钮：开始重命名。</summary>
    private void OnRenameClick(object sender, RoutedEventArgs e) => ViewModel?.BeginRenameSelectedSession();

    /// <summary>会话列表右键「重命名」。</summary>
    private void OnRenameSessionClick(object sender, RoutedEventArgs e) =>
        (SessionList.SelectedItem as AiChatSessionViewModel)?.BeginRename();

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

    /// <summary>点别处（失焦）视为确认改名。</summary>
    private void OnTitleLostFocus(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedSession is { IsRenaming: true } session)
        {
            session.CommitRename();
        }
    }

    private void OnSwapLanguagesClick(object sender, RoutedEventArgs e) => ViewModel?.SwapLanguages();

    private void OnSwapPairClick(object sender, RoutedEventArgs e) => ViewModel?.SwapPair();

    private void OnDeleteSessionClick(object sender, RoutedEventArgs e) =>
        ViewModel?.DeleteSession(SessionList.SelectedItem as AiChatSessionViewModel);

    private void OnClearSessionClick(object sender, RoutedEventArgs e) => ViewModel?.ClearCurrentSession();

    private void OnConfigureClick(object sender, RoutedEventArgs e) => ViewModel?.OpenSettings();

    private void OnRecheckClick(object sender, RoutedEventArgs e) => ViewModel?.RefreshConfiguration();

    private void OnPopWindowClick(object sender, RoutedEventArgs e) => TranslationPopup.ShowAi(this);

    private void OnCopyTranslationClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AiChatTurnViewModel turn })
        {
            AiChatTranslationViewModel.CopyToClipboard(turn.TranslationPlain);
        }
    }

    /// <summary>右键点在某个对话上时先选中它，保证菜单作用于鼠标下的那个。</summary>
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

    /// <summary>
    /// 回车立即翻译；<b>Shift+回车</b>保留为换行（整段 MOTD/邮件正文要能换行）。
    /// 用 PreviewKeyDown：多行 TextBox 会在 KeyDown 的类处理器里把回车当换行吞掉。
    /// </summary>
    private void OnInputPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            return;
        }

        e.Handled = true;
        _ = ViewModel?.TranslateAsync();
    }
}
