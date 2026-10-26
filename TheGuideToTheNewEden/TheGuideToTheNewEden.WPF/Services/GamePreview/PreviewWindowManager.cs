using System.Windows;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.WPF.Helpers.Interop;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Services.GamePreview;

/// <summary>
/// 预览窗口的创建、销毁与批量操作（尺寸/布局/显示隐藏/高亮）。
/// <para>
/// 只认 <see cref="ProcessInfo.GUID"/> 与 <see cref="PreviewItem"/>，不关心页面与进程列表，
/// 也不做持久化时机的判断——位置变化通过 <see cref="SettingChanged"/> 上报，由上层决定何时落盘。
/// </para>
/// </summary>
public sealed class PreviewWindowManager : IDisposable
{
    private readonly PreviewSetting _global;
    private readonly GlobalHotkeyService _hotkeys;
    private readonly Dictionary<string, IPreviewWindow> _running = [];
    private readonly Dictionary<int, string> _itemHotkeys = [];
    private bool _disposed;

    /// <summary>运行中的预览数量发生变化。</summary>
    public event Action? RunningChanged;

    /// <summary>某个预览的位置/尺寸变化。</summary>
    public event Action<PreviewItem>? SettingChanged;

    /// <summary>某个快捷键注册失败（参数为组合键文本）。</summary>
    public event Action<string>? HotkeyFailed;

    public PreviewWindowManager(PreviewSetting global, GlobalHotkeyService hotkeys)
    {
        _global = global;
        _hotkeys = hotkeys;
        _hotkeys.Pressed += OnHotkeyPressed;
    }

    public int RunningCount => _running.Count;

    public IReadOnlyCollection<IPreviewWindow> Windows => _running.Values;

    public bool IsRunning(ProcessInfo process) => _running.ContainsKey(process.GUID);

    public IPreviewWindow? Find(ProcessInfo process)
        => _running.TryGetValue(process.GUID, out var window) ? window : null;

    // ---------- 启停 ----------

    /// <summary>开始预览。已在运行则改为换绑设置（不会重复建窗，也不会静默忽略）。</summary>
    public bool Start(ProcessInfo process, PreviewItem setting)
    {
        if (_disposed)
        {
            return false;
        }

        if (_running.TryGetValue(process.GUID, out var existing))
        {
            existing.Rebind(process, setting);
            existing.ApplySettings();
            RegisterItemHotkey(process.GUID, setting);
            return true;
        }

        IPreviewWindow window = setting.ShowPreviewWindow
            ? new GamePreviewWindow(process, setting, _global)
            : new HeadlessPreviewWindow(process, setting, _global);

        window.SettingChanged += OnWindowSettingChanged;
        window.StopRequested += OnWindowStopRequested;

        _running[process.GUID] = window;

        try
        {
            window.Start();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            _running.Remove(process.GUID);
            window.Dispose();
            return false;
        }

        SetupProcess(process, setting);
        RegisterItemHotkey(process.GUID, setting);
        RunningChanged?.Invoke();
        return true;
    }

    public void Stop(ProcessInfo process)
    {
        if (process is null || !_running.Remove(process.GUID, out var window))
        {
            return;
        }

        UnregisterItemHotkey(process.GUID);
        process.Running = false;
        process.Setting = null;
        if (window.Setting is { } setting)
        {
            setting.ProcessInfo = null;
        }

        window.Stop();
        window.Dispose();
        RunningChanged?.Invoke();
    }

    public void StopAll()
    {
        foreach (var process in _running.Keys
                     .Select(guid => _running[guid].Process)
                     .ToList())
        {
            Stop(process);
        }
    }

    /// <summary>角色切换/进程重启后的换绑。</summary>
    public void Rebind(ProcessInfo process, PreviewItem setting)
    {
        if (!_running.TryGetValue(process.GUID, out var window))
        {
            return;
        }

        window.Rebind(process, setting);
        SetupProcess(process, setting);
        RegisterItemHotkey(process.GUID, setting);
    }

    /// <summary>单独刷新某一项的快捷键（用户在设置里改了 HotKey 时调用）。</summary>
    public void RefreshItemHotkey(ProcessInfo process, PreviewItem setting)
        => RegisterItemHotkey(process.GUID, setting);

    private static void SetupProcess(ProcessInfo process, PreviewItem setting)
    {
        setting.ProcessInfo = process;
        process.Setting = setting;
        process.Running = true;
    }

    /// <summary>用户点预览窗口的关闭键 / Esc。</summary>
    private void OnWindowStopRequested(PreviewItem setting)
    {
        if (setting.ProcessInfo is { } process)
        {
            Stop(process);
        }
    }

    private void OnWindowSettingChanged(PreviewItem setting) => SettingChanged?.Invoke(setting);

    // ---------- 前台联动 ----------

    /// <summary>前台窗口变化：把对应预览隐藏或高亮，其余恢复。</summary>
    public void ApplyForeground(IntPtr foreground)
    {
        foreach (var window in _running.Values)
        {
            if (window.Process.MainWindowHandle == foreground)
            {
                // 真实前台状态：角色名底色的高亮用它，与"边框高亮"开关无关
                window.SetForegroundState(true);

                if (window.Setting.HideOnForeground)
                {
                    window.HideWindow();
                }
                else
                {
                    window.ShowWindow();
                    window.SetHighlight(window.Setting.Highlight);
                }

                window.Recover();
            }
            else
            {
                window.SetForegroundState(false);
                window.SetHighlight(false);
                window.ShowWindow();
            }
        }
    }

    // ---------- 批量操作 ----------

    /// <summary>
    /// 把全部可见预览设成同一尺寸。因为窗口尺寸被锁定为"游戏客户区比例"，
    /// 这里给出的是<b>画面区域</b>的宽高：高度按第一个窗口的源画面比例算出后统一使用，
    /// 让所有窗口得到完全相同的结果（各自按自己的比例算会得到不同高度）。
    /// </summary>
    public bool ApplyUniformSize(int width, int height)
    {
        var windows = VisibleWindows().ToList();
        if (windows.Count == 0)
        {
            return false;
        }

        // 比例未知（源窗口最小化/已关闭）时用 0 表示"别动高度"，而不是拿一个坏比例去压扁窗口
        var aspect = windows[0].SourceAspect;
        if (aspect > 0)
        {
            height = Math.Max(1, (int)Math.Round(width / aspect));
        }

        foreach (var window in windows)
        {
            window.SetSize(width, height);
            Persist(window);
        }

        return true;
    }

    /// <summary>以某个客户端为锚点自动排列其余预览窗口。</summary>
    public bool ApplyAutoLayout(ProcessInfo? anchor, int mode, int align, int span, int count)
    {
        var visible = VisibleWindows().ToList();
        if (visible.Count < 2)
        {
            return false;
        }

        var anchorWindow = (anchor is null ? null : Find(anchor));
        if (anchorWindow is null || !anchorWindow.Setting.ShowPreviewWindow)
        {
            anchorWindow = visible[0];
        }

        var others = visible.Where(w => !ReferenceEquals(w, anchorWindow)).ToList();
        if (others.Count == 0)
        {
            return false;
        }

        var anchorRect = ToRect(anchorWindow.GetRect());
        if (anchorRect.Width <= 0 || anchorRect.Height <= 0)
        {
            return false;
        }

        if (!NativeMethods.TryGetMonitorWorkArea(anchorWindow.Process.MainWindowHandle, out var workArea))
        {
            return false;
        }

        var sizes = others
            .Select(w => ToRect(w.GetRect()))
            .Select(r => new PreviewLayoutCalculator.Size(r.Width, r.Height))
            .ToList();

        var positions = PreviewLayoutCalculator.Compute(anchorRect, sizes, workArea, mode, align, span, count);
        for (var i = 0; i < others.Count; i++)
        {
            others[i].SetPosition(positions[i].X, positions[i].Y);
            Persist(others[i]);
        }

        SettingChanged?.Invoke(anchorWindow.Setting);
        return true;
    }

    /// <summary>把一项的显示相关设置复制到其余所有已保存配置，并让运行中的预览立即生效。</summary>
    public void ApplyToAll(PreviewItem source)
    {
        foreach (var item in _global.PreviewItems.Where(i => !ReferenceEquals(i, source)).ToList())
        {
            item.OverlapOpacity = source.OverlapOpacity;
            item.HideOnForeground = source.HideOnForeground;
            item.Highlight = source.Highlight;
            item.HighlightColor = source.HighlightColor;
            item.TitleHighlightColor = source.TitleHighlightColor;
            item.TitleNormalColor = source.TitleNormalColor;
            item.HighlightMarginLeft = source.HighlightMarginLeft;
            item.HighlightMarginTop = source.HighlightMarginTop;
            item.HighlightMarginRight = source.HighlightMarginRight;
            item.HighlightMarginBottom = source.HighlightMarginBottom;
            item.RespondGlobalHotKey = source.RespondGlobalHotKey;
            item.ShowPreviewWindow = source.ShowPreviewWindow;
            item.ShowPreviewWindowMode = source.ShowPreviewWindowMode;

            if (item.ProcessInfo is not { } process)
            {
                continue;
            }

            var window = Find(process);
            if (window is null)
            {
                continue;
            }

            // 显示方式（有窗/无窗）变了必须重建窗口，否则改了设置看起来没生效
            var wasWindow = window is GamePreviewWindow;
            if (wasWindow != item.ShowPreviewWindow)
            {
                Stop(process);
                Start(process, item);
            }
            else
            {
                window.ApplySettings();
            }
        }
    }

    /// <summary>
    /// 恢复到该项保存的位置与尺寸。存档里的尺寸是<b>窗口</b>尺寸（可能来自旧版本、比例与游戏不符），
    /// 这里只把它当作"想要的宽度"，高度交给窗口按源画面比例重算，避免恢复出一个带黑边的比例。
    /// </summary>
    public void RestorePosition(ProcessInfo process, PreviewItem setting)
    {
        var window = Find(process);
        if (window is null)
        {
            return;
        }

        var width = setting.WinW > 0 ? setting.WinW : 533;
        var height = setting.WinH > 0 ? setting.WinH : 300;

        // 比例未知（源窗口最小化/已关闭）时保留存档高度，不要拿坏比例去压扁
        var aspect = window.SourceAspect;
        if (aspect > 0)
        {
            height = Math.Max(1, (int)Math.Round(width / aspect));
        }

        window.SetSize(width, height);
        window.SetPosition(setting.WinX, setting.WinY);
        Persist(window);
    }

    // ---------- 快捷键 ----------

    private void RegisterItemHotkey(string guid, PreviewItem setting)
    {
        UnregisterItemHotkey(guid);
        if (string.IsNullOrWhiteSpace(setting.HotKey))
        {
            return;
        }

        if (_hotkeys.TryRegister(setting.HotKey, out var id) && id >= 0)
        {
            _itemHotkeys[id] = guid;
        }
        else
        {
            HotkeyFailed?.Invoke(setting.HotKey);
        }
    }

    private void UnregisterItemHotkey(string guid)
    {
        foreach (var pair in _itemHotkeys.Where(p => p.Value == guid).ToList())
        {
            _hotkeys.Unregister(pair.Key);
            _itemHotkeys.Remove(pair.Key);
        }
    }

    private void OnHotkeyPressed(int id)
    {
        if (_itemHotkeys.TryGetValue(id, out var guid) && _running.TryGetValue(guid, out var window))
        {
            window.ActivateSource();
        }
    }

    // ---------- 工具 ----------

    private IEnumerable<IPreviewWindow> VisibleWindows()
        => _running.Values.Where(w => w.Setting.ShowPreviewWindow && w.IsShowing);

    private void Persist(IPreviewWindow window)
    {
        if (!window.Setting.ShowPreviewWindow)
        {
            return;
        }

        var rect = window.GetRect();
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        window.Setting.WinX = rect.X;
        window.Setting.WinY = rect.Y;
        window.Setting.WinW = rect.Width;
        window.Setting.WinH = rect.Height;
        SettingChanged?.Invoke(window.Setting);
    }

    private static NativeMethods.RECT ToRect(Int32Rect rect)
        => new() { Left = rect.X, Top = rect.Y, Right = rect.X + rect.Width, Bottom = rect.Y + rect.Height };

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _hotkeys.Pressed -= OnHotkeyPressed;
        StopAll();
        _itemHotkeys.Clear();
        SettingChanged = null;
        RunningChanged = null;
        HotkeyFailed = null;
    }
}
