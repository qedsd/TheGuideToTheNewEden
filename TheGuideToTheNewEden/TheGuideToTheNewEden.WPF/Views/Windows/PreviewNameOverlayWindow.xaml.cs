using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using TheGuideToTheNewEden.WPF.Helpers.Interop;
using TheGuideToTheNewEden.WPF.Services.GamePreview;
using TheGuideToTheNewEden.WPF.Services.GamePreview.Overlay;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 预览窗口左上角的"角色名 + 通用信息行"叠加窗。
/// <para>
/// <b>为什么必须是独立窗口</b>：DWM 缩略图由系统在窗口自身内容之上合成，
/// 画在预览窗口里的任何 WPF 元素都会被缩略图盖住（实测：样式 1 的名称条与样式 0 的角标都看不见）。
/// 因此角色名只能放在一个独立的、位于预览窗口之上的小窗口里。
/// </para>
/// <para>
/// 该窗口：无边框、背景透明、不抢焦点（<c>ShowActivated=False</c> + <c>WS_EX_NOACTIVATE</c>）、
/// 不进 Alt+Tab 与任务栏，并且带 <c>WS_EX_TRANSPARENT</c> —— 鼠标点击**穿透**到下面的预览窗口，
/// 所以拖动与滚轮缩放不受影响。位置尺寸由 <see cref="GamePreviewWindow"/> 在每次刷新时用物理像素同步。
/// </para>
/// <para>
/// <b>阶段 355 起不再只显示角色名</b>：名字下方是一组"信息行"，内容来自
/// <see cref="PreviewOverlayHub"/> 的各个 provider（第一个是频道预警的"最近/最新"）。
/// 本类只按 <see cref="PreviewOverlaySeverity"/> 着色，不认识任何具体功能——
/// 以后加新内容只需要新增 provider，本文件与预览窗口的渲染逻辑都不用动。
/// </para>
/// <para>
/// 这里刻意用普通 <see cref="Window"/> 而不是 <c>ui:FluentWindow</c>：
/// <c>AllowsTransparency</c> 与 FluentWindow 冲突（后者会把 WindowStyle 重置为单边框，
/// 显示时抛 InvalidOperationException，见 REFACTORING 阶段 43 的实测记录）。
/// 这个窗口本身完全透明、无标题栏，不需要 WPF-UI 的外观。
/// </para>
/// </summary>
public sealed partial class PreviewNameOverlayWindow : Window
{
    /// <summary>信息行底色（跟随角色名叠加的背景色设置）。</summary>
    public static readonly DependencyProperty InfoBackgroundProperty = DependencyProperty.Register(
        nameof(InfoBackground),
        typeof(Brush),
        typeof(PreviewNameOverlayWindow),
        new PropertyMetadata(Brushes.Transparent));

    /// <summary>信息行文字色（跟随角色名叠加的文字颜色设置）。</summary>
    public static readonly DependencyProperty InfoForegroundProperty = DependencyProperty.Register(
        nameof(InfoForeground),
        typeof(Brush),
        typeof(PreviewNameOverlayWindow),
        new PropertyMetadata(Brushes.White));

    /// <summary>信息行字号（由"预警文字"自己的设置换算，不再从角色名字号派生）。</summary>
    public static readonly DependencyProperty InfoFontSizeProperty = DependencyProperty.Register(
        nameof(InfoFontSize),
        typeof(double),
        typeof(PreviewNameOverlayWindow),
        new PropertyMetadata(12d));

    /// <summary>信息行字体（与角色名叠加各自独立设置）。</summary>
    public static readonly DependencyProperty InfoFontFamilyProperty = DependencyProperty.Register(
        nameof(InfoFontFamily),
        typeof(FontFamily),
        typeof(PreviewNameOverlayWindow),
        new PropertyMetadata(new FontFamily("Microsoft YaHei UI")));

    /// <summary>
    /// 悬停详情用的 ToolTip（阶段 356）。
    /// <para>
    /// 叠加窗是鼠标穿透的、自己收不到 hover：由下面的预览窗检测光标后调 <see cref="SetDetails"/> 打开。
    /// 用**独立 ToolTip 浮层**而不是往叠加窗里加一块面板——信息行下面的区域要留给以后的功能，
    /// 不能被"详情"长期占位；浮层也不改变叠加窗尺寸，悬停/收起不会引发布局跳动。
    /// </para>
    /// </summary>
    private readonly ToolTip _detailTip;

    private readonly TextBlock _detailText;

    public PreviewNameOverlayWindow()
    {
        InitializeComponent();

        _detailText = new TextBlock
        {
            MaxWidth = 460,
            TextWrapping = TextWrapping.Wrap,
            // 必须显式左对齐：长内容折行时若走两端对齐，行内的分隔空格会被拉开，
            // 表现为"频道名 / 发言人 / 内容"间隔很远（用户反馈）。
            TextAlignment = TextAlignment.Left,
        };

        _detailTip = new ToolTip
        {
            Content = _detailText,
            // 挂在信息行上、出现在行下方：光标仍停在行上，不会因为浮层盖住光标而反复开关
            PlacementTarget = InfoList,
            Placement = PlacementMode.Relative,
            StaysOpen = true,
            UseLayoutRounding = true,
        };
    }

    /// <summary>左上角显示的角色名；传空则隐藏名字（只留信息行，用于"带标题栏"样式）。</summary>
    public string DisplayName
    {
        get => NameText.Text;
        set => NameText.Text = value ?? string.Empty;
    }

    /// <summary>名字单独控制显隐：带标题栏样式的窗口里名字已写在标题栏，这里只显示信息行。</summary>
    public void SetNameVisible(bool visible)
    {
        var next = visible ? Visibility.Visible : Visibility.Collapsed;
        if (Badge.Visibility != next)
        {
            Badge.Visibility = next;
        }
    }

    /// <summary>信息行底色/文字色/字号（由 <see cref="GamePreviewWindow"/> 按角色名外观与窗口宽度换算后传入）。</summary>
    public Brush InfoBackground
    {
        get => (Brush)GetValue(InfoBackgroundProperty);
        set => SetValue(InfoBackgroundProperty, value);
    }

    public Brush InfoForeground
    {
        get => (Brush)GetValue(InfoForegroundProperty);
        set => SetValue(InfoForegroundProperty, value);
    }

    public double InfoFontSize
    {
        get => (double)GetValue(InfoFontSizeProperty);
        set => SetValue(InfoFontSizeProperty, value);
    }

    public FontFamily InfoFontFamily
    {
        get => (FontFamily)GetValue(InfoFontFamilyProperty);
        set => SetValue(InfoFontFamilyProperty, value);
    }

    /// <summary>
    /// 套用信息行：空集合 = 整块隐藏。调用方随后应自行 <c>UpdateLayout()</c> 并重摆窗口
    /// （<c>SizeToContent</c> 改变了宽高，必须重新按物理像素 <see cref="SetBounds"/>）。
    /// </summary>
    public void SetInfoItems(IReadOnlyList<PreviewOverlayItem>? items)
    {
        if (items is { Count: > 0 })
        {
            InfoList.ItemsSource = items;
            InfoList.Visibility = Visibility.Visible;
        }
        else
        {
            InfoList.ItemsSource = null;
            InfoList.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>当前是否有信息行（决定"带标题栏样式"下叠加窗是否需要显示）。</summary>
    public bool HasInfoItems => InfoList.Visibility == Visibility.Visible;

    /// <summary>当前是否展开了悬停详情。</summary>
    public bool HasDetails => _detailTip.IsOpen;

    /// <summary>测试 / 排查用：悬停详情的文字元素（可直接渲染出来核对排版）。</summary>
    public FrameworkElement DetailsVisual => _detailText;

    /// <summary>
    /// 悬停详情：传 null / 空串即收起。由 <see cref="GamePreviewWindow"/> 在光标进入叠加区域时调用
    /// （叠加窗本身是鼠标穿透的，收不到鼠标消息）。
    /// </summary>
    public void SetDetails(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            if (_detailTip.IsOpen)
            {
                _detailTip.IsOpen = false;
            }

            return;
        }

        if (!string.Equals(_detailText.Text, text, StringComparison.Ordinal))
        {
            _detailText.Text = text;
        }

        // 用的是标准 ToolTip 外观（WPF-UI 主题）；只把字体/字号跟预警行对齐，
        // 颜色交给主题（深色主题下是深底浅字），不套用户给信息行自选的半透明底色。
        _detailText.FontFamily = InfoFontFamily;
        _detailText.FontSize = Math.Clamp(InfoFontSize, 12d, 18d);

        if (!_detailTip.IsOpen)
        {
            // 出现在信息行正下方，左对齐
            _detailTip.PlacementTarget = InfoList;
            _detailTip.Placement = PlacementMode.Relative;
            _detailTip.HorizontalOffset = 0;
            _detailTip.VerticalOffset = InfoList.ActualHeight + 6;
            _detailTip.IsOpen = true;
        }
    }

    /// <summary>角色名文字色（测试 / 排查用）。</summary>
    public Brush NameForeground => NameText.Foreground;

    /// <summary>
    /// 套用外观设置：角色名的字体/字号/文字色/背景色，以及信息行自己的字体/字号/文字色/背景色。
    /// <para>
    /// 两个颜色都带透明度：背景全透明就等于"只要文字"、文字透明就等于"只要底"，都是用户可自选的正常组合。
    /// 文字色不做自动反差（用户明确指定），默认白色配默认半透明黑底。
    /// </para>
    /// <para>
    /// 阶段 356：信息行（预警）的字体、字号、**文字色与背景色**都与角色名叠加各自独立——
    /// 用户希望预警行和名字能分开配色（名字颜色可能有高亮切换，预警行保持固定更好判读）。
    /// </para>
    /// </summary>
    public void ApplyAppearance(
        string? fontFamily,
        double fontSize,
        System.Drawing.Color foregroundColor,
        System.Drawing.Color backgroundColor,
        string? infoFontFamily,
        double infoFontSize,
        System.Drawing.Color infoForegroundColor,
        System.Drawing.Color infoBackgroundColor)
    {
        if (!string.IsNullOrWhiteSpace(fontFamily) && !string.Equals(NameText.FontFamily.Source, fontFamily, StringComparison.OrdinalIgnoreCase))
        {
            // 字体名可能来自别处（未安装/来自 WinUI 版的配置），解析失败就用默认字体，不抛异常
            try
            {
                NameText.FontFamily = new FontFamily(fontFamily);
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        }

        NameText.FontSize = Math.Max(1, fontSize);
        var foreground = PreviewColorHelper.ToBrush(foregroundColor);
        var background = PreviewColorHelper.ToBrush(backgroundColor);
        NameText.Foreground = foreground;
        Badge.Background = background;

        // 信息行：字体 / 字号 / 文字色 / 背景色都用自己的一套
        InfoForeground = PreviewColorHelper.ToBrush(infoForegroundColor);
        InfoBackground = PreviewColorHelper.ToBrush(infoBackgroundColor);
        InfoFontFamily = ResolveFontFamily(infoFontFamily) ?? InfoFontFamily;
        InfoFontSize = Math.Max(1, infoFontSize);

        // 字号变了尺寸也随之变，重新量一次（SizeToContent 会跟着更新）
        UpdateLayout();
    }

    /// <summary>解析字体名；未安装/非法名称返回 null（调用方保留原字体）。</summary>
    private static FontFamily? ResolveFontFamily(string? family)
    {
        if (string.IsNullOrWhiteSpace(family))
        {
            return null;
        }

        try
        {
            return new FontFamily(family);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(
            hwnd,
            NativeMethods.GWL_EXSTYLE,
            exStyle | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TRANSPARENT);
    }

    /// <summary>按物理像素摆放（不激活、不改 Z 序，保证仍贴在预览窗口之上）。</summary>
    public void SetBounds(int x, int y, int width, int height)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.SetWindowBounds(hwnd, x, y, Math.Max(1, width), Math.Max(1, height));
    }

    /// <summary>窗口销毁时把悬停浮层一起关掉（ToolTip 是独立 popup，不会随窗口自动消失）。</summary>
    protected override void OnClosed(EventArgs e)
    {
        _detailTip.IsOpen = false;
        base.OnClosed(e);
    }
}
