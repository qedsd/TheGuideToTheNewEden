using System.Runtime.InteropServices;

namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>
/// 通过 Win32 Get/SetWindowPlacement 读写窗口位置。
/// 全程使用物理像素，避免 WPF 在 Per-Monitor DPI 下 Left/Top 逻辑坐标导致的跨显示器错位。
/// </summary>
internal static class WindowPlacementHelper
{
    public const int SwShowNormal = 1;
    public const int SwShowMaximized = 3;

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WindowPlacement
    {
        public int Length;
        public int Flags;
        public int ShowCmd;
        public Point MinPosition;
        public Point MaxPosition;
        public Rect NormalPosition;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WindowPlacement lpwndpl);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPlacement(IntPtr hWnd, ref WindowPlacement lpwndpl);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(Point pt, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [StructLayout(LayoutKind.Sequential)]
    public struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect WorkArea;
        public uint Flags;
    }

    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    private const uint MonitorDefaultToNearest = 0x00000002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    public static WindowPlacement CreateEmpty() => new() { Length = Marshal.SizeOf<WindowPlacement>() };

    public static bool TryGet(IntPtr handle, out WindowPlacement placement)
    {
        placement = CreateEmpty();
        return handle != IntPtr.Zero && GetWindowPlacement(handle, ref placement);
    }

    public static bool TrySet(IntPtr handle, WindowPlacement placement)
    {
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        placement.Length = Marshal.SizeOf<WindowPlacement>();
        return SetWindowPlacement(handle, ref placement);
    }

    /// <summary>
    /// 判断窗口矩形是否与当前虚拟桌面（所有显示器）有交集。
    /// 显示器被移除或分辨率变化时用于兜底，避免窗口被恢复到屏幕之外。
    /// </summary>
    public static bool IsVisibleOnAnyScreen(Rect rect)
    {
        var x = GetSystemMetrics(SmXVirtualScreen);
        var y = GetSystemMetrics(SmYVirtualScreen);
        var width = GetSystemMetrics(SmCxVirtualScreen);
        var height = GetSystemMetrics(SmCyVirtualScreen);

        return rect.Right > x
            && rect.Left < x + width
            && rect.Bottom > y
            && rect.Top < y + height;
    }

    /// <summary>
    /// 把窗口居中到鼠标所在的显示器（可选），否则居中到主显示器。
    /// 用于"没有历史位置"时的首次启动，避免多显示器环境下总是落在主屏。
    /// </summary>
    public static bool TryCenterOnMonitor(IntPtr handle, bool preferMonitorUnderCursor)
    {
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out var windowRect))
        {
            return false;
        }

        var monitor = IntPtr.Zero;
        if (preferMonitorUnderCursor && GetCursorPos(out var cursor))
        {
            monitor = MonitorFromPoint(cursor, MonitorDefaultToNearest);
        }

        Rect workArea;
        if (monitor != IntPtr.Zero)
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info))
            {
                return false;
            }

            workArea = info.WorkArea;
        }
        else
        {
            var x = GetSystemMetrics(SmXVirtualScreen);
            var y = GetSystemMetrics(SmYVirtualScreen);
            workArea = new Rect
            {
                Left = x,
                Top = y,
                Right = x + GetSystemMetrics(SmCxVirtualScreen),
                Bottom = y + GetSystemMetrics(SmCyVirtualScreen),
            };
        }

        var width = windowRect.Right - windowRect.Left;
        var height = windowRect.Bottom - windowRect.Top;
        var left = workArea.Left + ((workArea.Right - workArea.Left) - width) / 2;
        var top = workArea.Top + ((workArea.Bottom - workArea.Top) - height) / 2;

        return SetWindowPos(handle, IntPtr.Zero, left, top, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
    }
}