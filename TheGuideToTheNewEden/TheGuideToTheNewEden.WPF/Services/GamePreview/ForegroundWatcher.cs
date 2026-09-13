using System.Windows.Threading;
using TheGuideToTheNewEden.WPF.Helpers.Interop;

namespace TheGuideToTheNewEden.WPF.Services.GamePreview;

/// <summary>
/// 前台窗口轮询。用 UI 线程的 <see cref="DispatcherTimer"/>（WinUI 版是线程池定时器 + 跨线程派发），
/// 这样订阅方（预览窗口的显示/隐藏/高亮）天然在 UI 线程上执行，不需要再手动 marshal。
/// </summary>
public sealed class ForegroundWatcher : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(120);

    private readonly DispatcherTimer _timer;
    private bool _disposed;

    /// <summary>前台窗口变化（参数为新的前台窗口句柄）。</summary>
    public event Action<IntPtr>? ForegroundChanged;

    public IntPtr Current { get; private set; }

    public bool IsRunning => _timer.IsEnabled;

    public ForegroundWatcher()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = Interval };
        _timer.Tick += OnTick;
    }

    public void Start()
    {
        if (_disposed)
        {
            return;
        }

        // 重新 Start 时必须复位上一次 Stop 留下的状态：
        // WinUI 版把 _isDisposing 永久置 true，Stop 之后再 Start 就再也不上报前台变化了。
        Current = NativeMethods.GetForegroundWindow();
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    private void OnTick(object? sender, EventArgs e)
    {
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground == Current)
        {
            return;
        }

        Current = foreground;
        ForegroundChanged?.Invoke(foreground);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        ForegroundChanged = null;
    }
}
