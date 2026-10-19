using TheGuideToTheNewEden.WPF.Helpers;

namespace TheGuideToTheNewEden.WPF.Services.Tools;

/// <summary>
/// 全局键盘钩子的共享入口（对应 WinUI 版 <c>Services/KeyboardService</c>）：引用计数式地启停钩子，
/// 只有第一个使用者安装、最后一个使用者卸载。
/// <para>
/// 与 WinUI 版的差异：① 事件改成实例事件 + 转发（原版是静态事件，多个页面订阅后会互相干扰）；
/// ② <c>Stop()</c> 计数减到 0 时才真正卸载，计数不会变成负数；③ <c>Clear()</c> 空安全
/// （原版 <c>Current._keyboardHook.Clear()</c> 在钩子还没建时直接 NRE）。
/// </para>
/// </summary>
public sealed class KeyboardHookService : IDisposable
{
    private static KeyboardHookService? _current;

    private readonly KeyboardHook _hook = new();
    private readonly object _locker = new();
    private int _refCount;

    /// <summary>某个键被按下（UI 线程回调）。</summary>
    public event Action<KeyboardKeyInfo>? KeyPressed;

    public static KeyboardHookService Current => _current ??= new KeyboardHookService();

    public bool IsRunning => _hook.IsRunning;

    /// <summary>录制快捷键时临时吞掉系统按键（事件仍派发）；结束录制必须复位。</summary>
    public bool SuppressAll
    {
        get => _hook.SuppressAll;
        set => _hook.SuppressAll = value;
    }

    /// <summary>开始监听（引用计数 +1；第一个调用者安装钩子）。</summary>
    public void Start()
    {
        lock (_locker)
        {
            _refCount++;
            if (_refCount > 1)
            {
                return;
            }

            _hook.KeyPressed += OnHookKeyPressed;
            if (!_hook.Start())
            {
                _hook.KeyPressed -= OnHookKeyPressed;
                _hook.SuppressAll = false;
                _refCount = 0;
            }
        }
    }

    /// <summary>停止监听（引用计数 -1；减到 0 才卸载钩子）。</summary>
    public void Stop()
    {
        lock (_locker)
        {
            if (_refCount > 0)
            {
                _refCount--;
            }

            if (_refCount > 0)
            {
                return;
            }

            _hook.KeyPressed -= OnHookKeyPressed;
            _hook.SuppressAll = false;
            _hook.Stop();
        }
    }

    /// <summary>清空"当前按住的键"。</summary>
    public void Clear() => _hook.Clear();

    /// <summary>查询某个虚拟键当前是否被钩子记为按住（录制修饰键用）。</summary>
    public bool IsPressed(int virtualKey) => _hook.IsPressed(virtualKey);

    public void Dispose()
    {
        lock (_locker)
        {
            _refCount = 0;
            _hook.KeyPressed -= OnHookKeyPressed;
            _hook.SuppressAll = false;
            _hook.Dispose();
        }
    }

    private void OnHookKeyPressed(KeyboardKeyInfo key) => KeyPressed?.Invoke(key);
}
