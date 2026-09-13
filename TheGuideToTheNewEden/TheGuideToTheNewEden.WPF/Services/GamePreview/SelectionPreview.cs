using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TheGuideToTheNewEden.WPF.Helpers.Interop;

namespace TheGuideToTheNewEden.WPF.Services.GamePreview;

/// <summary>
/// 页面内的"选中进程"实时预览：把游戏窗口用 DWM 缩略图直接合成到主窗口上
/// （<b>不用抓图、不用子窗口</b>——DWM 缩略图的目标窗口可以就是主窗口本身）。
/// <para>
/// 因为缩略图是画在主窗口上的，离开页面时必须注销，否则那块区域会一直留着游戏画面。
/// </para>
/// </summary>
public sealed class SelectionPreview : IDisposable
{
    private readonly DwmThumbnail _thumbnail = new();
    private readonly DispatcherTimer _timer;

    private FrameworkElement? _host;
    private Window? _owner;
    private IntPtr _ownerHwnd;
    private bool _disposed;

    public SelectionPreview()
    {
        // 页面滚动/窗口拖动都会改变宿主位置，用定时器统一刷新，避免挂一堆布局事件
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(150),
        };
        _timer.Tick += (_, _) => Refresh();
    }

    public bool IsAttached => _host is not null && _owner is not null;

    /// <summary>源画面宽高比（宽/高）；未知时为 0。</summary>
    public double SourceAspect { get; private set; }

    /// <summary>当前是否有可显示的画面（已选进程、源窗口仍存在且未最小化、缩略图已建立）。</summary>
    public bool IsSourceAvailable { get; private set; }

    /// <summary>
    /// 源窗口当前是否最小化。最小化时没有实时画面，DWM 会退化成一张被压缩的残留快照，
    /// 所以这种情况同样算"没有画面可显示"（<see cref="IsSourceAvailable"/> 为 false），
    /// 由页面显示占位提示而不是留下残影。
    /// </summary>
    public bool IsSourceMinimized { get; private set; }

    /// <summary>源比例或可用状态变化（页面据此调整预览区高度与空状态提示）。</summary>
    public event Action? StateChanged;

    public void Attach(FrameworkElement host, Window owner)
    {
        _host = host;
        _owner = owner;
        _ownerHwnd = new WindowInteropHelper(owner).Handle;
        _timer.Start();
    }

    public void SetSource(IntPtr sourceHwnd)
    {
        if (_disposed)
        {
            return;
        }

        if (_ownerHwnd == IntPtr.Zero && _owner is not null)
        {
            _ownerHwnd = new WindowInteropHelper(_owner).Handle;
        }

        if (sourceHwnd == IntPtr.Zero || _ownerHwnd == IntPtr.Zero)
        {
            _thumbnail.Unregister();
            UpdateState();
            return;
        }

        if (!_thumbnail.IsRegistered || _thumbnail.SourceWindow != sourceHwnd)
        {
            _thumbnail.TryRegister(_ownerHwnd, sourceHwnd);
        }

        UpdateState();
        Refresh();
    }

    /// <summary>重算"源比例 / 是否可用 / 是否最小化"，变化时通知页面。</summary>
    private void UpdateState()
    {
        var source = _thumbnail.SourceWindow;
        var exists = _thumbnail.IsRegistered
            && source != IntPtr.Zero
            && NativeMethods.IsWindow(source);
        var minimized = exists && NativeMethods.IsIconic(source);

        var available = exists && !minimized;
        var aspect = 0.0;
        if (available
            && PreviewGeometry.TryGetSourceSize(source, out var width, out var height)
            && width > 0
            && height > 0)
        {
            aspect = (double)width / height;
        }
        else
        {
            // 源窗口没了/已最小化/尺寸取不到，就当作没有画面，空状态提示才有机会显示
            available = false;
        }

        if (available == IsSourceAvailable
            && minimized == IsSourceMinimized
            && Math.Abs(aspect - SourceAspect) < 0.0001)
        {
            return;
        }

        IsSourceAvailable = available;
        IsSourceMinimized = minimized;
        SourceAspect = aspect;
        StateChanged?.Invoke();
    }

    public void Refresh()
    {
        UpdateState();

        if (_disposed || _host is null || _owner is null || !_thumbnail.IsRegistered)
        {
            return;
        }

        // 最小化期间不画缩略图：否则留下压缩残影（与浮层预览同一处理）
        if (IsSourceMinimized)
        {
            _thumbnail.Hide();
            return;
        }

        if (!_host.IsVisible || _host.ActualWidth <= 1 || _host.ActualHeight <= 1)
        {
            _thumbnail.Hide();
            return;
        }

        var origin = _host.TransformToAncestor(_owner).Transform(new Point(0, 0));
        var dpi = VisualTreeHelper.GetDpi(_host);
        var left = (int)Math.Round(origin.X * dpi.DpiScaleX);
        var top = (int)Math.Round(origin.Y * dpi.DpiScaleY);
        var right = (int)Math.Round((origin.X + _host.ActualWidth) * dpi.DpiScaleX);
        var bottom = (int)Math.Round((origin.Y + _host.ActualHeight) * dpi.DpiScaleY);

        if (right <= left || bottom <= top)
        {
            return;
        }

        // 与浮层预览一致：按源画面纵横比居中留边，不拉伸
        var container = new NativeMethods.RECT { Left = left, Top = top, Right = right, Bottom = bottom };
        var destination = PreviewGeometry.TryGetSourceSize(_thumbnail.SourceWindow, out var sourceWidth, out var sourceHeight)
            ? PreviewGeometry.FitAspect(container, sourceWidth, sourceHeight)
            : container;

        _thumbnail.TryUpdate(destination, 255, true);
    }

    /// <summary>离开页面时调用：注销缩略图，避免残影。</summary>
    public void Detach()
    {
        _timer.Stop();
        _thumbnail.Unregister();
        _host = null;
        _owner = null;
        UpdateState();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _thumbnail.Dispose();
    }
}
