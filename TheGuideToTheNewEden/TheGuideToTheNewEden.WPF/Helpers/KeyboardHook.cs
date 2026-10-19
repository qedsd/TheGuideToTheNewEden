using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>一次按键事件里的按键信息（虚拟键码 + 扫描码 + 名称）。</summary>
public sealed class KeyboardKeyInfo
{
    public KeyboardKeyInfo(int virtualKey, int scanCode, string name)
    {
        VirtualKey = virtualKey;
        ScanCode = scanCode;
        Name = name;
    }

    /// <summary>虚拟键码（<c>VK_*</c>）。</summary>
    public int VirtualKey { get; }

    /// <summary>硬件扫描码。</summary>
    public int ScanCode { get; }

    /// <summary>按键名称（来自 <c>GetKeyNameText</c>，随系统语言，例如 F1 / A / Space）。</summary>
    public string Name { get; }
}

/// <summary>
/// 全局低级键盘钩子（<c>WH_KEYBOARD_LL</c>），移植自 WinUI 版的 <c>Common/KeyboardHook</c>。
/// <para>
/// 与 WinUI 版的差异（都是实测能踩到的点）：
/// ① **扩展键要带扩展位**取名字——方向键 / 小键盘 / 右 Ctrl 的扫描码相同，必须把
/// <c>lParam</c> 的第 24 位（extended）一起给 <c>GetKeyNameText</c>，否则名字会解析成另一个键；
/// ② **Sys 键也要算按下**（<c>WM_SYSKEYDOWN</c>/<c>WM_SYSKEYUP</c>）——WinUI 只处理了 0x100/0x101，
/// 于是 Alt 组合键完全不计数；
/// ③ 模块句柄用 <c>GetModuleHandle(null)</c>，不再走 <c>Assembly.GetModules()[0]</c>（.NET Core 下不可靠）；
/// ④ 事件分成"某个键按下"（供计数）与"当前按住的键集合"（原版的语义），并做成线程安全的只读快照。
/// </para>
/// <para>
/// <b>必须在有消息泵的线程上安装</b>（本项目即 UI 线程）：低级键盘钩子的回调由安装线程的消息循环派发，
/// 在无消息泵的线程上安装会收不到任何事件。回调因此也在 UI 线程上执行，订阅方不必再切线程。
/// </para>
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;

    private readonly Dictionary<int, KeyboardKeyInfo> _pressed = new();

    private IntPtr _hookHandle = IntPtr.Zero;
    private HookProc? _hookProc; // 必须持有引用，否则会被 GC 回收（回调时进程直接崩）

    /// <summary>
    /// 为 true 时吞掉所有键盘消息（事件仍会派发）。录制快捷键时用：
    /// 避免 Win/Alt+Tab/Esc 等触发系统或窗口动作，捕获完立即恢复。
    /// </summary>
    public bool SuppressAll { get; set; }

    /// <summary>某个键被按下（含系统键）。</summary>
    public event Action<KeyboardKeyInfo>? KeyPressed;

    /// <summary>当前按住的键集合发生变化（快照，只读）。</summary>
    public event Action<IReadOnlyList<KeyboardKeyInfo>>? PressedKeysChanged;

    public bool IsRunning => _hookHandle != IntPtr.Zero;

    /// <summary>安装钩子。已安装时返回 true（幂等）。</summary>
    public bool Start()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return true;
        }

        try
        {
            _hookProc = HookCallback;
            _hookHandle = SetWindowsHookEx(WhKeyboardLl, _hookProc, GetModuleHandle(null), 0);
            if (_hookHandle == IntPtr.Zero)
            {
                Core.Log.Error($"[KeyboardHook] SetWindowsHookEx 失败：{Marshal.GetLastWin32Error()}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return false;
        }
    }

    /// <summary>卸载钩子。未安装时也是安全操作。</summary>
    public bool Stop()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return true;
        }

        try
        {
            if (!UnhookWindowsHookEx(_hookHandle))
            {
                Core.Log.Warn($"[KeyboardHook] UnhookWindowsHookEx 失败：{Marshal.GetLastWin32Error()}");
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
        finally
        {
            _hookHandle = IntPtr.Zero;
            _hookProc = null;
            SuppressAll = false;
            Clear();
        }

        return true;
    }

    /// <summary>清空"当前按住的键"（例如切换页面、窗口失焦后残留）。</summary>
    public void Clear()
    {
        lock (_pressed)
        {
            _pressed.Clear();
        }
    }

    /// <summary>查询某个虚拟键当前是否被钩子记为按住（不受 SuppressAll 影响）。</summary>
    public bool IsPressed(int virtualKey)
    {
        lock (_pressed)
        {
            return _pressed.ContainsKey(virtualKey);
        }
    }

    public void Dispose() => Stop();

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // 已卸载就不再派发：卸载与"系统已排队的回调"之间存在竞态，
        // 不挡一下的话 Stop() 之后仍可能多计一次按键（探针里手动调用回调时也能观察到）。
        if (nCode == 0 && _hookHandle != IntPtr.Zero)
        {
            var message = wParam.ToInt32();
            var isKeyMessage = message is WmKeyDown or WmKeyUp or WmSysKeyDown or WmSysKeyUp;
            // 先记下本次消息是否要吞：事件处理器可能在回调里 Stop 录制并把 SuppressAll 复位，
            // 但触发捕获的那一下按键仍应被吞掉。
            var suppress = SuppressAll;

            try
            {
                if (message is WmKeyDown or WmSysKeyDown)
                {
                    HandleKeyDown(lParam);
                }
                else if (message is WmKeyUp or WmSysKeyUp)
                {
                    HandleKeyUp(lParam);
                }
            }
            catch (Exception ex)
            {
                // 钩子回调里绝不能把异常抛回系统。
                Core.Log.Error(ex);
            }

            // 录制模式：事件已经派发出去，但不再把按键交给系统/当前窗口。
            if (suppress && isKeyMessage)
            {
                return new IntPtr(1);
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void HandleKeyDown(IntPtr lParam)
    {
        var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
        var virtualKey = (int)data.vkCode;

        lock (_pressed)
        {
            if (_pressed.ContainsKey(virtualKey))
            {
                return;
            }

            var info = new KeyboardKeyInfo(virtualKey, (int)data.scanCode, ResolveKeyName(data));
            _pressed[virtualKey] = info;
        }

        KeyPressed?.Invoke(new KeyboardKeyInfo(virtualKey, (int)data.scanCode, ResolveKeyName(data)));
        NotifyPressedChanged();
    }

    private void HandleKeyUp(IntPtr lParam)
    {
        var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
        var virtualKey = (int)data.vkCode;
        bool removed;

        lock (_pressed)
        {
            removed = _pressed.Remove(virtualKey);
        }

        if (removed)
        {
            NotifyPressedChanged();
        }
    }

    private void NotifyPressedChanged()
    {
        KeyboardKeyInfo[] snapshot;
        lock (_pressed)
        {
            snapshot = new KeyboardKeyInfo[_pressed.Count];
            _pressed.Values.CopyTo(snapshot, 0);
        }

        PressedKeysChanged?.Invoke(snapshot);
    }

    /// <summary>
    /// 取按键名称。<c>GetKeyNameText</c> 的 lParam 布局：bit16-23 = 扫描码，bit24 = 扩展键。
    /// （WinUI 版只给了扫描码，所以方向键/小键盘会取到错误的名字。）
    /// </summary>
    private static string ResolveKeyName(KbdLlHookStruct data)
    {
        try
        {
            var extended = (data.flags & 0x01) != 0; // LLKHF_EXTENDED
            var lParam = ((int)data.scanCode & 0xFF) << 16;
            if (extended)
            {
                lParam |= 1 << 24;
            }

            var buffer = new StringBuilder(64);
            if (GetKeyNameText(lParam, buffer, buffer.Capacity) > 0)
            {
                return buffer.ToString().Trim(' ', '\0');
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        return string.Empty;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetKeyNameText(int lParam, StringBuilder lpString, int nSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
