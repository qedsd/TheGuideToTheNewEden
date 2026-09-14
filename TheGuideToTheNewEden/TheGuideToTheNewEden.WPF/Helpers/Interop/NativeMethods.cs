using System.Runtime.InteropServices;
using System.Text;

namespace TheGuideToTheNewEden.WPF.Helpers.Interop;

/// <summary>
/// 多开功能用到的 Win32 互操作。<b>坐标一律使用物理像素</b>（不做 DIP 换算），
/// 与 DWM 缩略图、窗口位置设置的坐标空间保持一致——WinUI 版在 DIP/物理之间来回换算，
/// 是"保存的尺寸与显示尺寸对不上"等一类问题的根源。
/// </summary>
internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;

        public int Height => Bottom - Top;

        public override string ToString() => $"({Left},{Top})-({Right},{Bottom})";
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    internal const uint MONITOR_DEFAULTTONEAREST = 2;

    internal const int SW_RESTORE = 9;

    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOZORDER = 0x0004;
    internal const uint SWP_NOACTIVATE = 0x0010;

    /// <summary>让预览窗口不进入 Alt+Tab 列表。</summary>
    internal const int GWL_EXSTYLE = -20;
    internal const int WS_EX_TOOLWINDOW = 0x00000080;

    /// <summary>显示/点击预览窗口时不激活它，避免抢走游戏窗口的焦点。</summary>
    internal const int WS_EX_NOACTIVATE = 0x08000000;

    /// <summary>鼠标点击穿透到下层窗口（用于预览窗口上的角色名叠加窗，避免挡住拖动/缩放）。</summary>
    internal const int WS_EX_TRANSPARENT = 0x00000020;

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DWM_BLURBEHIND
    {
        public int dwFlags;

        [MarshalAs(UnmanagedType.Bool)]
        public bool fEnable;

        public IntPtr hRgnBlur;

        [MarshalAs(UnmanagedType.Bool)]
        public bool fTransitionOnMaximized;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    private const int DWM_BB_ENABLE = 0x00000001;
    private const int DWM_BB_BLURREGION = 0x00000002;

    [DllImport("dwmapi.dll")]
    private static extern int DwmEnableBlurBehindWindow(IntPtr hWnd, ref DWM_BLURBEHIND pBlurBehind);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRectRgn(int x1, int y1, int x2, int y2);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern void SwitchToThisWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool fAltTab);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

    [DllImport("kernel32.dll")]
    internal static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    internal static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    internal static string GetWindowTitle(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return string.Empty;
        }

        var length = GetWindowTextLength(hWnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        var buffer = new StringBuilder(length + 1);
        GetWindowText(hWnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    /// <summary>取窗口所在显示器的工作区（物理像素）；失败时回退到主屏。</summary>
    internal static bool TryGetMonitorWorkArea(IntPtr hWnd, out RECT workArea)
    {
        workArea = default;
        var monitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        workArea = info.rcWork;
        return true;
    }

    /// <summary>按物理像素移动窗口（不改尺寸）。</summary>
    internal static void MoveWindow(IntPtr hWnd, int x, int y)
        => SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

    /// <summary>按物理像素设置窗口位置与尺寸。</summary>
    internal static void SetWindowBounds(IntPtr hWnd, int x, int y, int width, int height)
        => SetWindowPos(hWnd, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE);

    private const int WM_SIZE = 0x0005;

    /// <summary>
    /// 主动给窗口补一条 <c>WM_SIZE</c>（lParam 为客户区宽高）。
    /// <para>
    /// 用途：窗口尺寸是用 <c>SetWindowPos</c> 改的，正常情况下系统会自动发 <c>WM_SIZE</c>；
    /// 但实测遇到过 WPF 的布局尺寸与实际窗口脱节（布局停在旧尺寸，边框/内容都按旧尺寸绘制），
    /// 此时补发一条 <c>WM_SIZE</c> 能让 WPF 重新按真实尺寸布局（尺寸本身没变，所以不会引起二次缩放）。
    /// </para>
    /// </summary>
    internal static void NotifyClientSize(IntPtr hWnd, int width, int height)
    {
        if (hWnd == IntPtr.Zero || width <= 0 || height <= 0)
        {
            return;
        }

        // lParam = MAKELPARAM(宽, 高)
        var lParam = (IntPtr)((height << 16) | (width & 0xFFFF));
        SendMessage(hWnd, WM_SIZE, IntPtr.Zero, lParam);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// 让窗口客户区"可以透明"：DWM 之后才会把客户区里 <b>没有被内容画满</b> 的像素按 alpha 合成到桌面上，
    /// 也就是"窗口自己没画东西的地方真的透出后面的窗口"。
    /// <para>
    /// 传一个<b>空</b>的模糊区域：只要透明、不要模糊。
    /// </para>
    /// <para>
    /// 为什么需要它：预览窗口用 <c>ui:FluentWindow</c>，而 <c>AllowsTransparency</c> 与它冲突（实测抛
    /// <c>InvalidOperationException</c>），所以拿不到 WPF 的逐像素透明；只把 <c>Window.Background</c> 设成
    /// Transparent 也不够——非分层窗口的客户区会被 DWM 当作完全不透明处理（透明像素合成成黑色）。
    /// 把客户区标记为"玻璃"之后，alpha 才会被尊重：画面区交给 DWM 缩略图（<c>DwmThumbnail</c>，本身带不透明度）
    /// 合成，其余部分由 WPF 正常画成实色（标题栏、高亮边框、无画面时的占位底色）。
    /// </para>
    /// </summary>
    internal static void EnableTransparentClientArea(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return;
        }

        // ① 把 DWM 玻璃框扩展到整个客户区（四个 -1 = 铺满）：这一步才是"客户区可以透出桌面"的开关。
        var margins = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(hWnd, ref margins);

        // ② 模糊区域给一个空矩形：只要透明、不要模糊。
        var region = CreateRectRgn(-2, -2, -1, -1);
        try
        {
            var blurBehind = new DWM_BLURBEHIND
            {
                dwFlags = DWM_BB_ENABLE | DWM_BB_BLURREGION,
                fEnable = true,
                hRgnBlur = region,
            };

            DwmEnableBlurBehindWindow(hWnd, ref blurBehind);
        }
        finally
        {
            if (region != IntPtr.Zero)
            {
                DeleteObject(region);
            }
        }
    }
}
