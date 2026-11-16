using System.Runtime.InteropServices;
using TheGuideToTheNewEden.WPF.Helpers.Interop;

namespace TheGuideToTheNewEden.WPF.Services.GamePreview;

/// <summary>
/// 原生 Win32 layered 缩略图画面层。
/// <para>
/// 背景：WPF 顶层窗口无法像 WinForms <c>Form.Opacity</c> 那样可靠地叠加 DWM 缩略图；
/// 这里用独立 WS_EX_LAYERED 顶层窗口承载 DWM 缩略图，由 <c>SetLayeredWindowAttributes</c>
/// 做整窗 alpha，缩略图自身 opacity 固定 255。它与 EVE-O Preview 的 WinForms Form 等价，
/// 但不引入 WinForms 依赖。
/// </para>
/// <para>
/// 窗口只覆盖预览窗的“游戏画面矩形”，WPF 标题栏/高亮边框/角色名叠加窗保持不变；
/// WS_EX_TRANSPARENT 让鼠标点击穿透到下面的 WPF 预览窗。
/// </para>
/// </summary>
internal sealed class PreviewThumbnailSurface : IDisposable
{
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int SW_HIDE = 0;
    private const int SW_SHOWNOACTIVATE = 4;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const int LWA_ALPHA = 0x00000002;

    private const string WindowClassName = "TheGuideToTheNewEden.PreviewThumbnailSurface";
    private static readonly WndProcDelegate WndProc = StaticWndProc;
    private static bool _classRegistered;

    private readonly IntPtr _hwnd;
    private readonly DwmThumbnail _thumbnail = new();
    private bool _visible;

    public bool IsRegistered => _hwnd != IntPtr.Zero && _thumbnail.IsRegistered;

    public PreviewThumbnailSurface(IntPtr owner, IntPtr source)
    {
        EnsureWindowClass();

        _hwnd = CreateWindowEx(
            WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT,
            WindowClassName,
            string.Empty,
            WS_POPUP,
            0,
            0,
            1,
            1,
            owner,
            IntPtr.Zero,
            GetModuleHandle(null),
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"创建预览缩略图 layered 窗口失败：Win32Error={Marshal.GetLastWin32Error()}");
        }

        // 先给一个完全不透明的 layered attribute；ShowAt 再按设置更新。
        NativeMethods.SetLayeredWindowAttributes(_hwnd, 0, 255, LWA_ALPHA);
        _thumbnail.TryRegister(_hwnd, source);
    }

    /// <summary>
    /// 显示/更新画面层。坐标是屏幕物理像素，宽高是游戏画面目标矩形。
    /// <para>
    /// <paramref name="source"/> 非空时只显示源窗口客户区中的该矩形（阶段 347 的自定义显示区域）。
    /// </para>
    /// </summary>
    public bool ShowAt(int x, int y, int width, int height, int opacityPercent, NativeMethods.RECT? source = null)
    {
        if (_hwnd == IntPtr.Zero || width <= 0 || height <= 0)
        {
            return false;
        }

        if (!_thumbnail.IsRegistered)
        {
            return false;
        }

        NativeMethods.SetWindowPos(
            _hwnd,
            IntPtr.Zero,
            x,
            y,
            width,
            height,
            SWP_NOZORDER | SWP_NOACTIVATE);

        var alpha = (byte)Math.Clamp(
            (int)Math.Round(opacityPercent * 255.0 / 100.0),
            0,
            255);
        if (!NativeMethods.SetLayeredWindowAttributes(_hwnd, 0, alpha, LWA_ALPHA))
        {
            TheGuideToTheNewEden.Core.Log.Warn(
                $"[预览窗] overlay SetLayeredWindowAttributes 失败：err={Marshal.GetLastWin32Error()} alpha={alpha}");
        }

        if (!_visible)
        {
            NativeMethods.ShowWindow(_hwnd, SW_SHOWNOACTIVATE);
            _visible = true;
        }

        var destination = new NativeMethods.RECT
        {
            Left = 0,
            Top = 0,
            Right = width,
            Bottom = height,
        };
        _thumbnail.TryUpdate(destination, source, 255, true);
        return true;
    }

    public void SetSource(IntPtr source)
    {
        if (_hwnd != IntPtr.Zero)
        {
            _thumbnail.Rebind(_hwnd, source);
        }
    }

    public void Hide()
    {
        if (_hwnd != IntPtr.Zero)
        {
            _thumbnail.Hide();
            NativeMethods.ShowWindow(_hwnd, SW_HIDE);
        }

        _visible = false;
    }

    public void Dispose()
    {
        _thumbnail.Dispose();

        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
        }
    }

    private static void EnsureWindowClass()
    {
        if (_classRegistered)
        {
            return;
        }

        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            style = 0,
            lpfnWndProc = WndProc,
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = GetModuleHandle(null),
            hIcon = IntPtr.Zero,
            hCursor = IntPtr.Zero,
            hbrBackground = IntPtr.Zero,
            lpszMenuName = null,
            lpszClassName = WindowClassName,
            hIconSm = IntPtr.Zero,
        };

        var atom = RegisterClassEx(ref wc);
        if (atom == 0)
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 1410) // ERROR_CLASS_ALREADY_EXISTS
            {
                throw new InvalidOperationException($"注册预览缩略图窗口类失败：Win32Error={error}");
            }
        }

        _classRegistered = true;
    }

    private static IntPtr StaticWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        => DefWindowProc(hWnd, msg, wParam, lParam);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public WndProcDelegate lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle,
        string lpClassName,
        string lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
