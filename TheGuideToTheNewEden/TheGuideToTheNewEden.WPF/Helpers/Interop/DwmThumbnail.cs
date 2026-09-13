using System.Runtime.InteropServices;

namespace TheGuideToTheNewEden.WPF.Helpers.Interop;

/// <summary>
/// DWM 缩略图：把"源窗口（EVE 客户端）"的画面实时合成到"目标窗口（预览窗口）"的指定矩形里。
/// <para>
/// 关键取舍：只使用 <c>DWM_TNP_SOURCECLIENTAREAONLY</c>，由系统裁掉源窗口的非客户区，
/// <b>不再自己算标题栏高度/边框宽度</b>（WinUI 版按 DIP 与物理两种单位手工裁剪，正是
/// "不同 DPI 下画面错位"的成因）。缩略图是 GPU 合成，无抓图开销，也不需要 IPC。
/// </para>
/// </summary>
internal sealed class DwmThumbnail : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DWM_THUMBNAIL_PROPERTIES
    {
        public int dwFlags;
        public NativeMethods.RECT rcDestination;
        public NativeMethods.RECT rcSource;
        public byte opacity;

        [MarshalAs(UnmanagedType.Bool)]
        public bool fVisible;

        [MarshalAs(UnmanagedType.Bool)]
        public bool fSourceClientAreaOnly;
    }

    private const int DWM_TNP_RECTDESTINATION = 0x00000001;
    private const int DWM_TNP_OPACITY = 0x00000004;
    private const int DWM_TNP_VISIBLE = 0x00000008;
    private const int DWM_TNP_SOURCECLIENTAREAONLY = 0x00000010;

    [DllImport("dwmapi.dll")]
    private static extern int DwmRegisterThumbnail(IntPtr destWindow, IntPtr sourceWindow, out IntPtr thumbnail);

    [DllImport("dwmapi.dll")]
    private static extern int DwmUnregisterThumbnail(IntPtr thumbnail);

    [DllImport("dwmapi.dll")]
    private static extern int DwmQueryThumbnailSourceSize(IntPtr thumbnail, out SIZE size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmUpdateThumbnailProperties(IntPtr thumbnail, ref DWM_THUMBNAIL_PROPERTIES properties);

    private IntPtr _thumbnail;

    public IntPtr SourceWindow { get; private set; }

    public bool IsRegistered => _thumbnail != IntPtr.Zero;

    /// <summary>把 <paramref name="sourceWindow"/> 合成到 <paramref name="destinationWindow"/>；重复调用会先注销旧的。</summary>
    public bool TryRegister(IntPtr destinationWindow, IntPtr sourceWindow)
    {
        Unregister();

        if (destinationWindow == IntPtr.Zero || sourceWindow == IntPtr.Zero)
        {
            return false;
        }

        if (DwmRegisterThumbnail(destinationWindow, sourceWindow, out var thumbnail) != 0 || thumbnail == IntPtr.Zero)
        {
            _thumbnail = IntPtr.Zero;
            return false;
        }

        _thumbnail = thumbnail;
        SourceWindow = sourceWindow;
        return true;
    }

    /// <summary>更新目标矩形（目标窗口客户区物理像素）、不透明度与可见性。</summary>
    public bool TryUpdate(NativeMethods.RECT destination, byte opacity, bool visible)
    {
        if (_thumbnail == IntPtr.Zero)
        {
            return false;
        }

        var properties = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = DWM_TNP_RECTDESTINATION | DWM_TNP_OPACITY | DWM_TNP_VISIBLE | DWM_TNP_SOURCECLIENTAREAONLY,
            rcDestination = destination,
            opacity = opacity,
            fVisible = visible,
            fSourceClientAreaOnly = true,
        };

        return DwmUpdateThumbnailProperties(_thumbnail, ref properties) == 0;
    }

    public void Hide() => TryUpdate(default, 0, false);

    public bool TryGetSourceSize(out int width, out int height)
    {
        width = 0;
        height = 0;
        if (_thumbnail == IntPtr.Zero)
        {
            return false;
        }

        if (DwmQueryThumbnailSourceSize(_thumbnail, out var size) != 0 || size.cx <= 0 || size.cy <= 0)
        {
            return false;
        }

        width = size.cx;
        height = size.cy;
        return true;
    }

    /// <summary>源窗口重启/句柄变化后重新绑定。</summary>
    public bool Rebind(IntPtr destinationWindow, IntPtr sourceWindow)
    {
        if (sourceWindow == SourceWindow && _thumbnail != IntPtr.Zero)
        {
            return true;
        }

        SourceWindow = IntPtr.Zero;
        return TryRegister(destinationWindow, sourceWindow);
    }

    public void Unregister()
    {
        if (_thumbnail != IntPtr.Zero)
        {
            DwmUnregisterThumbnail(_thumbnail);
            _thumbnail = IntPtr.Zero;
        }

        SourceWindow = IntPtr.Zero;
    }

    public void Dispose() => Unregister();
}
