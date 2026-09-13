using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using TheGuideToTheNewEden.WPF.Helpers.Interop;
using TheGuideToTheNewEden.WPF.Services.GamePreview;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 预览窗口左上角的"角色名"叠加窗。
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
/// 这里刻意用普通 <see cref="Window"/> 而不是 <c>ui:FluentWindow</c>：
/// <c>AllowsTransparency</c> 与 FluentWindow 冲突（后者会把 WindowStyle 重置为单边框，
/// 显示时抛 InvalidOperationException，见 REFACTORING 阶段 43 的实测记录）。
/// 这个窗口本身完全透明、无标题栏，不需要 WPF-UI 的外观。
/// </para>
/// </summary>
public sealed partial class PreviewNameOverlayWindow : Window
{
    public PreviewNameOverlayWindow()
    {
        InitializeComponent();
    }

    /// <summary>左上角显示的角色名；传空则隐藏文字（只留半透明底会显得多余，调用方会整个隐藏窗口）。</summary>
    public string DisplayName
    {
        get => NameText.Text;
        set => NameText.Text = value ?? string.Empty;
    }

    /// <summary>
    /// 套用外观设置：字体、字号（已按需算好缩放后的实际字号）、文字色、背景色。
    /// <para>
    /// 两个颜色都带透明度：背景全透明就等于"只要文字"、文字透明就等于"只要底"，都是用户可自选的正常组合。
    /// 文字色不做自动反差（用户明确指定），默认白色配默认半透明黑底。
    /// </para>
    /// </summary>
    public void ApplyAppearance(
        string? fontFamily,
        double fontSize,
        System.Drawing.Color foregroundColor,
        System.Drawing.Color backgroundColor)
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
        NameText.Foreground = PreviewColorHelper.ToBrush(foregroundColor);
        Badge.Background = PreviewColorHelper.ToBrush(backgroundColor);

        // 字号变了尺寸也随之变，重新量一次（SizeToContent 会跟着更新）
        UpdateLayout();
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
}
