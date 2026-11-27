using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.ChannelIntel;
using TheGuideToTheNewEden.WPF.Services.CombatLog;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.ViewModels.Channel;

/// <summary>
/// 日志监控页：左侧角色列表（每个角色取最新一天的日志文件），中间按配置页签设置监控
/// （配置信息 / 监控模式 / 通知方式 / 监控关键词），右侧实时显示日志内容（命中关键词的行标红）。
/// <para>
/// 监控编排：**一个角色对应一个** <see cref="GameLogMonitorSession"/>，角色下的多个配置只是同一份日志上的关键词过滤器；
/// 配置来源 <c>Configs/GameLogInfoSettings.json</c>（与 WinUI 共用）；文件监听走 Core 的
/// <see cref="Core.Services.ObservableFileService"/> + <see cref="GameLogItem"/>。
/// </para>
/// 与 WinUI 的有意差异：新增/删除配置与改属性都会<b>立即持久化</b>（WinUI 只在"开始监控"时落盘，
/// 新增配置后直接关窗会丢失）。
/// </summary>
public sealed class GameLogMonitorViewModel : INotifyPropertyChanged
{
    private readonly string _logPath = GameLogsSettingService.GetGamelogsPath();
    /// <summary>阶段 390：会话按角色 ID 全局共享（页面 VM / 预览自动启动共用，避免重复会话）。</summary>
    private static readonly Dictionary<int, GameLogMonitorSession> _sessions = [];

    public ObservableCollection<GameLogInfo> GameLogInfos { get; } = [];

    public GameLogMonitorViewModel()
    {
        // 阶段 395：内容由"会话 → 静态广播 → 各 VM"转发，页面 VM 才能看到自动启动会话的实时日志
        ContentUpdated += OnSharedContentUpdated;
        SharedSessionsChanged += OnSharedSessionsChanged;
    }

    /// <summary>阶段 395：会话产出新日志（静态广播，所有 VM 实例都能收到）。</summary>
    private static event Action<GameLogItem, IReadOnlyList<GameLogContent>>? ContentUpdated;

    /// <summary>阶段 395：任何实例启动 / 停止会话后通知其它实例刷新状态并接管内容。</summary>
    private static event Action? SharedSessionsChanged;

    private static void NotifySessionsChanged() => SharedSessionsChanged?.Invoke();

    /// <summary>已被静态挂上内容回调的会话（按角色 ID），避免多个 VM 重复订阅导致日志翻倍。</summary>
    private static readonly HashSet<int> _contentAttached = [];

    private static void EnsureContentAttached(GameLogMonitorSession session)
    {
        lock (_contentAttached)
        {
            if (!_contentAttached.Add(session.ListenerID))
            {
                return;
            }

            session.OnContentUpdate += Session_OnContentUpdateStatic;
        }
    }

    private static void DetachContent(GameLogMonitorSession session)
    {
        lock (_contentAttached)
        {
            if (_contentAttached.Remove(session.ListenerID))
            {
                session.OnContentUpdate -= Session_OnContentUpdateStatic;
            }
        }
    }

    private void OnSharedContentUpdated(GameLogItem item, IReadOnlyList<GameLogContent> contents)
    {
        if (SelectedGameLogInfo is not null && item.Info.ListenerID == SelectedGameLogInfo.ListenerID)
        {
            OnContentUpdate?.Invoke(item, contents);
        }
    }

    private void OnSharedSessionsChanged()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            AdoptRunningSessions();
            return;
        }

        dispatcher.BeginInvoke(new Action(AdoptRunningSessions));
    }

    /// <summary>
    /// 阶段 395：把静态共享会话接管到本 VM —— 角色列表换成会话持有的 <see cref="GameLogInfo"/>
    /// （实时日志写在它身上）、挂上内容回调、标记运行中。页面打开 / 自动启动后都能看到实时日志。
    /// </summary>
    private void AdoptRunningSessions()
    {
        List<GameLogMonitorSession> sessions;
        lock (_sessions)
        {
            sessions = _sessions.Values.ToList();
        }

        var selectedId = SelectedGameLogInfo?.ListenerID;
        foreach (var session in sessions)
        {
            var info = session.Info;
            var existing = GameLogInfos.FirstOrDefault(p => p.ListenerID == info.ListenerID);
            if (existing is not null && !ReferenceEquals(existing, info))
            {
                var index = GameLogInfos.IndexOf(existing);
                GameLogInfos[index] = info;
            }
            else if (existing is null)
            {
                GameLogInfos.Add(info);
            }

            info.Running = true;
            EnsureContentAttached(session);

            // 选中的那一项被换成会话对象后，选中引用要跟着换，否则页面拿不到实时日志
            if (selectedId is { } id && id == info.ListenerID && !ReferenceEquals(SelectedGameLogInfo, info))
            {
                SelectedGameLogInfo = info;
            }
        }

        RefreshRunningState();
        OnPropertyChanged(nameof(SelectedRunning));
        OnPropertyChanged(nameof(CombatStatusText));
    }

    /// <summary>阶段 395：页面可见时主动接管一次，不依赖事件时序。</summary>
    public void SyncRunningState()
    {
        AdoptRunningSessions();
        RefreshRunningState();
    }

    private GameLogInfo? _selectedGameLogInfo;

    public GameLogInfo? SelectedGameLogInfo
    {
        get => _selectedGameLogInfo;
        set
        {
            if (Set(ref _selectedGameLogInfo, value))
            {
                LoadSetting();
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(SelectedRunning));
            }
        }
    }

    private GameLogSetting? _setting;

    /// <summary>选中角色的配置。</summary>
    public GameLogSetting? Setting
    {
        get => _setting;
        private set
        {
            if (Set(ref _setting, value))
            {
                RebuildItemConfigs();
            }
        }
    }

    /// <summary>页面可见的配置集合。</summary>
    public ObservableCollection<GameLogItemConfig> ItemConfigs { get; } = [];

    private GameLogItemConfig? _selectedItemConfig;

    public GameLogItemConfig? SelectedItemConfig
    {
        get => _selectedItemConfig;
        set => Set(ref _selectedItemConfig, value);
    }

    /// <summary>
    /// 异常日志（<c>LogType=1</c>）当前不可用，先在 UI 上隐藏：<b>不删除</b>任何代码路径与已有配置，
    /// 只是把 <c>LogType=1</c> 的配置从页面可见集合中过滤掉，并隐藏"添加配置"里的入口。
    /// 改回 <c>true</c> 即可恢复显示（隐藏期间已有配置仍原样保留在 GameLogInfoSettings.json 中）。
    /// </summary>
    private const bool ErrorLogVisible = false;

    /// <summary>从 <see cref="GameLogSetting.ItemConfigs"/> 重建可见集合；不修改 Setting 本身，隐藏的配置仍会原样落盘。</summary>
    private void RebuildItemConfigs()
    {
        ItemConfigs.Clear();
        // 阶段 357：固定第一个页签为「战斗日志」（不落盘、不可删除），其余是普通关键词监控
        ItemConfigs.Add(CreateCombatConfig());
        if (Setting is not null)
        {
            foreach (var config in Setting.ItemConfigs.Where(IsVisibleConfig))
            {
                ItemConfigs.Add(config);
            }
        }

        // 默认仍停在普通关键词配置上（避免"打开页面点开始"意外启动战斗日志）；没有普通配置时才落在战斗日志页签
        SelectedItemConfig = ItemConfigs.FirstOrDefault(p => p.LogType != 2) ?? ItemConfigs.FirstOrDefault();
    }

    /// <summary>「战斗日志」固定页签的占位配置（LogType=2，只用于页签模板切换，不写进配置列表）。</summary>
    private static GameLogItemConfig CreateCombatConfig()
        => new GameLogItemConfig(2) { ConfigName = FindString("GameLogMonitorPage_CombatLog") };

    private static bool IsVisibleConfig(GameLogItemConfig config)
        => ErrorLogVisible || config.LogType != 1;

    public bool HasSelection => SelectedGameLogInfo is not null;

    private bool _running;

    /// <summary>是否有任意配置正在监控（控制按钮显隐）。</summary>
    public bool Running
    {
        get => _running;
        private set => Set(ref _running, value);
    }

    /// <summary>选中角色是否正在监控（频道/配置列表的可用性）。</summary>
    public bool SelectedRunning => SelectedGameLogInfo?.Running ?? false;

    // ---------- 战斗日志（阶段 357） ----------

    /// <summary>分类开关（反跳/毁电/…，与日志监控页「战斗日志」页签绑定）。</summary>
    public ObservableCollection<CombatCategoryOption> CombatCategoryOptions { get; } = [];

    /// <summary>
    /// 过期时间（秒，默认 60，可手输任意秒数）：某类日志超过该时间没有更新，卡片置灰并提示「已过期」；
    /// 行不会消失（只有手动清除 / 停止监控才清），反跳 / 干扰不参与过期判断。
    /// </summary>
    public double CombatExpireSeconds
    {
        get => GameLogsSettingService.CombatLogExpireSeconds;
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return;
            }

            var seconds = (int)Math.Round(value);
            if (seconds == GameLogsSettingService.CombatLogExpireSeconds)
            {
                return;
            }

            GameLogsSettingService.SetCombatExpire(seconds);
            OnPropertyChanged();
        }
    }

    /// <summary>状态类（电子战 / 反跳）多久没有新日志就变灰（秒，默认 300 = 5 分钟）。</summary>
    public double CombatStatusGreySeconds
    {
        get => GameLogsSettingService.CombatLogStatusGreySeconds;
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return;
            }

            var seconds = (int)Math.Round(value);
            if (seconds == GameLogsSettingService.CombatLogStatusGreySeconds)
            {
                return;
            }

            GameLogsSettingService.SetCombatStatusGreySeconds(seconds);
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 阶段 359：日志监控窗口标题里显示当前舰船类型（ESI 读取，取不到时只显示角色名）。
    /// 存在该角色的 <see cref="GameLogSetting"/> 里，默认开启。
    /// </summary>
    public bool ShowShipInTitle
    {
        get => Setting?.ShowShipInTitle ?? true;
        set
        {
            if (Setting is null || Setting.ShowShipInTitle == value)
            {
                return;
            }

            Setting.ShowShipInTitle = value;
            SaveSetting();
            OnPropertyChanged();

            if (SelectedGameLogInfo is { } info
                && _sessions.TryGetValue(info.ListenerID, out var session))
            {
                session.SetShowShipInTitle(value);
            }
        }
    }

    /// <summary>
    /// 阶段 361：日志监控窗口背板不透明度（1–100，100 = 完全不透明）。
    /// 只影响背板，文字与卡片始终实色；拖动滑块时实时生效，落盘做 400ms 合并。
    /// </summary>
    public int WindowOpacity
    {
        get => Setting?.WindowOpacity ?? 100;
        set
        {
            if (Setting is null)
            {
                return;
            }

            var clamped = Math.Clamp(value, 1, 100);
            if (Setting.WindowOpacity == clamped)
            {
                return;
            }

            Setting.WindowOpacity = clamped;
            OnPropertyChanged();

            if (SelectedGameLogInfo is { } info
                && _sessions.TryGetValue(info.ListenerID, out var session))
            {
                session.SetWindowOpacity(clamped);
            }

            ScheduleOpacitySave();
        }
    }

    private DispatcherTimer? _opacitySaveTimer;

    /// <summary>滑块拖动时合并落盘，避免每一格都写一次 JSON。</summary>
    private void ScheduleOpacitySave()
    {
        if (_opacitySaveTimer is null)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                SaveSetting();
            };
            _opacitySaveTimer = timer;
        }

        _opacitySaveTimer.Stop();
        _opacitySaveTimer.Start();
    }

    /// <summary>
    /// 「战斗日志」开关：只记录"要不要监控"，**不会**因为选中页签/打开开关就自动开始；
    /// 真正开始要用户点「开始监控」，见 <see cref="StartCombat"/>。关闭时若正在跑会立即停止。
    /// </summary>
    public bool CombatEnabled
    {
        get => Setting?.CombatEnabled ?? false;
        set
        {
            if (Setting is null || SelectedGameLogInfo is not { } info)
            {
                return;
            }

            if (Setting.CombatEnabled == value)
            {
                return;
            }

            Setting.CombatEnabled = value;
            SaveSetting();

            if (_sessions.TryGetValue(info.ListenerID, out var session))
            {
                session.SetCombatEnabled(value);
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(CombatStatusText));
            RefreshRunningState();
        }
    }

    public string CombatStatusText
    {
        get
        {
            if (SelectedGameLogInfo is not { } info)
            {
                return string.Empty;
            }

            if (CombatLogStateCenter.Current.IsAttached(info.ListenerName))
            {
                return FindString("CombatLog_StatusRunning");
            }

            return Setting?.CombatEnabled == true
                ? FindString("CombatLog_StatusNotStarted")
                : FindString("CombatLog_StatusStopped");
        }
    }

    private void BuildCombatCategoryOptions()
    {
        CombatCategoryOptions.Clear();
        foreach (var definition in Core.Services.CombatLog.CombatCategories.All)
        {
            CombatCategoryOptions.Add(new CombatCategoryOption(
                definition.Key,
                FindString(CategoryLabelKey(definition.Key)),
                CombatLogStateCenter.Current.IsCategoryEnabled(definition.Key),
                OnCombatCategoryToggled));
        }
    }

    private static string CategoryLabelKey(string category) => category switch
    {
        Core.Services.CombatLog.CombatCategories.Scram => "CombatCategory_Scram",
        Core.Services.CombatLog.CombatCategories.EwOther => "CombatCategory_EwOther",
        Core.Services.CombatLog.CombatCategories.Neut => "CombatCategory_Neut",
        Core.Services.CombatLog.CombatCategories.Nos => "CombatCategory_Nos",
        Core.Services.CombatLog.CombatCategories.Ecm => "CombatCategory_Ecm",
        Core.Services.CombatLog.CombatCategories.Web => "CombatCategory_Web",
        Core.Services.CombatLog.CombatCategories.Painter => "CombatCategory_Painter",
        Core.Services.CombatLog.CombatCategories.WeaponDisrupt => "CombatCategory_WeaponDisrupt",
        Core.Services.CombatLog.CombatCategories.SensorDamp => "CombatCategory_SensorDamp",
        Core.Services.CombatLog.CombatCategories.Rep => "CombatCategory_Rep",
        Core.Services.CombatLog.CombatCategories.CapTransfer => "CombatCategory_CapTransfer",
        Core.Services.CombatLog.CombatCategories.Damage => "CombatCategory_Damage",
        _ => category,
    };

    private void OnCombatCategoryToggled(CombatCategoryOption option)
    {
        var enabled = CombatCategoryOptions.Where(p => p.Enabled).Select(p => p.Key).ToList();
        GameLogsSettingService.SetCombatCategories(enabled.Count == 0 ? [] : enabled);
    }

    public bool StartCombat()
    {
        if (SelectedGameLogInfo is not { } info)
        {
            PageNotifyService.Error(FindString("General_CharacterUnselected"));
            return false;
        }

        if (Setting?.CombatEnabled != true)
        {
            PageNotifyService.Warning(FindString("CombatLog_EnableFirst"));
            return false;
        }

        if (_sessions.TryGetValue(info.ListenerID, out var session))
        {
            session.SetCombatEnabled(true);
        }
        else
        {
            return Start();
        }

        OnPropertyChanged(nameof(CombatStatusText));
        RefreshRunningState();
        return true;
    }

    public void StopCombat()
    {
        if (SelectedGameLogInfo is { } info)
        {
            if (_sessions.TryGetValue(info.ListenerID, out var session))
            {
                session.SetCombatEnabled(false);
            }

            OnPropertyChanged(nameof(CombatStatusText));
            RefreshRunningState();
        }
    }

    /// <summary>重新打开当前角色的战斗日志小窗。</summary>
    public void OpenCombatWindow()
    {
        if (SelectedGameLogInfo is { } info
            && _sessions.TryGetValue(info.ListenerID, out var session)
            && CombatLogStateCenter.Current.IsAttached(info.ListenerName))
        {
            session.ShowWindow();
        }
        else
        {
            PageNotifyService.Info(FindString("CombatLog_NotRunning"));
        }
    }

    /// <summary>日志内容更新（页面着色显示）。</summary>
    public event GameLogItem.ContentUpdate? OnContentUpdate;

    // ---------- 初始化 ----------

    public async Task InitAsync(bool quiet = false)
    {
        if (!quiet)
        {
            PageNotifyService.ShowWaiting(FindString("General_RefreshList"));
        }

        try
        {
            var runningInfos = GameLogInfos.Where(p => p.Running).ToDictionary(p => p.ListenerID);
            var infos = await Task.Run(() =>
            {
                try
                {
                    return GameLogHelper.GetLatestGameLogInfos(_logPath) ?? [];
                }
                catch (Exception ex)
                {
                    Core.Log.Error(ex);
                    return [];
                }
            });

            GameLogInfos.Clear();
            foreach (var info in infos)
            {
                // 正在监控的角色复用原对象：会话仍持有它，且 LogContents / Setting 不会因刷新丢失
                GameLogInfos.Add(runningInfos.TryGetValue(info.ListenerID, out var running) ? running : info);
            }

            SelectedGameLogInfo = null;

            // 阶段 395：多开预览自动启动的会话要接管过来（否则页面只有空列表、看不到实时日志）
            AdoptRunningSessions();
            RefreshRunningState();
        }
        finally
        {
            if (!quiet)
            {
                PageNotifyService.HideWaiting();
            }
        }
    }

    private void LoadSetting()
    {
        if (SelectedGameLogInfo is null)
        {
            Setting = null;
            return;
        }

        var setting = GameLogInfoSettingService.GetValue(SelectedGameLogInfo.ListenerID);
        if (setting is null)
        {
            setting = new GameLogSetting { ListenerID = SelectedGameLogInfo.ListenerID };
            setting.ItemConfigs.Add(CreateGameLogConfig());
            if (ErrorLogVisible)
            {
                setting.ItemConfigs.Add(CreateErrorLogConfig());
            }
        }

        Setting = setting;

        BuildCombatCategoryOptions();
        OnPropertyChanged(nameof(CombatEnabled));
        OnPropertyChanged(nameof(CombatExpireSeconds));
        OnPropertyChanged(nameof(CombatStatusText));
        OnPropertyChanged(nameof(ShowShipInTitle));
        OnPropertyChanged(nameof(WindowOpacity));
    }

    // ---------- 默认配置 ----------

    private static GameLogItemConfig CreateGameLogConfig()
    {
        var config = new GameLogItemConfig(0)
        {
            ConfigName = FindString("GameLogMonitorPage_Type_GameLog"),
        };
        config.Keys.Add(new GameLogMonityKey("combat"));
        return config;
    }

    private static GameLogItemConfig CreateErrorLogConfig()
    {
        var config = new GameLogItemConfig(1)
        {
            ConfigName = FindString("GameLogMonitorPage_Type_ErrorLog"),
        };
        foreach (var regex in ReadErrorRegex())
        {
            config.Keys.Add(new GameLogMonityKey(regex));
        }

        return config;
    }

    /// <summary>异常日志默认关键词：随应用发布的 <c>Resources/Configs/GameThreadErrorLogRegex.txt</c>（每行一个）。</summary>
    private static string[] ReadErrorRegex()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Configs", "GameThreadErrorLogRegex.txt");
            return File.Exists(path) ? File.ReadAllLines(path).Where(p => !string.IsNullOrWhiteSpace(p)).ToArray() : [];
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return [];
        }
    }

    // ---------- 监控控制 ----------

    /// <summary>开始前把角色日志路径更新到当前最新一份，避免游戏重开后仍监听旧日志。</summary>
    private void RefreshLatestGameLogFile(GameLogInfo info)
    {
        try
        {
            var latest = GameLogHelper.GetLatestGameLogInfos(_logPath);
            var newest = latest?.FirstOrDefault(p => p.ListenerID == info.ListenerID);
            if (newest is null)
            {
                return;
            }

            info.FilePath = newest.FilePath;
            info.ListenerName = newest.ListenerName;
            info.StartTime = newest.StartTime;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    /// <summary>开始监控"当前角色的全部配置"（关键词配置共用一个会话/窗口，战斗日志按开关启动）。</summary>
    public bool Start()
    {
        if (SelectedGameLogInfo is not { } info || Setting is null)
        {
            PageNotifyService.Error(FindString("General_CharacterUnselected"));
            return false;
        }

        return StartCharacter(info, Setting);
    }

    /// <summary>
    /// 启动一个角色：该角色的所有关键词配置合并成一个 <see cref="GameLogMonitorSession"/>，
    /// 战斗开关开着时再启动战斗解析；两者共用同一个窗口。
    /// </summary>
    /// <summary>阶段 392：同一角色的启动串行化，避免并发创建两个监控会话/窗口。</summary>
    private static readonly object StartLock = new object();

    private bool StartCharacter(GameLogInfo info, GameLogSetting setting, bool quiet = false)
    {
        lock (StartLock)
        {
            return StartCharacterCore(info, setting, quiet);
        }
    }

    private bool StartCharacterCore(GameLogInfo info, GameLogSetting setting, bool quiet)
    {
        var configs = setting.ItemConfigs.Where(IsVisibleConfig).ToList();
        if (configs.Count == 0 && !setting.CombatEnabled)
        {
            if (!quiet)
            {
                PageNotifyService.Error(FindString("GameLogMonitorPage_NoneKeyError"));
            }

            return false;
        }

        RefreshLatestGameLogFile(info);

        if (!_sessions.TryGetValue(info.ListenerID, out var session))
        {
            session = new GameLogMonitorSession(
                info,
                configs,
                info.FilePath,
                setting.ShowShipInTitle,
                setting.WindowOpacity,
                setting.CombatEnabled);
            if (!session.Start())
            {
                session.Stop();
                if (!quiet)
                {
                    PageNotifyService.Error(FindString("GameLogMonitorPage_AddNotifyServiceFalied"));
                }

                return false;
            }

            EnsureContentAttached(session);
            _sessions[info.ListenerID] = session;
        }
        else if (setting.CombatEnabled)
        {
            session.SetCombatEnabled(true);
        }

        info.Running = true;
        RefreshRunningState();
        SaveSetting();
        NotifySessionsChanged();
        if (!quiet)
        {
            PageNotifyService.Success($"{FindString("GameLogMonitorPage_Start")}：{info.ListenerName}");
        }

        return true;
    }

    /// <summary>
    /// 阶段 390：多开预览开始显示该角色时，尝试自动启动它的战斗日志监控。
    /// <para>已运行（含只有战斗的会话）直接复用；找不到该角色的游戏日志返回 false。</para>
    /// </summary>
    public async Task<bool> EnsureCombatStartedForPreviewAsync(string listener)
    {
        if (string.IsNullOrWhiteSpace(listener))
        {
            return false;
        }

        if (GameLogInfos.Count == 0)
        {
            await InitAsync(quiet: true);
        }

        var info = GameLogInfos.FirstOrDefault(
            p => string.Equals(p.ListenerName, listener, StringComparison.OrdinalIgnoreCase));
        if (info is null)
        {
            Core.Log.Info($"多开预览：日志监控自动启动跳过 {listener}（没有找到该角色的游戏日志）");
            return false;
        }

        if (_sessions.TryGetValue(info.ListenerID, out var running))
        {
            running.SetCombatEnabled(true);
            info.Running = true;
            EnsureContentAttached(running);
            RefreshRunningState();
            NotifySessionsChanged();
            return true;
        }

        var setting = GameLogInfoSettingService.GetValue(info.ListenerID);
        if (setting is null)
        {
            setting = new GameLogSetting { ListenerID = info.ListenerID };
            setting.ItemConfigs.Add(CreateGameLogConfig());
        }

        setting.CombatEnabled = true;
        var started = StartCharacter(info, setting, quiet: true);
        if (started)
        {
            NotifySessionsChanged();
        }

        return started;
    }

    /// <summary>
    /// 开始全部：遍历所有角色，每个角色启动其全部关键词配置（战斗开关开着时一并启动战斗），
    /// 不依赖当前是否选中了角色；结束后恢复原来的选中项。
    /// </summary>
    public void StartAll()
    {
        var original = SelectedGameLogInfo;
        try
        {
            foreach (var info in GameLogInfos.ToList())
            {
                try
                {
                    SelectedGameLogInfo = info;
                    if (Setting is null)
                    {
                        continue;
                    }

                    StartCharacter(info, Setting);
                }
                catch (Exception ex)
                {
                    // 某个角色的日志文件缺失/配置损坏不应中断其他角色的"开始全部"
                    Core.Log.Error(ex);
                }
            }
        }
        finally
        {
            SelectedGameLogInfo = original;
            RefreshRunningState();
        }
    }

    /// <summary>停止当前角色的全部配置。</summary>
    public void Stop()
    {
        if (SelectedGameLogInfo is { } info)
        {
            StopCharacter(info);
        }
    }

    private void StopCharacter(GameLogInfo info)
    {
        if (_sessions.Remove(info.ListenerID, out var session))
        {
            DetachContent(session);
            session.Stop();
        }

        info.Running = false;
        RefreshRunningState();
        SaveSetting();
        NotifySessionsChanged();
    }

    public void StopAll()
    {
        foreach (var session in _sessions.Values.ToList())
        {
            DetachContent(session);
            session.Stop();
        }

        _sessions.Clear();

        foreach (var info in GameLogInfos)
        {
            info.Running = false;
        }

        OnPropertyChanged(nameof(CombatStatusText));
        RefreshRunningState();
    }

    /// <summary>
    /// AI 功能重启（阶段 156）：停止全部会话 → 重读 GameLogInfoSettings.json → 按原 GUID 找到配置 →
    /// 把原本在监控的"角色 + 配置"按新设置重新开始。
    /// </summary>
    public Task<string> RestartFromSettingsAsync()
    {
        var running = _sessions.Keys.ToList();

        StopAll();
        GameLogInfoSettingService.Reload();

        var restarted = 0;
        foreach (var listenerId in running)
        {
            var info = GameLogInfos.FirstOrDefault(p => p.ListenerID == listenerId);
            if (info is null)
            {
                continue;
            }

            // 先置空再赋值：同一个对象不会触发 Setter，也就不会重新 LoadSetting
            SelectedGameLogInfo = null;
            SelectedGameLogInfo = info;
            if (Setting is not null && StartCharacter(info, Setting))
            {
                restarted++;
            }
        }

        return Task.FromResult(
            $"日志监控已重启：停止 {running.Count} 个角色会话，重读 GameLogInfoSettings.json 后重新开始 {restarted} 个。");
    }

    /// <summary>
    /// 阶段 395：内容是**会话级**的单点回调（每个会话只挂一次），负责写入 LogContents 再广播给各 VM；
    /// 原来挂在 VM 实例上，自动启动的会话写不进页面 VM 的数据，页面就看不到实时日志。
    /// </summary>
    private static void Session_OnContentUpdateStatic(GameLogItem item, IEnumerable<GameLogContent> news)
    {
        var contents = (news as IList<GameLogContent> ?? news.ToList()).ToList();
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        // 内容来自后台的日志读取线程，统一回到 UI 线程后再改动集合/通知页面，
        // 否则切换角色重绘时可能与后台追加并发读写 LogContents。
        dispatcher.BeginInvoke(() =>
        {
            item.Info.LogContents.AddRange(contents);
            TrimContents(item.Info);
            ContentUpdated?.Invoke(item, contents);
        });
    }

    /// <summary>只保留最近 <c>MaxShowItems</c> 条，避免长时间监控内存无限增长。</summary>
    private static void TrimContents(GameLogInfo info)
    {
        var max = GameLogsSettingService.MaxShowItems;
        if (max > 0 && info.LogContents.Count > max)
        {
            info.LogContents.RemoveRange(0, info.LogContents.Count - max);
        }
    }

    /// <summary>停止提醒（声音/弹窗），不停监控。</summary>
    public void StopNotify()
    {
        if (SelectedGameLogInfo is { } info
            && _sessions.TryGetValue(info.ListenerID, out var session))
        {
            session.StopNotify();
        }
    }

    public void StopNotifyAll()
    {
        foreach (var session in _sessions.Values)
        {
            session.StopNotify();
        }
    }

    // ---------- 配置增删 ----------

    /// <summary>新增配置（0 游戏日志 / 1 异常日志），并立即持久化。异常日志隐藏期间仍会落盘、但不显示。</summary>
    public void AddConfig(int logType)
    {
        if (Setting is null)
        {
            return;
        }

        var config = logType == 1 ? CreateErrorLogConfig() : CreateGameLogConfig();
        Setting.ItemConfigs.Add(config);
        if (IsVisibleConfig(config))
        {
            ItemConfigs.Add(config);
            SelectedItemConfig = config;
        }

        SaveSetting();

        // 新过滤器运行中立即生效
        if (SelectedGameLogInfo is { } info && _sessions.ContainsKey(info.ListenerID))
        {
            StopCharacter(info);
            StartCharacter(info, Setting);
        }
    }

    /// <summary>删除配置（若在监控中先停止），并立即持久化。</summary>
    public void RemoveConfig(GameLogItemConfig config)
    {
        if (Setting is null)
        {
            return;
        }

        Setting.ItemConfigs.Remove(config);
        ItemConfigs.Remove(config);
        SelectedItemConfig = ItemConfigs.FirstOrDefault();

        // 配置只是过滤器：运行中删除后按新配置重启该角色会话即可生效
        if (SelectedGameLogInfo is { } info && _sessions.ContainsKey(info.ListenerID))
        {
            StopCharacter(info);
            StartCharacter(info, Setting);
        }
        else
        {
            RefreshRunningState();
        }

        SaveSetting();
    }

    public void AddKey()
    {
        SelectedItemConfig?.Keys.Add(new GameLogMonityKey(FindString("GameLogMonitorPage_NewKeyDefault")));
        SaveSetting();
    }

    public void DeleteKey(GameLogMonityKey key)
    {
        SelectedItemConfig?.Keys.Remove(key);
        SaveSetting();
    }

    public void PickSoundFile()
    {
        if (SelectedItemConfig is not { } config)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Audio|*.mp3;*.wav;*.wma;*.flac|All|*.*",
        };
        if (dialog.ShowDialog() == true)
        {
            config.SoundFile = dialog.FileName;
            SaveSetting();
        }
    }

    /// <summary>属性变更后立即落盘（页面在配置控件变更时调用）。</summary>
    public void SaveSetting()
    {
        if (Setting is not null)
        {
            GameLogInfoSettingService.SetValue(Setting);
        }
    }

    public void Dispose()
    {
        // 透明度滑块可能还在 400ms 合并窗口里，关页面前补一次落盘
        if (_opacitySaveTimer is { IsEnabled: true })
        {
            _opacitySaveTimer.Stop();
            SaveSetting();
        }

        StopAll();
    }

    private void RefreshRunningState()
    {
        foreach (var info in GameLogInfos)
        {
            info.Running = _sessions.ContainsKey(info.ListenerID);
        }

        Running = _sessions.Count > 0 || GameLogInfos.Any(p => p.Running);
        OnPropertyChanged(nameof(SelectedRunning));
        OnPropertyChanged(nameof(CombatStatusText));
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;

    // ---------- INotifyPropertyChanged ----------

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

/// <summary>「战斗日志」页签里的单个分类开关。</summary>
public sealed class CombatCategoryOption
{
    private readonly Action<CombatCategoryOption> _changed;
    private bool _enabled;

    public CombatCategoryOption(string key, string label, bool enabled, Action<CombatCategoryOption> changed)
    {
        Key = key;
        Label = label;
        _enabled = enabled;
        _changed = changed;
    }

    public string Key { get; }

    public string Label { get; }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }

            _enabled = value;
            _changed(this);
        }
    }
}
