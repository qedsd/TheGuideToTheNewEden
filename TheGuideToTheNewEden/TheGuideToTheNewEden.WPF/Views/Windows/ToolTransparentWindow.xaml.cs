using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Automation;
using TheGuideToTheNewEden.WPF.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 逐像素透明的工具窗外壳（配方照 <c>IntelWindow</c>，见其 XAML 注释与 §9 第 81 条）：
/// 普通 <see cref="Window"/> + <c>WindowStyle=None</c> + <c>AllowsTransparency=True</c>，
/// 拖动/缩放交给 <c>WindowChrome</c>，透明度作用在**背板 Border** 上（内容保持实色）。
/// <para>
/// 与 <see cref="ToolWindow"/> 的关系：两者都实现 <see cref="IToolWindowShell"/>，
/// 由 <c>ToolWindowHost</c> 按 <c>IToolPage.NeedsTransparentWindow</c> 二选一；
/// 本窗口刻意只提供工具窗需要的那点能力（标题栏三颗按钮 + 置顶 + 背板透明度），不重复 FluentWindow 那一套样式。
/// </para>
/// </summary>
public partial class ToolTransparentWindow : Window, IToolWindowShell
{
    private const double MinPlateOpacity = 0.05;

    private double _plateOpacity = 1.0;

    public ToolTransparentWindow(object? content, string titleKey, bool showInTaskbar, double width, double height)
    {
        InitializeComponent();

        Width = width;
        Height = height;
        ShowInTaskbar = showInTaskbar;
        ContentFrame.Content = content;

        SetResourceReference(TitleProperty, titleKey);
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    /// <summary>页面实例（与 <see cref="ToolWindow.GetContent"/> 对应）。</summary>
    public object? GetContent() => ContentFrame.Content;

    /// <summary>置顶显示（对应 <see cref="ToolWindow.SetAlwaysOnTop"/>）。</summary>
    public void SetAlwaysOnTop(bool top = true)
    {
        Topmost = top;
        UpdateTopmostVisual();
    }

    /// <summary>
    /// 背板不透明度：1 = 完全不透明（就是主题背景色），越小越能透出桌面，0.05 下限避免彻底看不见。
    /// 只改背板，不动窗口本身，也不动文字/按钮——挂机监控条要的就是"背景透、内容清晰"。
    /// </summary>
    public void SetPlateOpacity(double opacity)
    {
        _plateOpacity = Math.Clamp(opacity, MinPlateOpacity, 1.0);
        ApplyPlateBrush();
    }

    /// <summary>
    /// 标题栏按钮显隐，语义与 <see cref="ToolWindow.SetVisibleTitleBarButtons"/> 一致。
    /// 本外壳只有「置顶 / 最小化 / 关闭」三颗，<see cref="ToolWindowButtons.Maximize"/> **忽略**
    /// （透明挂机条不做最大化；要最大化请用完整布局）。
    /// </summary>
    public void SetVisibleTitleBarButtons(ToolWindowButtons buttons)
    {
        TopmostButton.Visibility = (buttons & ToolWindowButtons.Topmost) != 0 ? Visibility.Visible : Visibility.Collapsed;
        MinimizeButton.Visibility = (buttons & ToolWindowButtons.Minimize) != 0 ? Visibility.Visible : Visibility.Collapsed;
        CloseButton.Visibility = (buttons & ToolWindowButtons.Close) != 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyPlateBrush();
        UpdateTopmostVisual();

        // 主题切换后背板要跟着换色（§9 第 18 条：停着不动的界面也要颜色正确）
        ThemeService.ThemeChanged += OnThemeChanged;
    }

    private void OnClosed(object? sender, EventArgs e) => ThemeService.ThemeChanged -= OnThemeChanged;

    private void OnThemeChanged() => ApplyPlateBrush();

    /// <summary>背板画刷＝主题应用背景色 + 当前 alpha（ABGR 无关，这里是 WPF 的 ARGB）。</summary>
    private void ApplyPlateBrush()
    {
        var tint = (Application.Current?.TryFindResource("ApplicationBackgroundBrush") as SolidColorBrush)?.Color
                   ?? Color.FromRgb(0x20, 0x20, 0x20);

        BackdropPlate.Background = new SolidColorBrush(Color.FromArgb(
            (byte)Math.Round(_plateOpacity * 255),
            tint.R,
            tint.G,
            tint.B));
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
        TopmostButton.Appearance = Topmost ? ControlAppearance.Secondary : ControlAppearance.Transparent;

        var text = Topmost
            ? Application.Current?.TryFindResource("ToolWindow.TopmostOff") as string ?? "取消置顶"
            : Application.Current?.TryFindResource("ToolWindow.Topmost") as string ?? "置顶";
        TopmostButton.ToolTip = text;
        AutomationProperties.SetName(TopmostButton, text);
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
