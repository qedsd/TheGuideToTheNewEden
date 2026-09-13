using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.WPF.Services.GamePreview;

/// <summary>
/// 全局快捷键（<c>RegisterHotKey</c> + <c>WM_HOTKEY</c>）。
/// <para>
/// 每个注册返回一个 id，<see cref="Pressed"/> 回传该 id，由调用方决定它代表哪个动作。
/// WinUI 版借 Vanara 的 <c>SetWindowSubclass</c> 挂消息处理，并且**子类化失败时会在
/// RegisterHotKey 已成功的情况下返回 false**（热键已注册却报失败）；这里用 WPF 原生的
/// <see cref="HwndSource.AddHook"/>，注册是否成功就是真正的结果。
/// </para>
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;

    private const int ModAlt = 0x0001;
    private const int ModControl = 0x0002;
    private const int ModShift = 0x0004;
    private const int ModWin = 0x0008;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private static readonly Dictionary<string, int> KeyCodes = BuildKeyCodes();

    private readonly IntPtr _hwnd;
    private readonly HwndSource? _source;
    private readonly HashSet<int> _registeredIds = [];
    private int _nextId;
    private bool _disposed;

    /// <summary>按下已注册的全局快捷键；参数为注册时返回的 id。</summary>
    public event Action<int>? Pressed;

    public GlobalHotkeyService(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _source = hwnd == IntPtr.Zero ? null : HwndSource.FromHwnd(hwnd);
        _source?.AddHook(WndProc);
    }

    public bool IsAvailable => _source is not null && _hwnd != IntPtr.Zero;

    /// <summary>注册组合键（如 <c>Ctrl+1</c>、<c>F5</c>）；空字符串视为"未设置"，返回 true 且 id 为 -1。</summary>
    public bool TryRegister(string? combination, out int registerId)
    {
        registerId = -1;
        if (string.IsNullOrWhiteSpace(combination))
        {
            return true;
        }

        if (!TryParse(combination, out var modifiers, out var virtualKey))
        {
            return false;
        }

        return TryRegister(modifiers, virtualKey, out registerId);
    }

    public bool TryRegister(int modifiers, int virtualKey, out int registerId)
    {
        registerId = -1;
        if (_disposed || !IsAvailable)
        {
            return false;
        }

        var id = ++_nextId;
        if (!RegisterHotKey(_hwnd, id, modifiers, virtualKey))
        {
            return false;
        }

        _registeredIds.Add(id);
        registerId = id;
        return true;
    }

    public void Unregister(int registerId)
    {
        if (registerId < 0 || !_registeredIds.Remove(registerId))
        {
            return;
        }

        if (_hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, registerId);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey)
        {
            Pressed?.Invoke(wParam.ToInt32());
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var id in _registeredIds.ToList())
        {
            if (_hwnd != IntPtr.Zero)
            {
                UnregisterHotKey(_hwnd, id);
            }
        }

        _registeredIds.Clear();
        _source?.RemoveHook(WndProc);
        Pressed = null;
    }

    /// <summary>解析 <c>Ctrl+Alt+A</c> 形式的组合键。</summary>
    public static bool TryParse(string? text, out int modifiers, out int virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var hasKey = false;
        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= ModControl;
                    continue;
                case "alt":
                    modifiers |= ModAlt;
                    continue;
                case "shift":
                    modifiers |= ModShift;
                    continue;
                case "win":
                case "windows":
                    modifiers |= ModWin;
                    continue;
            }

            if (hasKey || !KeyCodes.TryGetValue(raw.ToLowerInvariant(), out var code))
            {
                return false;
            }

            // 鼠标键不在 RegisterHotKey 的能力范围内
            if (code is >= 1 and <= 6)
            {
                return false;
            }

            virtualKey = code;
            hasKey = true;
        }

        return hasKey;
    }

    private static Dictionary<string, int> BuildKeyCodes()
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // 与设置页展示的按键表保持一致（Resources/Configs/Keyboardlist.csv）
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Configs", "Keyboardlist.csv");
            if (File.Exists(path))
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    var item = KeyboardItem.FromCsv(line);
                    if (item is not null && !string.IsNullOrWhiteSpace(item.Name) && item.Code > 0)
                    {
                        map[item.Name.Trim().ToLowerInvariant()] = item.Code;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        // 兜底：即使按键表缺失，字母/数字/功能键仍可用
        for (var c = 'A'; c <= 'Z'; c++)
        {
            map.TryAdd(c.ToString().ToLowerInvariant(), c);
        }

        for (var c = '0'; c <= '9'; c++)
        {
            map.TryAdd(c.ToString(), c);
        }

        for (var i = 1; i <= 24; i++)
        {
            map.TryAdd($"f{i}", 0x6F + i);
        }

        map.TryAdd("space", 0x20);
        map.TryAdd("enter", 0x0D);
        map.TryAdd("return", 0x0D);
        map.TryAdd("esc", 0x1B);
        map.TryAdd("escape", 0x1B);
        map.TryAdd("tab", 0x09);
        map.TryAdd("backspace", 0x08);
        map.TryAdd("delete", 0x2E);
        map.TryAdd("insert", 0x2D);
        map.TryAdd("home", 0x24);
        map.TryAdd("end", 0x23);
        map.TryAdd("pageup", 0x21);
        map.TryAdd("pagedown", 0x22);
        map.TryAdd("up", 0x26);
        map.TryAdd("down", 0x28);
        map.TryAdd("left", 0x25);
        map.TryAdd("right", 0x27);
        return map;
    }
}
