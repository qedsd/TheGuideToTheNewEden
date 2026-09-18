using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>标题栏按钮组合（对齐 WinUI 版 <c>WindowTitleStyle</c>）。</summary>
public enum ToolWindowTitleStyle
{
    /// <summary>最小化 + 最大化 + 关闭。</summary>
    Default,
    /// <summary>只有关闭。</summary>
    OnlyClose,
    /// <summary>只有最小化。</summary>
    OnlyMini,
    /// <summary>只有最大化。</summary>
    OnlyMax,
    /// <summary>不显示任何标题按钮（标题与 logo 仍显示）。</summary>
    NoButton,
    /// <summary>整个标题栏都不显示。</summary>
    Empty,
    /// <summary>最小化 + 关闭。</summary>
    MiniAndClose,
}

/// <summary>
/// 标题栏按钮位标志：可任意组合，配合 <see cref="ToolWindow.SetVisibleTitleBarButtons"/>、
/// <see cref="ToolWindow.HideTitleBarButton"/>、<see cref="ToolWindow.ShowTitleBarButton"/> 使用。
/// </summary>
[Flags]
public enum ToolWindowButtons
{
    /// <summary>一个都不显示（注意标题栏本身仍在，只是没有按钮）。</summary>
    None = 0,
    Minimize = 1,
    Maximize = 2,
    Close = 4,
    /// <summary>标题栏上的"置顶"切换按钮。</summary>
    Topmost = 8,
    /// <summary>全部按钮。</summary>
    All = Minimize | Maximize | Close | Topmost,
}

/// <summary>
/// 工具窗口外壳：统一的标题栏样式（左上角 logo，右侧窗口名称）、可配置的标题按钮与置顶按钮，
/// 内容通过 <see cref="SetContent"/> / 构造函数传入（Page、UserControl 或任意 UIElement 均可，
/// 内部用 Frame 承载以规避 "Page 只能由 Window/Frame 承载" 的限制）。
/// 窗口标题（任务栏/Alt+Tab 文本）用 <see cref="SystemTitle"/>，标题栏文字用 <see cref="DisplayTitle"/>。
/// 是否显示在任务栏用 WPF 原生的 <see cref="Window.ShowInTaskbar"/>，是否置顶用 <see cref="Window.Topmost"/>。
/// </summary>
public partial class ToolWindow : FluentWindow
{
    private bool _closeToHide;

    public ToolWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyToTitleBar();
    }

    /// <summary>创建并初始化工具窗口。</summary>
    /// <param name="content">内容（Page / UserControl / UIElement）。</param>
    /// <param name="titleStyle">标题按钮组合。</param>
    /// <param name="showTopmostButton">是否在标题栏显示"置顶"切换按钮。</param>
    /// <param name="showInTaskbar">是否显示在任务栏与 Alt+Tab 列表。</param>
    public ToolWindow(
        object? content,
        ToolWindowTitleStyle titleStyle = ToolWindowTitleStyle.Default,
        bool showTopmostButton = false,
        bool showInTaskbar = true,
        int width = 800,
        int height = 600)
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyToTitleBar();

        SetContent(content);
        TitleStyle = titleStyle;
        ShowTopmostButton = showTopmostButton;
        ShowInTaskbar = showInTaskbar;
        Width = width;
        Height = height;
    }

    // ---------- 内容 ----------

    /// <summary>设置窗口内容。</summary>
    public void SetContent(object? content) => ContentFrame.Content = content;

    /// <summary>当前内容。</summary>
    public object? GetContent() => ContentFrame.Content;

    // ---------- 标题 ----------

    public static readonly DependencyProperty DisplayTitleProperty = DependencyProperty.Register(
        nameof(DisplayTitle),
        typeof(string),
        typeof(ToolWindow),
        new PropertyMetadata(null, OnTitleRelatedPropertyChanged));

    /// <summary>标题栏上显示的名称（logo 右侧）。</summary>
    public string? DisplayTitle
    {
        get => (string?)GetValue(DisplayTitleProperty);
        set => SetValue(DisplayTitleProperty, value);
    }

    public static readonly DependencyProperty SystemTitleProperty = DependencyProperty.Register(
        nameof(SystemTitle),
        typeof(string),
        typeof(ToolWindow),
        new PropertyMetadata(null, OnTitleRelatedPropertyChanged));

    /// <summary>系统窗口标题（任务栏 / Alt+Tab）；未设置时跟随 <see cref="DisplayTitle"/>。</summary>
    public string? SystemTitle
    {
        get => (string?)GetValue(SystemTitleProperty);
        set => SetValue(SystemTitleProperty, value);
    }

    public static readonly DependencyProperty ShowTitleBarProperty = DependencyProperty.Register(
        nameof(ShowTitleBar),
        typeof(bool),
        typeof(ToolWindow),
        new PropertyMetadata(true, OnTitleRelatedPropertyChanged));

    /// <summary>是否显示整个标题栏。</summary>
    public bool ShowTitleBar
    {
        get => (bool)GetValue(ShowTitleBarProperty);
        set => SetValue(ShowTitleBarProperty, value);
    }

    // ---------- 标题按钮 ----------

    public static readonly DependencyProperty ShowMinimizeButtonProperty = DependencyProperty.Register(
        nameof(ShowMinimizeButton), typeof(bool), typeof(ToolWindow), new PropertyMetadata(true, OnTitleRelatedPropertyChanged));

    /// <summary>是否显示最小化按钮。</summary>
    public bool ShowMinimizeButton
    {
        get => (bool)GetValue(ShowMinimizeButtonProperty);
        set => SetValue(ShowMinimizeButtonProperty, value);
    }

    public static readonly DependencyProperty ShowMaximizeButtonProperty = DependencyProperty.Register(
        nameof(ShowMaximizeButton), typeof(bool), typeof(ToolWindow), new PropertyMetadata(true, OnTitleRelatedPropertyChanged));

    /// <summary>是否显示最大化按钮（同时决定能否最大化：双击标题栏等途径）。</summary>
    public bool ShowMaximizeButton
    {
        get => (bool)GetValue(ShowMaximizeButtonProperty);
        set => SetValue(ShowMaximizeButtonProperty, value);
    }

    public static readonly DependencyProperty ShowCloseButtonProperty = DependencyProperty.Register(
        nameof(ShowCloseButton), typeof(bool), typeof(ToolWindow), new PropertyMetadata(true, OnTitleRelatedPropertyChanged));

    /// <summary>是否显示关闭按钮。</summary>
    public bool ShowCloseButton
    {
        get => (bool)GetValue(ShowCloseButtonProperty);
        set => SetValue(ShowCloseButtonProperty, value);
    }

    public static readonly DependencyProperty ShowTopmostButtonProperty = DependencyProperty.Register(
        nameof(ShowTopmostButton), typeof(bool), typeof(ToolWindow), new PropertyMetadata(false, OnTitleRelatedPropertyChanged));

    /// <summary>是否在标题栏显示"置顶"切换按钮。</summary>
    public bool ShowTopmostButton
    {
        get => (bool)GetValue(ShowTopmostButtonProperty);
        set => SetValue(ShowTopmostButtonProperty, value);
    }

    private ToolWindowTitleStyle _titleStyle = ToolWindowTitleStyle.Default;

    /// <summary>标题按钮组合；设置后会覆盖三个按钮的显隐。</summary>
    public ToolWindowTitleStyle TitleStyle
    {
        get => _titleStyle;
        set
        {
            _titleStyle = value;
            switch (value)
            {
                case ToolWindowTitleStyle.OnlyClose:
                    ShowMinimizeButton = false; ShowMaximizeButton = false; ShowCloseButton = true; ShowTitleBar = true;
                    break;
                case ToolWindowTitleStyle.OnlyMini:
                    ShowMinimizeButton = true; ShowMaximizeButton = false; ShowCloseButton = false; ShowTitleBar = true;
                    break;
                case ToolWindowTitleStyle.OnlyMax:
                    ShowMinimizeButton = false; ShowMaximizeButton = true; ShowCloseButton = false; ShowTitleBar = true;
                    break;
                case ToolWindowTitleStyle.NoButton:
                    ShowMinimizeButton = false; ShowMaximizeButton = false; ShowCloseButton = false; ShowTitleBar = true;
                    break;
                case ToolWindowTitleStyle.Empty:
                    ShowMinimizeButton = false; ShowMaximizeButton = false; ShowCloseButton = false; ShowTitleBar = false;
                    break;
                case ToolWindowTitleStyle.MiniAndClose:
                    ShowMinimizeButton = true; ShowMaximizeButton = false; ShowCloseButton = true; ShowTitleBar = true;
                    break;
                default:
                    ShowMinimizeButton = true; ShowMaximizeButton = true; ShowCloseButton = true; ShowTitleBar = true;
                    break;
            }
        }
    }

    // ---------- 行为 ----------

    /// <summary>置顶显示（对应 WinUI 版 SetAlwaysOnTop）。</summary>
    public void SetAlwaysOnTop(bool top = true) => Topmost = top;

    /// <summary>点关闭时改为隐藏窗口（对应 WinUI 版 SetCloseToHide）。</summary>
    public void SetCloseToHide() => _closeToHide = true;

    // ---------- 标题栏按钮的精细控制 ----------

    /// <summary>
    /// 一次性指定"要显示哪些标题栏按钮"（未列出的都隐藏），比逐个设 <c>ShowXxxButton</c> 更直观。
    /// 例：只留关闭 → <c>SetVisibleTitleBarButtons(ToolWindowButtons.Close)</c>；按钮全隐藏 → <c>ToolWindowButtons.None</c>
    /// （标题栏本身仍在；要连标题栏一起隐藏用 <see cref="ShowTitleBar"/>）。
    /// </summary>
    public void SetVisibleTitleBarButtons(ToolWindowButtons buttons)
    {
        ShowMinimizeButton = (buttons & ToolWindowButtons.Minimize) != 0;
        ShowMaximizeButton = (buttons & ToolWindowButtons.Maximize) != 0;
        ShowCloseButton = (buttons & ToolWindowButtons.Close) != 0;
        ShowTopmostButton = (buttons & ToolWindowButtons.Topmost) != 0;
        ApplyToTitleBar();
    }

    /// <summary>隐藏指定的一个（或几个）标题栏按钮，其余按钮保持不动。</summary>
    public void HideTitleBarButton(ToolWindowButtons buttons)
    {
        if ((buttons & ToolWindowButtons.Minimize) != 0)
        {
            ShowMinimizeButton = false;
        }

        if ((buttons & ToolWindowButtons.Maximize) != 0)
        {
            ShowMaximizeButton = false;
        }

        if ((buttons & ToolWindowButtons.Close) != 0)
        {
            ShowCloseButton = false;
        }

        if ((buttons & ToolWindowButtons.Topmost) != 0)
        {
            ShowTopmostButton = false;
        }

        ApplyToTitleBar();
    }

    /// <summary>显示指定的一个（或几个）标题栏按钮，其余按钮保持不动。</summary>
    public void ShowTitleBarButton(ToolWindowButtons buttons)
    {
        if ((buttons & ToolWindowButtons.Minimize) != 0)
        {
            ShowMinimizeButton = true;
        }

        if ((buttons & ToolWindowButtons.Maximize) != 0)
        {
            ShowMaximizeButton = true;
        }

        if ((buttons & ToolWindowButtons.Close) != 0)
        {
            ShowCloseButton = true;
        }

        if ((buttons & ToolWindowButtons.Topmost) != 0)
        {
            ShowTopmostButton = true;
        }

        ApplyToTitleBar();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_closeToHide && !_isShuttingDown)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private bool _isShuttingDown;

    /// <summary>真正退出应用前调用，避免 CloseToHide 拦截关闭。</summary>
    public void AllowClose() => _isShuttingDown = true;

    private static void OnTitleRelatedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ToolWindow)d).ApplyToTitleBar();

    /// <summary>把各开关应用到标题栏。XAML 属性可能在标题栏创建前被赋值，因此这里做空判断。</summary>
    private void ApplyToTitleBar()
    {
        if (TitleBarHost is null)
        {
            return;
        }

        TitleBarHost.Visibility = ShowTitleBar ? Visibility.Visible : Visibility.Collapsed;
        TitleBarHost.ShowMinimize = ShowMinimizeButton;
        TitleBarHost.ShowMaximize = ShowMaximizeButton;
        TitleBarHost.CanMaximize = ShowMaximizeButton;
        TitleBarHost.ShowClose = ShowCloseButton;

        if (DisplayTitle is { Length: > 0 } display)
        {
            TitleBarHost.Title = display;
            Title = SystemTitle is { Length: > 0 } system ? system : display;
        }
        else if (SystemTitle is { Length: > 0 } systemOnly)
        {
            Title = systemOnly;
        }

        TopmostButton.Visibility = ShowTopmostButton ? Visibility.Visible : Visibility.Collapsed;
        // 置顶按钮位于 WindowChrome 的标题区，显式声明为"可交互"，避免被当作标题栏拖动吞掉点击
        // （实测 WPF-UI 的 TitleBar 已通过 WM_NCHITTEST 返回 HTCLIENT，这里是双保险）
        System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(TopmostButton, true);
        UpdateTopmostVisual();
    }

    private void OnTopmostClick(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        UpdateTopmostVisual();
    }

    private void UpdateTopmostVisual()
    {
        if (TopmostIcon is null || TopmostButton is null)
        {
            return;
        }

        TopmostIcon.Symbol = Topmost ? SymbolRegular.PinOff24 : SymbolRegular.Pin24;
        // 已置顶时按钮用填充底色：状态一眼可辨（只切 Appearance，颜色交给主题，不缓存 Brush）
        TopmostButton.Appearance = Topmost ? ControlAppearance.Secondary : ControlAppearance.Transparent;

        var text = Topmost
            ? Application.Current?.TryFindResource("ToolWindow.TopmostOff") as string ?? "取消置顶"
            : Application.Current?.TryFindResource("ToolWindow.Topmost") as string ?? "置顶";
        TopmostButton.ToolTip = text;
        AutomationProperties.SetName(TopmostButton, text); // 图标按钮需要有可读名称（无障碍 + 自动化测试）
    }
}
