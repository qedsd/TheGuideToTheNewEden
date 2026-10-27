using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
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
    private int _settingsTabIndex = 1;
    private PreviewHotKeyGroup? _selectedHotKeyGroup;
    private string _groupFilter = string.Empty;
    private int _forwardHotkeyId = -1;
    private int _backwardHotkeyId = -1;
    private string _lastActiveGuid = string.Empty;
    private PreviewHotKeyGroup? _lastActiveGroup;
    private string _clientFilter = string.Empty;
    private bool _isPreviewVisible = true;

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

        // 顶部客户端条的"运行计数胶囊"与空态提示都靠这两个集合/属性变化驱动
        Processes.CollectionChanged += OnProcessesChanged;

        // 客户端列表的搜索过滤：只影响视图，不动 Processes 本身（排序/启停/快捷键仍按完整列表走）
        ProcessesView = CollectionViewSource.GetDefaultView(Processes);
        ProcessesView.Filter = FilterProcess;
        // 视图的增删在 Processes.CollectionChanged 之后才落定，用视图自己的事件补一次"可见数量"
        ProcessesView.CollectionChanged += (_, _) => NotifyVisibleProcessesChanged();

        // 分组快捷键：独立页签里的"分组列表 + 编辑区"。列表支持搜索/上下移，编辑区只显示当前分组。
        HotKeyGroupsView = CollectionViewSource.GetDefaultView(HotKeyGroups);
        HotKeyGroupsView.Filter = FilterHotKeyGroup;
        HotKeyGroupsView.CollectionChanged += (_, _) => NotifyHotKeyGroupsViewChanged();
        HotKeyGroups.CollectionChanged += OnHotKeyGroupsChanged;
        SelectedHotKeyGroup = HotKeyGroups.FirstOrDefault();
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
                // 取消选中 → 若正停在已隐藏的"选中项设置"上则回到全局设置，停在分组页签时保持不变
                if (value is null)
                {
                    if (_settingsTabIndex == 0)
                    {
                        SettingsTabIndex = 1;
                    }
                }
                else
                {
                    SettingsTabIndex = 0;
                }

                // 无论索引是否变化都通知一次：HasSelection 变化会改变 getter 的归一化结果
                OnPropertyChanged(nameof(SettingsTabIndex));
            }
        }
    }

    public PreviewItem? SelectedSetting => _selectedProcess?.Setting;

    public bool HasSelection => _selectedProcess is not null;

    /// <summary>
    /// 设置区当前应选中的页签索引：0 = 选中项设置、1 = 全局设置、2 = 分组设置、3 = 切换顺序。
    /// <para>
    /// 刻意由 VM 直接给出、并且**没选中进程时 0 归一化为 1**：WPF 的 <c>TabControl</c> 允许选中一个
    /// <c>Visibility=Collapsed</c> 的 <c>TabItem</c>（实测确实是这么干的），
    /// 若启动时 <c>SelectedIndex=0</c> 而该项已隐藏，就会停在隐藏项上、全局设置点不到。
    /// 分组/顺序页签（2/3）在无选中进程时仍然可选。
    /// </para>
    /// </summary>
    public int SettingsTabIndex
    {
        get => !HasSelection && _settingsTabIndex == 0 ? 1 : _settingsTabIndex;

        // 用户点页签时回写；0 只在有选中进程时有效，其余情况由 getter 归一化
        set => Set(ref _settingsTabIndex, Math.Clamp(value, 0, 3));
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

    public bool Running => _manager.RunningCount > 0;

    /// <summary>是否发现了客户端（客户端列表的空态与列表二选一）。</summary>
    public bool HasProcesses => Processes.Count > 0;

    public int ProcessCount => Processes.Count;

    public int RunningProcessCount => Processes.Count(p => p.Running);

    /// <summary>还没解析出角色名的客户端数（登录界面 / 选人界面）。它们不能成为分组成员，但要让用户知道原因。</summary>
    public int UnnamedClientCount => Processes.Count(p => string.IsNullOrEmpty(p.GetCharacterName()));

    public bool HasUnnamedClients => UnnamedClientCount > 0;

    public string UnnamedClientHint
        => string.Format(FindString("GamePreviewPage_GroupHotkey_UnnamedClients"), UnnamedClientCount);

    /// <summary>角色名重复的组数（两个客户端同名时，分组候选按名字去重，只会出现一个胶囊）。</summary>
    public int DuplicateCharacterNameCount => Processes
        .Select(p => p.GetCharacterName())
        .Where(n => !string.IsNullOrEmpty(n))
        .GroupBy(n => n!, StringComparer.OrdinalIgnoreCase)
        .Count(g => g.Count() > 1);

    public bool HasDuplicateCharacterNames => DuplicateCharacterNameCount > 0;

    public string DuplicateCharacterNameHint
        => string.Format(FindString("GamePreviewPage_GroupHotkey_DuplicateNames"), DuplicateCharacterNameCount);

    /// <summary>左侧计数胶囊的文字（运行数 / 总数）。语言中立，语义靠 ToolTip 的「预览中」。</summary>
    public string ProcessCountText => $"{RunningProcessCount} / {ProcessCount}";

    /// <summary>左侧客户端列表的过滤视图（搜索只影响显示，不影响排序与启停）。</summary>
    public ICollectionView ProcessesView { get; }

    /// <summary>客户端搜索框内容（匹配角色名或窗口标题，忽略大小写）。</summary>
    public string ClientFilter
    {
        get => _clientFilter;
        set
        {
            if (!Set(ref _clientFilter, value ?? string.Empty))
            {
                return;
            }

            // 过滤刷新可能让 ListBox 把"仍在视图里"的选中项也丢掉，这里补回来；
            // 被过滤掉的选中项则不强行保留（用户正在找别的客户端）。
            var selected = _selectedProcess;
            ProcessesView.Refresh();
            NotifyVisibleProcessesChanged();

            if (selected is not null
                && ProcessesView.Contains(selected)
                && !ReferenceEquals(SelectedProcess, selected))
            {
                SelectedProcess = selected;
            }
        }
    }

    /// <summary>过滤后仍可见的客户端数。</summary>
    public int VisibleProcessCount => ProcessesView.Cast<object>().Count();

    public bool HasVisibleProcesses => VisibleProcessCount > 0;

    /// <summary>有客户端、但都被搜索条件挡掉——用于显示"没有匹配的客户端"而不是"未发现客户端"。</summary>
    public bool HasNoMatch => HasProcesses && VisibleProcessCount == 0;

    /// <summary>
    /// 页面内的实时画面侧栏是否展开（阶段 135：画面不再是主区域，收起后设置区拿回全部宽度）。
    /// 默认在宽视口展开、窄视口收起（首次 <c>Loaded</c> 时由页面按视口宽度决定）。
    /// </summary>
    public bool IsPreviewVisible
    {
        get => _isPreviewVisible;
        set => Set(ref _isPreviewVisible, value);
    }

    /// <summary>
    /// 分组快捷键可点选的角色（当前发现的客户端 + 已保存配置 + 各分组里手工加过的名字，去重后按发现顺序）。
    /// 只作展示与点选，真正的成员关系仍存在 <see cref="PreviewHotKeyGroup.GameNames"/>。
    /// </summary>
    public ObservableCollection<CharacterCandidate> CharacterCandidates { get; } = [];

    /// <summary>
    /// "切换顺序"页签的列表：按 <see cref="PreviewSetting.ProcessOrder"/> 的顺序展示（含已关闭的旧标题），
    /// 新发现但不在顺序里的客户端追加在末尾。
    /// </summary>
    public ObservableCollection<SwitchOrderEntry> SwitchOrderEntries { get; } = [];

    public bool HasSwitchOrderEntries => SwitchOrderEntries.Count > 0;

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

    /// <summary>分组列表的过滤视图（按分组名搜索；只影响显示，不改 HotKeyGroups 本身）。</summary>
    public ICollectionView HotKeyGroupsView { get; }

    /// <summary>分组搜索框内容（当前按分组名匹配，忽略大小写）。</summary>
    public string GroupFilter
    {
        get => _groupFilter;
        set
        {
            if (!Set(ref _groupFilter, value ?? string.Empty))
            {
                return;
            }

            var selected = _selectedHotKeyGroup;
            HotKeyGroupsView.Refresh();
            NotifyHotKeyGroupsViewChanged();

            // 刷新视图可能让 ListBox 把仍在视图里的选中项也丢掉，这里补回来；
            // 选中项被过滤掉（或本来没选）时自动落到第一个可见分组。
            if (selected is not null && HotKeyGroupsView.Contains(selected))
            {
                if (!ReferenceEquals(SelectedHotKeyGroup, selected))
                {
                    SelectedHotKeyGroup = selected;
                }
            }
            else
            {
                SelectedHotKeyGroup = HotKeyGroupsView.Cast<PreviewHotKeyGroup>().FirstOrDefault();
            }
        }
    }

    /// <summary>分组列表里当前选中的分组。</summary>
    public PreviewHotKeyGroup? SelectedHotKeyGroup
    {
        get => _selectedHotKeyGroup;
        set
        {
            if (Set(ref _selectedHotKeyGroup, value))
            {
                OnPropertyChanged(nameof(HasSelectedHotKeyGroup));
            }
        }
    }

    public bool HasSelectedHotKeyGroup => _selectedHotKeyGroup is not null;

    public bool HasHotKeyGroups => HotKeyGroups.Count > 0;

    public int VisibleHotKeyGroupCount => HotKeyGroupsView.Cast<object>().Count();

    /// <summary>有分组、但都被搜索条件挡掉。</summary>
    public bool HasGroupNoMatch => HasHotKeyGroups && VisibleHotKeyGroupCount == 0;

    /// <summary>当前分组的"单独排序"列表（角色名顺序）。</summary>
    public ObservableCollection<GroupOrderEntry> GroupOrderEntries { get; } = [];

    public bool HasGroupOrderEntries => GroupOrderEntries.Count > 0;

    private bool FilterHotKeyGroup(object item)
    {
        if (item is not PreviewHotKeyGroup group)
        {
            return false;
        }

        var query = _groupFilter.Trim();
        return query.Length == 0
            || (!string.IsNullOrEmpty(group.GroupName)
                && group.GroupName.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private void NotifyHotKeyGroupsViewChanged()
    {
        OnPropertyChanged(nameof(VisibleHotKeyGroupCount));
        OnPropertyChanged(nameof(HasGroupNoMatch));
    }

    private void OnHotKeyGroupsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasHotKeyGroups));
        NotifyHotKeyGroupsViewChanged();

        // 分组被移除后如果仍指着已不存在的对象，清掉选中（RemoveGroup 会主动选邻居，这里是兜底）
        if (_selectedHotKeyGroup is not null && !HotKeyGroups.Contains(_selectedHotKeyGroup))
        {
            SelectedHotKeyGroup = null;
        }
    }

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
        _setting.SnapDistance = Math.Clamp(_setting.SnapDistance <= 0 ? 12 : _setting.SnapDistance, 1, 64);
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

            var orderChanged = false;
            foreach (var existing in Processes.ToList())
            {
                if (byHandle.Remove(existing.MainWindowHandle, out var fresh))
                {
                    var oldTitle = existing.WindowTitle;
                    var renamed = oldTitle != fresh.WindowTitle;
                    var hadCharacter = !string.IsNullOrEmpty(existing.GetCharacterName());
                    var oldProcess = existing.Process;
                    existing.WindowTitle = fresh.WindowTitle;
                    existing.ProcessName = fresh.ProcessName;
                    existing.Process = fresh.Process;
                    DisposeProcess(oldProcess);
                    if (renamed)
                    {
                        // 切换顺序存的是窗口标题：角色切换后把旧标题原位替换成新标题，
                        // 否则顺序页签里会留下一条"未发现"的旧标题、新标题被追加到末尾。
                        orderChanged |= ReplaceOrderTitle(oldTitle, fresh.WindowTitle);
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

            foreach (var fresh in byHandle.Values)
            {
                orderChanged |= InsertByOrder(fresh);
                if (_setting.AutoStartNewProcess && !string.IsNullOrEmpty(fresh.GetCharacterName()))
                {
                    StartProcess(fresh);
                }
            }

            OnPropertyChanged(nameof(Running));

            // 窗口标题变成"EVE - 角色名"、以及预览启动后新落盘的配置，都会让候选角色变化
            RefreshCharacterCandidates();

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

    /// <summary>
    /// 列表增删/移动时维护"运行计数"与空态：只在加入时订阅、移出时退订，
    /// 逐项的 <see cref="ProcessInfo.Running"/> 变化才会反映到计数胶囊上。
    /// </summary>
    private void OnProcessesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ProcessInfo process in e.OldItems)
            {
                process.PropertyChanged -= OnProcessPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (ProcessInfo process in e.NewItems)
            {
                process.PropertyChanged += OnProcessPropertyChanged;
            }
        }

        OnPropertyChanged(nameof(HasProcesses));
        RaiseProcessCounts();
        RefreshCharacterCandidates();
    }

    private void OnProcessPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 标题变化 = 新客户端选好角色 / 切换角色：候选角色必须立刻刷新，
        // 不能只等 1 秒定时器的下一轮（分组设置里看不到新客户端通常就是这一步慢了）。
        if (e.PropertyName is nameof(ProcessInfo.Running) or nameof(ProcessInfo.WindowTitle))
        {
            RaiseProcessCounts();
            RefreshCharacterCandidates();
        }
    }

    private void RaiseProcessCounts()
    {
        OnPropertyChanged(nameof(ProcessCount));
        OnPropertyChanged(nameof(RunningProcessCount));
        OnPropertyChanged(nameof(ProcessCountText));
        OnPropertyChanged(nameof(UnnamedClientCount));
        OnPropertyChanged(nameof(HasUnnamedClients));
        OnPropertyChanged(nameof(UnnamedClientHint));
        OnPropertyChanged(nameof(DuplicateCharacterNameCount));
        OnPropertyChanged(nameof(HasDuplicateCharacterNames));
        OnPropertyChanged(nameof(DuplicateCharacterNameHint));
        NotifyVisibleProcessesChanged();
    }

    /// <summary>过滤视图的"可见数量 / 是否无匹配"跟着集合与搜索词变化。</summary>
    private void NotifyVisibleProcessesChanged()
    {
        OnPropertyChanged(nameof(VisibleProcessCount));
        OnPropertyChanged(nameof(HasVisibleProcesses));
        OnPropertyChanged(nameof(HasNoMatch));
    }

    private bool FilterProcess(object item)
    {
        if (item is not ProcessInfo process)
        {
            return false;
        }

        var query = _clientFilter.Trim();
        if (query.Length == 0)
        {
            return true;
        }

        return Contains(process.GetCharacterName(), query)
            || Contains(process.WindowTitle, query);

        static bool Contains(string? text, string query)
            => !string.IsNullOrEmpty(text)
                && text.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- 分组快捷键的候选角色 ----------

    /// <summary>
    /// 重建 <see cref="CharacterCandidates"/>：发现中的角色（按列表顺序）→ 已保存配置 / 其他分组里手工加过的名字（按字母）。
    /// 内容（名字 + 是否预览中）没有变化时直接返回——定时刷新每秒都会调用，不能每次都重建列表（会打断点选）。
    /// </summary>
    private void RefreshCharacterCandidates()
    {
        var ordered = new List<CharacterCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            var trimmed = name.Trim();
            if (seen.Add(trimmed))
            {
                ordered.Add(new CharacterCandidate(trimmed, IsCharacterPreviewing(trimmed)));
            }
        }

        foreach (var process in Processes)
        {
            Add(process.GetCharacterName());
        }

        // 未登录 / 已保存配置里的角色，以及旧配置里手工写进分组的名字，按名称排序补在后面
        var extras = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
        foreach (var item in _setting.PreviewItems)
        {
            if (!string.IsNullOrWhiteSpace(item.Name) && !item.Name.StartsWith('*'))
            {
                extras.Add(item.Name);
            }
        }

        foreach (var group in _setting.HotKeyGroups)
        {
            foreach (var name in group.GameNames)
            {
                extras.Add(name);
            }
        }

        foreach (var name in extras)
        {
            Add(name);
        }

        if (CharacterCandidates.Count == ordered.Count
            && CharacterCandidates.Zip(ordered).All(pair => pair.First == pair.Second))
        {
            return;
        }

        CharacterCandidates.Clear();
        foreach (var candidate in ordered)
        {
            CharacterCandidates.Add(candidate);
        }
    }

    private bool IsCharacterPreviewing(string name)
        => Processes.Any(p => p.Running
            && string.Equals(p.GetCharacterName(), name, StringComparison.OrdinalIgnoreCase));

    /// <summary>手工添加一个候选角色（不在列表里的角色名也能进分组），已经存在则忽略。</summary>
    public void AddCharacterCandidate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var trimmed = name.Trim();
        if (CharacterCandidates.Any(c => string.Equals(c.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        CharacterCandidates.Add(new CharacterCandidate(trimmed, IsCharacterPreviewing(trimmed)));
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
        RebuildSwitchOrder();
        Save();
    }

    // ---------- 切换顺序（独立页签） ----------

    /// <summary>
    /// 用"保存的顺序 + 当前进程"重建列表：
    /// 先按 <see cref="PreviewSetting.ProcessOrder"/> 逐个列出（保留已关闭客户端的旧标题），
    /// 再追加尚未写入顺序的进程。重复标题只保留一次。
    /// </summary>
    public void RebuildSwitchOrder()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<SwitchOrderEntry>();

        foreach (var title in _setting.ProcessOrder)
        {
            if (string.IsNullOrWhiteSpace(title) || !seen.Add(title))
            {
                continue;
            }

            entries.Add(new SwitchOrderEntry(title, Processes.FirstOrDefault(p => p.WindowTitle == title)));
        }

        foreach (var process in Processes)
        {
            if (seen.Add(process.WindowTitle))
            {
                entries.Add(new SwitchOrderEntry(process.WindowTitle, process));
            }
        }

        SwitchOrderEntries.Clear();
        foreach (var entry in entries)
        {
            SwitchOrderEntries.Add(entry);
        }

        RenumberSwitchOrderEntries();
        OnPropertyChanged(nameof(HasSwitchOrderEntries));
    }

    /// <summary>上移/下移一个顺序项（立即写回 ProcessOrder、重排进程列表并保存）。</summary>
    public void MoveSwitchOrderEntry(SwitchOrderEntry entry, int delta)
    {
        var index = SwitchOrderEntries.IndexOf(entry);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= SwitchOrderEntries.Count)
        {
            return;
        }

        SwitchOrderEntries.Move(index, target);
        RenumberSwitchOrderEntries();
        CommitSwitchOrder();
    }

    /// <summary>按角色名排序（已关闭的旧标题排在最后），立即生效。</summary>
    public void SortSwitchOrderByName()
    {
        var sorted = SwitchOrderEntries
            .OrderBy(e => e.IsPresent ? 0 : 1)
            .ThenBy(e => e.CharacterName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        for (var i = 0; i < sorted.Count; i++)
        {
            var current = SwitchOrderEntries.IndexOf(sorted[i]);
            if (current != i)
            {
                SwitchOrderEntries.Move(current, i);
            }
        }

        RenumberSwitchOrderEntries();
        CommitSwitchOrder();
    }

    /// <summary>用当前进程列表重建顺序（丢弃已关闭的旧标题），立即生效。</summary>
    public void FillSwitchOrderFromProcesses()
    {
        _setting.ProcessOrder = Processes.Select(p => p.WindowTitle).ToList();
        RebuildSwitchOrder();
        ReorderProcessesBySetting();
        Save();
    }

    /// <summary>从顺序中移除一个"未发现"的旧标题（当前仍存在的客户端不允许移除，只能调整位置）。</summary>
    public void RemoveSwitchOrderEntry(SwitchOrderEntry entry)
    {
        if (entry.IsPresent)
        {
            return;
        }

        SwitchOrderEntries.Remove(entry);
        RenumberSwitchOrderEntries();
        CommitSwitchOrder();
    }

    /// <summary>把列表顺序写回 ProcessOrder、重排左侧客户端列表并保存。</summary>
    private void CommitSwitchOrder()
    {
        _setting.ProcessOrder = SwitchOrderEntries.Select(e => e.WindowTitle).ToList();
        ReorderProcessesBySetting();
        OnPropertyChanged(nameof(HasSwitchOrderEntries));
        Save();
    }

    private void RenumberSwitchOrderEntries()
    {
        for (var i = 0; i < SwitchOrderEntries.Count; i++)
        {
            SwitchOrderEntries[i].UpdatePosition(i, SwitchOrderEntries.Count);
        }
    }

    /// <summary>角色切换后把顺序里的旧窗口标题原位替换成新标题（保留原位置）。</summary>
    private bool ReplaceOrderTitle(string oldTitle, string newTitle)
    {
        var changed = false;
        for (var i = 0; i < _setting.ProcessOrder.Count; i++)
        {
            if (string.Equals(_setting.ProcessOrder[i], oldTitle, StringComparison.Ordinal))
            {
                _setting.ProcessOrder[i] = newTitle;
                changed = true;
            }
        }

        return changed;
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
        LockAspectRatio = source.LockAspectRatio,
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

    /// <summary>
    /// 按磁盘上的配置重启多开（阶段 155，供 AI 的 <c>feature_restart</c> 调用）：
    /// 停止现有预览 → **取消待保存的旧内存数据** → 就地重读配置 → 刷新进程列表 → 原本在跑的预览按新设置重新开始。
    /// </summary>
    public async Task<string> RestartFromSettingsAsync()
    {
        var running = _manager.RunningCount;

        // 不走 StopAll()：它会 Save() 把旧内存设置写回，正好覆盖掉刚被 AI 改好的文件
        _manager.StopAll();
        foreach (var process in Processes)
        {
            process.Running = false;
            process.Setting = ResolveSetting(process);
        }

        GamePreviewSettingService.Current.CancelPendingSave();
        GamePreviewSettingService.Current.ReloadFromDisk();

        // Value 是同一个对象（就地重载），通知一次让 Setting.Xxx 的绑定重新求值
        OnPropertyChanged(nameof(Setting));
        OnPropertyChanged(nameof(SelectedSetting));
        OnPropertyChanged(nameof(SelectedRunning));

        await RefreshAsync();

        if (running > 0)
        {
            StartAll();
        }

        return $"多开预览已重启：停止 {running} 个预览，已重新读取 GamePreviewSetting.json"
               + (running > 0 ? "，并按新设置重新开始预览。" : "。");
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
        // 新建的分组要立刻可见：清掉可能正在生效的搜索
        if (!string.IsNullOrEmpty(_groupFilter))
        {
            GroupFilter = string.Empty;
        }

        var group = new PreviewHotKeyGroup { GroupName = CreateGroupName() };
        _setting.HotKeyGroups.Add(group);
        SelectedHotKeyGroup = group;
        UpdateHotkeyRegistration();
        Save();
    }

    /// <summary>默认分组名：在"分组快捷键"后取一个还没用过的序号（删过中间分组后不会撞名）。</summary>
    private string CreateGroupName()
    {
        var prefix = FindString("GamePreviewPage_GroupHotkey");
        var index = _setting.HotKeyGroups.Count + 1;
        string name;
        do
        {
            name = $"{prefix}{index++}";
        }
        while (_setting.HotKeyGroups.Any(g => string.Equals(g.GroupName, name, StringComparison.OrdinalIgnoreCase)));

        return name;
    }

    public void RemoveGroup(PreviewHotKeyGroup group)
    {
        var wasSelected = ReferenceEquals(_selectedHotKeyGroup, group);
        var index = HotKeyGroups.IndexOf(group);

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

        // 删掉的是当前分组时：优先选原位置之后的下一个可见分组，否则回到第一个可见分组
        if (wasSelected)
        {
            var visible = HotKeyGroupsView.Cast<PreviewHotKeyGroup>().ToList();
            SelectedHotKeyGroup = visible.FirstOrDefault(g => HotKeyGroups.IndexOf(g) >= index)
                ?? visible.FirstOrDefault();
        }

        Save();
    }

    /// <summary>分组的热键/角色名单改动后调用。</summary>
    public void UpdateGroup(PreviewHotKeyGroup group)
    {
        UpdateHotkeyRegistration();
        Save();
    }

    // ---------- 分组单独排序 ----------

    /// <summary>
    /// 重建当前分组的"单独排序"列表：先保留 <see cref="PreviewHotKeyGroup.Order"/> 里仍是成员的顺序，
    /// 其余成员按全局切换顺序（再按名字）追加到末尾。
    /// </summary>
    public void RebuildGroupOrder()
    {
        GroupOrderEntries.Clear();
        if (SelectedHotKeyGroup is not { } group)
        {
            OnPropertyChanged(nameof(HasGroupOrderEntries));
            return;
        }

        var members = group.GameNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();

        foreach (var name in group.Order ?? new List<string>())
        {
            var member = members.FirstOrDefault(m => string.Equals(m, name, StringComparison.OrdinalIgnoreCase));
            if (member is not null && seen.Add(member))
            {
                ordered.Add(member);
            }
        }

        var remaining = members
            .Where(n => !seen.Contains(n))
            .Select(n => (Name: n, GlobalIndex: GlobalNameOrderIndex(n)))
            .OrderBy(x => x.GlobalIndex)
            .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => x.Name);

        ordered.AddRange(remaining);

        foreach (var name in ordered)
        {
            var isRunning = Processes.Any(p => p.Running
                && string.Equals(p.GetCharacterName(), name, StringComparison.OrdinalIgnoreCase));
            GroupOrderEntries.Add(new GroupOrderEntry(name, isRunning));
        }

        RenumberGroupOrderEntries();
        OnPropertyChanged(nameof(HasGroupOrderEntries));

        // 只有"单独排序"模式才把补全后的顺序写回；跟随全局时保留用户之前的自定义顺序
        if (!group.UseGlobalOrder)
        {
            group.Order = ordered;
        }
    }

    public void MoveGroupOrderEntry(GroupOrderEntry entry, int delta)
    {
        if (SelectedHotKeyGroup is not { UseGlobalOrder: false } group)
        {
            return;
        }

        var index = GroupOrderEntries.IndexOf(entry);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= GroupOrderEntries.Count)
        {
            return;
        }

        GroupOrderEntries.Move(index, target);
        RenumberGroupOrderEntries();
        CommitGroupOrder(group);
    }

    public void SortGroupOrderByName()
    {
        if (SelectedHotKeyGroup is not { UseGlobalOrder: false } group)
        {
            return;
        }

        var sorted = GroupOrderEntries
            .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        for (var i = 0; i < sorted.Count; i++)
        {
            var current = GroupOrderEntries.IndexOf(sorted[i]);
            if (current != i)
            {
                GroupOrderEntries.Move(current, i);
            }
        }

        RenumberGroupOrderEntries();
        CommitGroupOrder(group);
    }

    /// <summary>把当前分组顺序重置为"跟随全局"时的顺序（仍保持在单独排序模式）。</summary>
    public void FillGroupOrderFromGlobal()
    {
        if (SelectedHotKeyGroup is not { UseGlobalOrder: false } group)
        {
            return;
        }

        group.Order = [];
        RebuildGroupOrder();
        Save();
    }

    private void CommitGroupOrder(PreviewHotKeyGroup group)
    {
        group.Order = GroupOrderEntries.Select(e => e.Name).ToList();
        Save();
    }

    private void RenumberGroupOrderEntries()
    {
        for (var i = 0; i < GroupOrderEntries.Count; i++)
        {
            GroupOrderEntries[i].UpdatePosition(i, GroupOrderEntries.Count);
        }
    }

    private int GlobalNameOrderIndex(string name)
    {
        var process = Processes.FirstOrDefault(p => string.Equals(p.GetCharacterName(), name, StringComparison.OrdinalIgnoreCase));
        return process is null ? int.MaxValue : OrderIndexOf(process);
    }

    /// <summary>分组自定义顺序里的位置；未列出的成员返回 int.MaxValue（稳定排序后保持全局顺序）。</summary>
    private static int GroupOrderIndex(PreviewHotKeyGroup group, string? name)
    {
        if (string.IsNullOrEmpty(name) || group.Order is not { } order)
        {
            return int.MaxValue;
        }

        for (var i = 0; i < order.Count; i++)
        {
            if (string.Equals(order[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return int.MaxValue;
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
            // 不区分大小写：手工加进来的角色名可能与游戏里的写法大小写不同（旧的 StringSetConverter 也是 IgnoreCase）
            return !string.IsNullOrEmpty(name)
                && group.GameNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        });

        if (targets.Count == 0)
        {
            PageNotifyService.Info(FindString("GamePreviewPage_NoGroupMember"));
            return;
        }

        // 分组单独排序：只调整顺序，不改变成员集合；Order 里没有的成员保持全局顺序排在后面。
        if (!group.UseGlobalOrder)
        {
            targets = targets
                .OrderBy(p => GroupOrderIndex(group, p.GetCharacterName()))
                .ToList();
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

        // 用"用户当前看着的那个客户端"作为下一次切换的起点。
        // 只有前台换成了**另一个**客户端时才清掉分组游标：分组快捷键自己激活目标也会触发前台变化，
        // 若无条件清空，Restart=true 的分组每次都会从第一个成员重新开始（永远切不动）。
        var process = Processes.FirstOrDefault(p => p.MainWindowHandle == hWnd);
        if (process is { Running: true } && process.Setting?.RespondGlobalHotKey == true
            && process.GUID != _lastActiveGuid)
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
