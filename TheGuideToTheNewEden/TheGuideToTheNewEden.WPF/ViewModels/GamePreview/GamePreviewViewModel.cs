using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.GamePreview;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.ViewModels.GamePreview;

/// <summary>
/// 多开页面的视图模型。
/// <para>
/// 职责划分：进程列表/排序/分组与快捷键分发在这里；窗口的创建与批量操作在
/// <see cref="PreviewWindowManager"/>；进程发现与激活在 <see cref="GameClientService"/>；
/// 前台轮询在 <see cref="ForegroundWatcher"/>。WinUI 版把这些全塞在一个 1600 行的 VM 里，
/// 并因此出现了"刷新与保存互相重入""排序改了不落盘""切换角色后配置越积越多"等问题。
/// </para>
/// </summary>
public sealed class GamePreviewViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly PreviewSetting _setting;
    private readonly PreviewWindowManager _manager;
    private readonly GlobalHotkeyService _hotkeys;
    private readonly ForegroundWatcher _foreground = new();
    private readonly System.Windows.Threading.DispatcherTimer _processMonitor;
    private readonly Dictionary<PreviewHotKeyGroup, (int Forward, int Backward)> _groupHotkeys = [];

    private bool _refreshing;
    private bool _disposed;
    private ProcessInfo? _selectedProcess;
    private bool _isGlobalSetting;
    private int _forwardHotkeyId = -1;
    private int _backwardHotkeyId = -1;
    private string _lastActiveGuid = string.Empty;
    private PreviewHotKeyGroup? _lastActiveGroup;
    private string _orderText = string.Empty;

    public GamePreviewViewModel()
    {
        _setting = GamePreviewSettingService.Current.Value;
        NormalizeSettings();

        _hotkeys = new GlobalHotkeyService(GetMainWindowHandle());
        _manager = new PreviewWindowManager(_setting, _hotkeys);
        _manager.RunningChanged += OnRunningChanged;
        _manager.SettingChanged += _ => GamePreviewSettingService.Current.ScheduleSave();
        _manager.HotkeyFailed += OnHotkeyFailed;
        _hotkeys.Pressed += OnHotkeyPressed;
        _foreground.ForegroundChanged += OnForegroundChanged;

        _processMonitor = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _processMonitor.Tick += async (_, _) => await RefreshAsync();
    }

    // ---------- 绑定属性 ----------

    public PreviewSetting Setting => _setting;

    public ObservableCollection<ProcessInfo> Processes { get; } = [];

    public ProcessInfo? SelectedProcess
    {
        get => _selectedProcess;
        set
        {
            if (Set(ref _selectedProcess, value))
            {
                OnPropertyChanged(nameof(SelectedSetting));
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(SelectedRunning));

                // 选中进程 → 切回"选中项设置"（否则用户点了进程却看不到它的设置）；
                // 取消选中 → 回到"全局设置"（此时"选中项设置"页签已隐藏，不能停在空页上）
                IsGlobalSetting = value is null;
                OnPropertyChanged(nameof(SettingsTabIndex));
            }
        }
    }

    public PreviewItem? SelectedSetting => _selectedProcess?.Setting;

    public bool HasSelection => _selectedProcess is not null;

    /// <summary>
    /// 设置区当前应选中的页签索引：0 = 选中项设置、1 = 全局设置。
    /// <para>
    /// 刻意由 VM 直接给出、并且**没选中进程时恒为 1**：WPF 的 <c>TabControl</c> 允许选中一个
    /// <c>Visibility=Collapsed</c> 的 <c>TabItem</c>（实测确实是这么干的），
    /// 若启动时 <c>SelectedIndex=0</c> 而该项已隐藏，就会停在隐藏项上、全局设置点不到。
    /// </para>
    /// </summary>
    public int SettingsTabIndex
    {
        get => !HasSelection || IsGlobalSetting ? 1 : 0;

        // 用户点页签时回写：未选中进程时"选中项设置"是隐藏的，不会走到 index 0
        set => IsGlobalSetting = value > 0;
    }

    /// <summary>
    /// "角色名叠加"字体下拉框的数据源：系统已安装字体名（按名称排序）。
    /// 字体集合与具体设置项无关，故放在 VM 上、只枚举一次。
    /// </summary>
    public IReadOnlyList<string> FontFamilies { get; } = Fonts.SystemFontFamilies
        .Select(f => f.Source)
        .Where(n => !string.IsNullOrWhiteSpace(n))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

    public bool SelectedRunning => _selectedProcess?.Running ?? false;

    /// <summary>右侧显示全局设置（true）还是选中项设置（false）。</summary>
    public bool IsGlobalSetting
    {
        get => _isGlobalSetting;
        set
        {
            if (Set(ref _isGlobalSetting, value))
            {
                OnPropertyChanged(nameof(SettingsTabIndex));
            }
        }
    }

    public bool Running => _manager.RunningCount > 0;

    /// <summary>顺序编辑框内容（每行一个窗口标题）。</summary>
    public string OrderText
    {
        get => _orderText;
        set => Set(ref _orderText, value);
    }

    public string ProcessKeywords
    {
        get => _setting.ProcessKeywords;
        set
        {
            if (_setting.ProcessKeywords == value)
            {
                return;
            }

            _setting.ProcessKeywords = value;
            OnPropertyChanged();
            Save();
            _ = RefreshAsync();
        }
    }

    public string ForwardHotkey
    {
        get => _setting.SwitchHotkey_Forward;
        set
        {
            if (_setting.SwitchHotkey_Forward == value)
            {
                return;
            }

            _setting.SwitchHotkey_Forward = value;
            OnPropertyChanged();
            UpdateHotkeyRegistration();
            Save();
        }
    }

    public string BackwardHotkey
    {
        get => _setting.SwitchHotkey_Backward;
        set
        {
            if (_setting.SwitchHotkey_Backward == value)
            {
                return;
            }

            _setting.SwitchHotkey_Backward = value;
            OnPropertyChanged();
            UpdateHotkeyRegistration();
            Save();
        }
    }

    public ObservableCollection<PreviewHotKeyGroup> HotKeyGroups => _setting.HotKeyGroups;

    // ---------- 初始化 ----------

    /// <summary>页面首次显示时调用。</summary>
    public async Task InitializeAsync()
    {
        await RefreshAsync();
        _foreground.Start();
        _processMonitor.Start();
    }

    private void NormalizeSettings()
    {
        // 历史配置里的 2 是已废弃的 IPC 无标题栏模式，并入普通窗口样式
        foreach (var item in _setting.PreviewItems)
        {
            if (item.ShowPreviewWindowMode is < 0 or > 1)
            {
                item.ShowPreviewWindowMode = 1;
            }
        }

        _setting.SetForegroundWindowMode = Math.Clamp(
            _setting.SetForegroundWindowMode, 0, GameClientService.MaxActivationMode);
        _setting.AutoLayout = Math.Clamp(_setting.AutoLayout, 0, 3);
        _setting.AutoLayoutAnchor = Math.Clamp(_setting.AutoLayoutAnchor, 0, 2);
    }

    private static IntPtr GetMainWindowHandle()
    {
        var window = Application.Current?.MainWindow;
        return window is null ? IntPtr.Zero : new WindowInteropHelper(window).EnsureHandle();
    }

    // ---------- 进程列表 ----------

    /// <summary>刷新进程列表；同一窗口（句柄相同）复用原有条目与配置。</summary>
    public async Task RefreshAsync()
    {
        if (_refreshing || _disposed)
        {
            return;
        }

        // 定时器是 1s 一次，扫描 + 进程遍历可能超时；重入会让列表与配置互相踩踏
        _refreshing = true;
        try
        {
            var keywords = _setting.ProcessKeywords;
            var found = await Task.Run(() => GameClientService.FindClients(keywords));

            var byHandle = new Dictionary<IntPtr, ProcessInfo>();
            foreach (var process in found)
            {
                byHandle.TryAdd(process.MainWindowHandle, process);
            }

            foreach (var existing in Processes.ToList())
            {
                if (byHandle.Remove(existing.MainWindowHandle, out var fresh))
                {
                    var renamed = existing.WindowTitle != fresh.WindowTitle;
                    var hadCharacter = !string.IsNullOrEmpty(existing.GetCharacterName());
                    var oldProcess = existing.Process;
                    existing.WindowTitle = fresh.WindowTitle;
                    existing.ProcessName = fresh.ProcessName;
                    existing.Process = fresh.Process;
                    DisposeProcess(oldProcess);
                    if (renamed)
                    {
                        OnCharacterSwitched(existing);

                        // "自动开始新出现的进程"原来只覆盖**新发现**的进程：EVE 刚启动时标题只有 "EVE"、
                        // 解析不出角色名，于是不会被自动开始；等选中角色后标题才变成 "EVE - 角色名"，
                        // 这时进程早就在列表里了（不属于"新发现"），就再也没有人开始它（用户反馈）。
                        // 这里补一次"刚解析出角色名"的自动开始；角色**切换**（改名前已有角色名）不在此列——
                        // 那种情况由"同进程切换角色后沿用设置"和 `OnCharacterSwitched` 负责。
                        if (!hadCharacter
                            && !existing.Running
                            && _setting.AutoStartNewProcess
                            && !string.IsNullOrEmpty(existing.GetCharacterName()))
                        {
                            StartProcess(existing);
                        }
                    }
                }
                else
                {
                    _manager.Stop(existing);
                    Processes.Remove(existing);
                    existing.Setting = null;
                    DisposeProcess(existing.Process);
                }
            }

            var orderChanged = false;
            foreach (var fresh in byHandle.Values)
            {
                orderChanged |= InsertByOrder(fresh);
                if (_setting.AutoStartNewProcess && !string.IsNullOrEmpty(fresh.GetCharacterName()))
                {
                    StartProcess(fresh);
                }
            }

            OnPropertyChanged(nameof(Running));

            // 只有顺序真的变了才落盘：定时器每秒都会刷新，不能每轮都写文件
            if (orderChanged)
            {
                Save();
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    private static void DisposeProcess(System.Diagnostics.Process? process)
    {
        try
        {
            process?.Dispose();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private bool InsertByOrder(ProcessInfo process)
    {
        var order = _setting.ProcessOrder;
        if (!order.Contains(process.WindowTitle))
        {
            order.Add(process.WindowTitle);
            Processes.Add(process);
            return true;
        }

        var target = order.IndexOf(process.WindowTitle);
        for (var i = 0; i < Processes.Count; i++)
        {
            var index = order.IndexOf(Processes[i].WindowTitle);
            if (index < 0 || index > target)
            {
                Processes.Insert(i, process);
                return false;
            }
        }

        Processes.Add(process);
        return false;
    }

    /// <summary>上移/下移选中项；顺序立即持久化（WinUI 版的 Move 不会触发保存）。</summary>
    public void MoveSelected(int delta)
    {
        if (_selectedProcess is null)
        {
            return;
        }

        var index = Processes.IndexOf(_selectedProcess);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= Processes.Count)
        {
            return;
        }

        Processes.Move(index, target);
        SyncOrderFromProcesses();
    }

    private void SyncOrderFromProcesses()
    {
        _setting.ProcessOrder = Processes.Select(p => p.WindowTitle).ToList();
        OrderText = string.Join(Environment.NewLine, _setting.ProcessOrder);
        Save();
    }

    /// <summary>把顺序编辑框内容写回（每行一个标题，忽略空行）。</summary>
    public void ApplyOrderText()
    {
        var titles = OrderText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToList();

        if (titles.Count == 0)
        {
            return;
        }

        _setting.ProcessOrder = titles;
        ReorderProcessesBySetting();
        Save();
    }

    /// <summary>用当前进程列表填充顺序编辑框。</summary>
    public void FillOrderFromProcesses()
    {
        OrderText = string.Join(Environment.NewLine, Processes.Select(p => p.WindowTitle));
    }

    private void ReorderProcessesBySetting()
    {
        var sorted = Processes
            .OrderBy(OrderIndexOf)
            .ThenBy(p => Processes.IndexOf(p))
            .ToList();

        for (var i = 0; i < sorted.Count; i++)
        {
            var current = Processes.IndexOf(sorted[i]);
            if (current != i)
            {
                Processes.Move(current, i);
            }
        }
    }

    private int OrderIndexOf(ProcessInfo process)
    {
        var index = _setting.ProcessOrder.IndexOf(process.WindowTitle);
        return index >= 0 ? index : int.MaxValue;
    }

    // ---------- 配置解析 ----------

    private PreviewItem ResolveSetting(ProcessInfo process)
    {
        if (process.Setting is not null)
        {
            return process.Setting;
        }

        var name = process.GetCharacterName();
        if (!string.IsNullOrEmpty(name))
        {
            var saved = _setting.PreviewItems.FirstOrDefault(p => p.Name == name && p.ProcessInfo is null);
            return saved ?? new PreviewItem { Name = name };
        }

        return new PreviewItem();
    }

    /// <summary>角色切换（同一进程换了角色）：换绑设置，并把原配置解绑。</summary>
    private void OnCharacterSwitched(ProcessInfo process)
    {
        // 只有正在预览的进程才需要换绑；未运行时的标题变化等下一次刷新自行处理即可
        if (!process.Running || process.Setting is not { } previous)
        {
            return;
        }

        if (!_setting.SwitchCharacterKeepSetting)
        {
            _manager.Stop(process);
            previous.ProcessInfo = null;
            return;
        }

        // 先解绑：否则这条配置的 ProcessInfo 一直非空，之后再也匹配不到，配置会越积越多
        previous.ProcessInfo = null;

        var name = process.GetCharacterName();
        PreviewItem next;
        if (string.IsNullOrEmpty(name))
        {
            // 回到选择角色的界面：保留外观设置，标记为未识别名称（不参与持久化匹配）
            if (!previous.Name.StartsWith('*'))
            {
                previous.Name = $"*Untitled[{previous.Name}]";
            }

            next = previous;
        }
        else
        {
            var saved = _setting.PreviewItems.FirstOrDefault(p => p.Name == name && p.ProcessInfo is null);
            next = saved ?? CopyVisualSettings(previous, name);
            if (saved is null && !_setting.PreviewItems.Contains(next))
            {
                _setting.PreviewItems.Add(next);
            }
        }

        _manager.Rebind(process, next);
        OnPropertyChanged(nameof(SelectedSetting));
        Save();
    }

    private static PreviewItem CopyVisualSettings(PreviewItem source, string name) => new()
    {
        Name = name,
        OverlapOpacity = source.OverlapOpacity,
        WinX = source.WinX,
        WinY = source.WinY,
        WinW = source.WinW,
        WinH = source.WinH,
        HideOnForeground = source.HideOnForeground,
        Highlight = source.Highlight,
        HighlightColor = source.HighlightColor,
        TitleHighlightColor = source.TitleHighlightColor,
        TitleNormalColor = source.TitleNormalColor,
        HighlightMarginLeft = source.HighlightMarginLeft,
        HighlightMarginTop = source.HighlightMarginTop,
        HighlightMarginRight = source.HighlightMarginRight,
        HighlightMarginBottom = source.HighlightMarginBottom,
        RespondGlobalHotKey = source.RespondGlobalHotKey,
        ShowPreviewWindow = source.ShowPreviewWindow,
        ShowPreviewWindowMode = source.ShowPreviewWindowMode,
        HotKey = source.HotKey,
    };

    // ---------- 启停 ----------

    public bool StartProcess(ProcessInfo process)
    {
        var setting = ResolveSetting(process);
        var started = _manager.Start(process, setting);
        if (!started)
        {
            PageNotifyService.Error(FindString("GamePreviewPage_StartFailed"));
            return false;
        }

        var name = setting.Name;
        if (!string.IsNullOrEmpty(name) && !name.StartsWith('*') && !_setting.PreviewItems.Contains(setting))
        {
            _setting.PreviewItems.Add(setting);
        }
        else if (string.IsNullOrEmpty(name))
        {
            PageNotifyService.Info(FindString("GamePreviewPage_EmptyName"));
        }

        OnPropertyChanged(nameof(SelectedSetting));
        OnPropertyChanged(nameof(SelectedRunning));
        Save();
        return true;
    }

    public void StartSelected()
    {
        if (_selectedProcess is not null)
        {
            StartProcess(_selectedProcess);
        }
    }

    public void StopSelected()
    {
        if (_selectedProcess is not { } process)
        {
            return;
        }

        _manager.Stop(process);

        // 停止后重新挂上"该角色的已保存配置"：否则设置面板会变成空白，
        // 用户会以为设置丢了（配置其实还在）。
        process.Setting = ResolveSetting(process);
        OnPropertyChanged(nameof(SelectedSetting));
        OnPropertyChanged(nameof(SelectedRunning));
        Save();
    }

    public void StartAll()
    {
        var started = 0;
        foreach (var process in Processes.ToList())
        {
            if (process.Running)
            {
                continue;
            }

            // StartAllWithNoneSetting=false 时跳过没有已保存配置的进程
            var name = process.GetCharacterName();
            var hasSaved = !string.IsNullOrEmpty(name)
                && _setting.PreviewItems.Any(p => p.Name == name && p.ProcessInfo is null);

            if (!_setting.StartAllWithNoneSetting && !hasSaved)
            {
                continue;
            }

            StartProcess(process);
            started++;
        }

        if (started == 0)
        {
            PageNotifyService.Info(FindString("GamePreviewPage_NoStartableProcess"));
        }
    }

    public void StopAll()
    {
        _manager.StopAll();
        foreach (var process in Processes)
        {
            process.Running = false;
            process.Setting = ResolveSetting(process);
        }

        OnPropertyChanged(nameof(SelectedSetting));
        OnPropertyChanged(nameof(SelectedRunning));
        Save();
    }

    // ---------- 批量操作 ----------

    public void ApplyUniformSize()
    {
        if (_manager.ApplyUniformSize(_setting.UniformWidth, _setting.UniformHeight))
        {
            PageNotifyService.Success(FindString("GamePreviewPage_UniformSizeApplied"));
            Save();
        }
        else
        {
            PageNotifyService.Info(FindString("GamePreviewPage_NoRunning"));
        }
    }

    public void ApplyAutoLayout()
    {
        if (_manager.ApplyAutoLayout(_selectedProcess, _setting.AutoLayout, _setting.AutoLayoutAnchor, _setting.AutoLayoutSpan, _setting.AutoLayoutCount))
        {
            PageNotifyService.Success(FindString("GamePreviewPage_AutoLayoutApplied"));
            Save();
        }
        else
        {
            PageNotifyService.Info(FindString("GamePreviewPage_NeedTwoWindows"));
        }
    }

    public void ApplySelectedToAll()
    {
        if (_selectedProcess?.Setting is not { } source)
        {
            return;
        }

        _manager.ApplyToAll(source);
        PageNotifyService.Success(FindString("GamePreviewPage_ApplyToAllDone"));
        Save();
    }

    public void RestorePosition()
    {
        if (_selectedProcess is not { Setting: { } setting } process)
        {
            return;
        }

        _manager.RestorePosition(process, setting);
        Save();
    }

    /// <summary>设置项改动后调用：立即套用到正在预览的窗口并保存。</summary>
    public void NotifySettingChanged(bool refreshHotkey = false)
    {
        if (_selectedProcess is { } process && process.Setting is { } setting)
        {
            var window = _manager.Find(process);
            window?.ApplySettings();
            if (refreshHotkey)
            {
                _manager.RefreshItemHotkey(process, setting);
            }
        }

        Save();
    }

    // ---------- 分组 ----------

    public void AddGroup()
    {
        var group = new PreviewHotKeyGroup { GroupName = $"{FindString("GamePreviewPage_GroupHotkey")}{_setting.HotKeyGroups.Count + 1}" };
        _setting.HotKeyGroups.Add(group);
        UpdateHotkeyRegistration();
        Save();
    }

    public void RemoveGroup(PreviewHotKeyGroup group)
    {
        if (_groupHotkeys.Remove(group, out var ids))
        {
            _hotkeys.Unregister(ids.Forward);
            _hotkeys.Unregister(ids.Backward);
        }

        _setting.HotKeyGroups.Remove(group);
        if (ReferenceEquals(_lastActiveGroup, group))
        {
            _lastActiveGroup = null;
        }

        Save();
    }

    /// <summary>分组的热键/角色名单改动后调用。</summary>
    public void UpdateGroup(PreviewHotKeyGroup group)
    {
        UpdateHotkeyRegistration();
        Save();
    }

    // ---------- 快捷键 ----------

    private bool _hotkeysRegistered;

    private void UpdateHotkeyRegistration()
    {
        if (_disposed)
        {
            return;
        }

        if (!Running)
        {
            UnregisterHotkeys();
            return;
        }

        UnregisterHotkeys();

        if (!_hotkeys.TryRegister(_setting.SwitchHotkey_Forward, out _forwardHotkeyId))
        {
            OnHotkeyFailed(_setting.SwitchHotkey_Forward);
        }

        if (!_hotkeys.TryRegister(_setting.SwitchHotkey_Backward, out _backwardHotkeyId))
        {
            OnHotkeyFailed(_setting.SwitchHotkey_Backward);
        }

        foreach (var group in _setting.HotKeyGroups)
        {
            var okForward = _hotkeys.TryRegister(group.SwitchHotkey_Forward, out var forward);
            if (!okForward && !string.IsNullOrWhiteSpace(group.SwitchHotkey_Forward))
            {
                OnHotkeyFailed(group.SwitchHotkey_Forward);
            }

            var okBackward = _hotkeys.TryRegister(group.SwitchHotkey_Backward, out var backward);
            if (!okBackward && !string.IsNullOrWhiteSpace(group.SwitchHotkey_Backward))
            {
                OnHotkeyFailed(group.SwitchHotkey_Backward);
            }

            _groupHotkeys[group] = (forward, backward);
        }

        _hotkeysRegistered = true;
    }

    private void UnregisterHotkeys()
    {
        if (!_hotkeysRegistered)
        {
            return;
        }

        _hotkeysRegistered = false;
        _hotkeys.Unregister(_forwardHotkeyId);
        _hotkeys.Unregister(_backwardHotkeyId);
        _forwardHotkeyId = -1;
        _backwardHotkeyId = -1;

        foreach (var ids in _groupHotkeys.Values)
        {
            _hotkeys.Unregister(ids.Forward);
            _hotkeys.Unregister(ids.Backward);
        }

        _groupHotkeys.Clear();
    }

    private void OnHotkeyFailed(string hotkey)
        => PageNotifyService.Error(string.Format(FindString("GamePreviewPage_HotkeyFailed"), hotkey));

    private void OnHotkeyPressed(int id)
    {
        if (id == _forwardHotkeyId)
        {
            SwitchProcess(true);
            return;
        }

        if (id == _backwardHotkeyId)
        {
            SwitchProcess(false);
            return;
        }

        foreach (var pair in _groupHotkeys)
        {
            if (pair.Value.Forward == id)
            {
                SwitchGroup(pair.Key, true);
                return;
            }

            if (pair.Value.Backward == id)
            {
                SwitchGroup(pair.Key, false);
                return;
            }
        }
    }

    private List<ProcessInfo> GetSwitchTargets(Func<ProcessInfo, bool>? filter = null)
        => Processes
            .Where(p => p.Running && p.Setting is { RespondGlobalHotKey: true })
            .Where(p => filter is null || filter(p))
            .OrderBy(OrderIndexOf)
            .ThenBy(p => Processes.IndexOf(p))
            .ToList();

    private void SwitchProcess(bool forward)
    {
        var targets = GetSwitchTargets();
        if (targets.Count == 0)
        {
            PageNotifyService.Info(FindString("GamePreviewPage_NoRunning"));
            return;
        }

        _lastActiveGroup = null;

        var current = targets.FindIndex(p => p.GUID == _lastActiveGuid);
        var next = current < 0
            ? (forward ? 0 : targets.Count - 1)
            : (((current + (forward ? 1 : -1)) % targets.Count) + targets.Count) % targets.Count;

        Activate(targets[next]);
    }

    private void SwitchGroup(PreviewHotKeyGroup group, bool forward)
    {
        var targets = GetSwitchTargets(p =>
        {
            var name = p.GetCharacterName();
            return !string.IsNullOrEmpty(name) && group.GameNames.Contains(name);
        });

        if (targets.Count == 0)
        {
            PageNotifyService.Info(FindString("GamePreviewPage_NoGroupMember"));
            return;
        }

        var restart = (_lastActiveGroup != group && group.Restart)
            || string.IsNullOrEmpty(group.LastActiveProcessGUID)
            || targets.All(p => p.GUID != group.LastActiveProcessGUID);

        int index;
        if (restart)
        {
            index = 0;
        }
        else
        {
            var current = targets.FindIndex(p => p.GUID == group.LastActiveProcessGUID);
            index = current < 0
                ? 0
                : (((current + (forward ? 1 : -1)) % targets.Count) + targets.Count) % targets.Count;
        }

        Activate(targets[index]);
        group.LastActiveProcessGUID = targets[index].GUID;
        _lastActiveGroup = group;
    }

    private void Activate(ProcessInfo process)
    {
        _manager.Find(process)?.ActivateSource();
        _lastActiveGuid = process.GUID;
    }

    // ---------- 前台联动 ----------

    private void OnForegroundChanged(IntPtr hWnd)
    {
        _manager.ApplyForeground(hWnd);

        // 用"用户当前看着的那个客户端"作为下一次切换的起点
        var process = Processes.FirstOrDefault(p => p.MainWindowHandle == hWnd);
        if (process is { Running: true } && process.Setting?.RespondGlobalHotKey == true)
        {
            _lastActiveGuid = process.GUID;
            _lastActiveGroup = null;
        }
    }

    private void OnRunningChanged()
    {
        // 预览可能由"预览窗口自己的关闭键"结束（不经过本 VM 的停止方法），
        // 这里统一把该角色的已保存配置重新挂上，避免设置面板变成空白。
        foreach (var process in Processes)
        {
            if (!process.Running && process.Setting is null)
            {
                process.Setting = ResolveSetting(process);
            }
        }

        UpdateHotkeyRegistration();
        OnPropertyChanged(nameof(Running));
        OnPropertyChanged(nameof(SelectedRunning));
        OnPropertyChanged(nameof(SelectedSetting));
        GamePreviewSettingService.Current.ScheduleSave();
    }

    // ---------- 其他 ----------

    public void Save() => GamePreviewSettingService.Current.ScheduleSave();

    private static string FindString(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _processMonitor.Stop();
        UnregisterHotkeys();

        _foreground.ForegroundChanged -= OnForegroundChanged;
        _foreground.Dispose();

        _hotkeys.Pressed -= OnHotkeyPressed;
        _manager.RunningChanged -= OnRunningChanged;
        _manager.Dispose();
        _hotkeys.Dispose();

        foreach (var process in Processes)
        {
            DisposeProcess(process.Process);
        }

        GamePreviewSettingService.Current.Save();
        PropertyChanged = null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
