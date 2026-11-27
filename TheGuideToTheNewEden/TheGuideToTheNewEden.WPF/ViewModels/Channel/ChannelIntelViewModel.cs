using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.ChannelIntel;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services.Settings;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.Services.ChannelIntel;
using TheGuideToTheNewEden.WPF.Services.Settings;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.ViewModels.Channel;

/// <summary>
/// 频道预警页：左侧角色列表（每个角色一个 <see cref="ChannelIntelSession"/>），
/// 选中后中间列出该角色的聊天频道（勾选预警频道），右侧预警设置与频道内容。
/// 取数与监控编排走会话层；等待/错误提示走 <see cref="PageNotifyService"/>。
/// </summary>
public sealed class ChannelIntelViewModel : INotifyPropertyChanged
{
    private readonly string _logPath;
    private readonly Dictionary<string, List<ChatChanelInfoItem>> _listenerChannelDic = [];
    /// <summary>阶段 390：会话按角色名全局共享（页面 VM / 预览自动启动共用，避免重复会话）。</summary>
    private static readonly Dictionary<string, ChannelIntelSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _chatQueueLock = new();
    private readonly Queue<IntelChatContent> _pendingChatContents = new();
    private readonly Queue<EarlyWarningContent> _pendingZkbContents = new();
    private bool _chatFlushScheduled;
    private bool _zkbFlushScheduled;

    private const int MaxChatContents = 2000;
    private const int MaxZkbIntelContents = 500;
    private const int MaxPendingChatContents = 2000;
    private const int MaxPendingZkbContents = 500;
    private const int ChatFlushBatchSize = 500;
    private const int ZkbFlushBatchSize = 200;

    /// <summary>阶段 307：预警小窗全局设置（统一尺寸/排列/吸附/标签阈值），供页面卡片绑定。</summary>
    public IntelGlobalSetting GlobalIntelSetting => IntelGlobalSettingService.Value;

    private List<string> _nameDbs = [];

    /// <summary>预警星系名语言库（Resources/Database/Local 下的 .db，首项为主库 default(en)）。</summary>
    public List<string> NameDbs
    {
        get => _nameDbs;
        private set => Set(ref _nameDbs, value);
    }

    private List<MapSolarSystemBase> _mapSolarSystems = [];

    public List<MapSolarSystemBase> MapSolarSystems
    {
        get => _mapSolarSystems;
        private set => Set(ref _mapSolarSystems, value);
    }

    private ChannelIntelListener? _selectedCharacter;

    public ChannelIntelListener? SelectedCharacter
    {
        get => _selectedCharacter;
        set
        {
            if (Set(ref _selectedCharacter, value))
            {
                UpdateSelectedCharacter(value?.Name);
            }
        }
    }

    public ObservableCollection<ChannelIntelListener> Characters { get; private set; } = [];

    private ChannelIntelSession? _session;

    /// <summary>选中角色的预警会话（设置/频道列表都绑定在它上面）。</summary>
    public ChannelIntelSession? Session
    {
        get => _session;
        private set
        {
            if (Set(ref _session, value))
            {
                OnPropertyChanged(nameof(HasSession));
            }
        }
    }

    /// <summary>是否已选中角色（控制中/右栏显示）。</summary>
    public bool HasSession => Session is not null;

    /// <summary>选中角色是否正在预警（停止按钮/设置区可用性的状态源）。</summary>
    public bool SelectedRunning => Session?.Running ?? SelectedCharacter?.Running ?? false;

    /// <summary>选中角色未在预警（开始按钮显隐）。与 <see cref="SelectedRunning"/> 同源计算，
    /// 两者必须在同一处一起通知——否则绑定到漏通知的那个会静默保持控件默认值（按钮常显）。</summary>
    public bool SelectedNotRunning => !SelectedRunning;

    private bool _running;

    /// <summary>任意角色正在预警。</summary>
    public bool Running
    {
        get => _running;
        private set => Set(ref _running, value);
    }

    /// <summary>频道原始消息（"频道内容"页签）。</summary>
    public ObservableCollection<IntelChatContent> ChatContents { get; } = [];

    /// <summary>ZKB 击杀预警消息。</summary>
    public ObservableCollection<EarlyWarningContent> ZKBIntelContents { get; } = [];

    public ChannelIntelViewModel()
    {
        _logPath = GameLogsSettingService.GetChatlogsPath();
        InitNameDbs();
        _ = InitSolarSystemsAsync();
        _ = InitDicAsync();

        // 阶段 393：预警会话被自动启动 / 停止时，页面上的"运行中"与底部按钮跟着变
        ChannelIntelAlertCenter.Current.ListenersChanged += OnIntelListenersChanged;
        SharedSessionsChanged += OnSharedSessionsChanged;
    }

    /// <summary>阶段 393：会话启停后通知所有 VM 实例刷新（页面 VM 与自动启动 VM 共用静态会话）。</summary>
    private static event Action? SharedSessionsChanged;

    private static void NotifySessionsChanged() => SharedSessionsChanged?.Invoke();

    private void OnSharedSessionsChanged()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            SyncRunningFromSessions();
            return;
        }

        dispatcher.BeginInvoke(new Action(SyncRunningFromSessions));
    }

    private void OnIntelListenersChanged(object? sender, EventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || !dispatcher.CheckAccess())
        {
            dispatcher?.BeginInvoke(new Action(SyncRunningFromSessions));
            return;
        }

        SyncRunningFromSessions();
    }

    private void InitNameDbs()
    {
        var dbs = new List<string> { "default(en)" };
        var local = LocalDbSelectorService.GetAll();
        if (local is { Count: > 0 })
        {
            dbs.AddRange(local);
        }

        NameDbs = dbs;
    }

    private async Task InitDicAsync()
    {
        await LoadListenerChannelDicAsync();
        Characters = [.. _listenerChannelDic.Keys.Select(CreateListener)];

        // 阶段 390/393：会话是静态共享的，可能已被"多开预览自动启动"拉起——把运行状态同步回列表
        SyncRunningFromSessions();
        OnPropertyChanged(nameof(Characters));
    }

    /// <summary>角色列表头像是 ESI portrait；找不到授权角色时 URL 留空，界面隐藏头像。</summary>
    private static ChannelIntelListener CreateListener(string name)
    {
        var listener = new ChannelIntelListener(name);
        var character = CharacterStore.Characters.FirstOrDefault(
            p => string.Equals(p.CharacterName, name, StringComparison.OrdinalIgnoreCase));
        if (character is not null)
        {
            listener.CharacterId = character.CharacterID;
            listener.AvatarUrl = GameImageHelper.BuildCharacterPortraitUrl(character.CharacterID, 64);
        }

        return listener;
    }

    private async Task LoadListenerChannelDicAsync()
    {
        _listenerChannelDic.Clear();
        if (!Directory.Exists(_logPath))
        {
            Core.Log.Warn($"频道预警：聊天日志目录不存在：{_logPath}");
            return;
        }

        await Task.Run(() =>
        {
            try
            {
                var dic = GameLogHelper.GetChatChanelInfos(_logPath, Math.Max(1, GameLogsSettingService.EVELogsChannelDurationValue));
                if (dic is null)
                {
                    return;
                }

                foreach (var item in dic)
                {
                    _listenerChannelDic.Add(item.Key, item.Value.Select(p => new ChatChanelInfoItem(p)).ToList());
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        });
    }

    private async Task InitSolarSystemsAsync()
    {
        var list = await Core.Services.DB.MapSolarSystemService.QueryAllAsync();
        if (list is { Count: > 0 })
        {
            MapSolarSystems = [.. list.Cast<MapSolarSystemBase>()];
        }
    }

    private void UpdateSelectedCharacter(string? name)
    {
        Session = string.IsNullOrEmpty(name) ? null : GetSession(name);
        OnPropertyChanged(nameof(SelectedRunning));
        OnPropertyChanged(nameof(SelectedNotRunning));
    }

    private ChannelIntelSession GetSession(string name)
    {
        // 阶段 392：会话字典是静态共享的，取/建都要在同一把锁里，避免并发建出两份同名会话
        lock (_sessions)
        {
            if (_sessions.TryGetValue(name, out var session))
            {
                return session;
            }

            var created = new ChannelIntelSession(
                name,
                _listenerChannelDic.TryGetValue(name, out var channels) ? channels : [],
                MapSolarSystems,
                NameDbs);
            created.WindowStopRequested += Session_WindowStopRequested;

            // 阶段 396：会话一置为运行中立刻广播，界面不用等 Start 全部返回
            created.RunningChanged += (_, _) => NotifySessionsChanged();
            _sessions.Add(name, created);
            return created;
        }
    }

    private void Session_WindowStopRequested(object? sender, EventArgs e)
    {
        // 预警小窗被手动关闭：复位该角色的运行状态
        var listener = (sender as ChannelIntelSession)?.Listener;
        StopSession(GetSession(listener!));
        PageNotifyService.Info($"{FindString("ChannelIntelPage_Stop")}：{listener}");
    }

    private void UnsubscribeEvents(ChannelIntelSession? session)
    {
        if (session is null)
        {
            return;
        }

        session.ChatContentEvent -= Session_ChatContentEvent;
        session.ZKBIntelEvent -= Session_ZKBIntelEvent;
        session.OnZKBError -= Session_OnZKBError;
        _attachedListeners.Remove(session.Listener);
    }

    /// <summary>阶段 395：本 VM 已订阅内容的会话（按角色名），避免重复订阅。</summary>
    private readonly HashSet<string> _attachedListeners = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>阶段 392：同一角色的启动串行化，避免并发 Start 把同一会话启动两次。</summary>
    private static readonly Dictionary<string, SemaphoreSlim> _startGates = new(StringComparer.OrdinalIgnoreCase);

    private static SemaphoreSlim StartGateOf(string listener)
    {
        lock (_startGates)
        {
            if (!_startGates.TryGetValue(listener, out var gate))
            {
                gate = new SemaphoreSlim(1, 1);
                _startGates[listener] = gate;
            }

            return gate;
        }
    }

    private async Task<bool> StartAsync(ChannelIntelSession? session, ChannelIntelListener? listener, bool quiet = false)
    {
        if (session is null || listener is null)
        {
            if (!quiet)
            {
                PageNotifyService.Error(FindString("General_CharacterUnselected"));
            }

            return false;
        }

        var gate = StartGateOf(session.Listener);
        await gate.WaitAsync();
        try
        {
            // 排队期间可能已被另一次调用启动：直接按成功返回，绝不再 Start 一遍
            if (session.Running)
            {
                listener.Running = true;
                RefreshRunningState();
                return true;
            }

            if (!quiet)
            {
                PageNotifyService.ShowWaiting($"{FindString("ChannelIntelPage_Start")}：{session.Listener}");
            }

            await session.Start();
            listener.Running = true;
            NotifySessionsChanged();
            UnsubscribeEvents(session);
            session.ChatContentEvent += Session_ChatContentEvent;
            session.ZKBIntelEvent += Session_ZKBIntelEvent;
            session.OnZKBError += Session_OnZKBError;
            _attachedListeners.Add(session.Listener);
            RefreshRunningState();
            OnPropertyChanged(nameof(SelectedRunning));
            OnPropertyChanged(nameof(SelectedNotRunning));
            return true;
        }
        catch (Exception ex)
        {
            IntelWarningService.Current.Remove(session.Listener);
            Core.Log.Error(ex);
            if (!quiet)
            {
                PageNotifyService.Error(ex.Message);
            }

            return false;
        }
        finally
        {
            gate.Release();
            if (!quiet)
            {
                PageNotifyService.HideWaiting();
            }
        }
    }

    /// <summary>
    /// 阶段 390：多开预览开始显示该角色时，尝试自动启动它的频道预警。
    /// <para>已运行直接成功；会话列表还在加载时最多等 3 秒；找不到同名角色（无聊天日志）返回 false。</para>
    /// </summary>
    public async Task<bool> EnsureIntelStartedForPreviewAsync(string listener)
    {
        if (string.IsNullOrWhiteSpace(listener))
        {
            return false;
        }

        lock (_sessions)
        {
            if (_sessions.TryGetValue(listener, out var running) && running.Running)
            {
                return true;
            }
        }

        for (var i = 0; i < 30 && Characters.Count == 0; i++)
        {
            await Task.Delay(100);
        }

        var character = Characters.FirstOrDefault(
            p => string.Equals(p.Name, listener, StringComparison.OrdinalIgnoreCase));
        if (character is null)
        {
            Core.Log.Info($"多开预览：频道预警自动启动跳过 {listener}（聊天日志里没有该角色）");
            return false;
        }

        // 阶段 394：必须用**聊天日志里的规范角色名**建会话——预览拿到的名字可能大小写不同，
        // 直接拿输入名建会话会让频道预警页按自己的名字查不到这个"运行中"的会话。
        return await StartAsync(GetSession(character.Name), character, quiet: true);
    }

    public Task StartAsync() => StartAsync(Session, SelectedCharacter);

    public async Task StartAllAsync()
    {
        foreach (var character in Characters)
        {
            await StartAsync(GetSession(character.Name), character);
        }
    }

    public void StopSelected()
    {
        if (Session is null || SelectedCharacter is null)
        {
            return;
        }

        StopSession(Session);
    }

    public void StopAll()
    {
        foreach (var session in _sessions.Values)
        {
            StopSession(session);
        }

        RefreshRunningState();
        OnPropertyChanged(nameof(SelectedRunning));
        OnPropertyChanged(nameof(SelectedNotRunning));
    }

    /// <summary>
    /// AI 功能重启（阶段 156）：停止全部会话 → 重读 IntelSettings.json → 重建会话 →
    /// 把原本在预警的角色按新配置重新开始。返回给模型/用户的结果说明。
    /// </summary>
    public async Task<string> RestartFromSettingsAsync()
    {
        var running = Characters.Where(p => p.Running).Select(p => p.Name).ToList();

        StopAll();
        IntelSettingService.Reload();
        _sessions.Clear();
        UpdateSelectedCharacter(SelectedCharacter?.Name);

        var restarted = 0;
        foreach (var name in running)
        {
            var character = Characters.FirstOrDefault(p => p.Name == name);
            if (character is not null && await StartAsync(GetSession(name), character))
            {
                restarted++;
            }
        }

        return $"频道预警已重启：停止 {running.Count} 个会话，重读 IntelSettings.json 后重新开始 {restarted} 个。";
    }

    private void StopSession(ChannelIntelSession session)
    {
        UnsubscribeEvents(session);
        session.Stop();
        NotifySessionsChanged();
        var listener = Characters.FirstOrDefault(p => p.Name == session.Listener);
        if (listener is not null)
        {
            listener.Running = false;
        }

        RefreshRunningState();
        OnPropertyChanged(nameof(SelectedRunning));
        OnPropertyChanged(nameof(SelectedNotRunning));
    }

    private void RefreshRunningState()
    {
        // 阶段 393：会话是静态共享的，可能由多开预览自动启动——"是否有人在预警"以会话为准，
        // 否则从预览页回来时底部只显示"开始全部"，看不到"停止所有预警"。
        lock (_sessions)
        {
            Running = _sessions.Values.Any(p => p.Running) || Characters.Any(p => p.Running);
        }
    }

    /// <summary>
    /// 阶段 394：页面可见 / 角色列表变化时，主动把共享会话的运行状态刷到角色列表。
    /// 不依赖事件时序——哪怕自动启动发生在页面创建之前、或通知被并发吞掉，这里也会纠正一次。
    /// </summary>
    public void SyncRunningState()
    {
        AdoptRunningSessions();
        SyncRunningFromSessions();
    }

    /// <summary>
    /// 阶段 395：把**已经在运行**的共享会话接管到本 VM（多开预览自动启动的那些）——
    /// 订阅聊天 / ZKB 内容，页面的"频道内容"才能看到实时消息。
    /// </summary>
    private void AdoptRunningSessions()
    {
        List<ChannelIntelSession> sessions;
        lock (_sessions)
        {
            sessions = _sessions.Values.ToList();
        }

        foreach (var session in sessions)
        {
            if (!session.Running || !_attachedListeners.Add(session.Listener))
            {
                continue;
            }

            session.ChatContentEvent += Session_ChatContentEvent;
            session.ZKBIntelEvent += Session_ZKBIntelEvent;
            session.OnZKBError += Session_OnZKBError;
            Core.Log.Info($"频道预警：接管运行中的会话 {session.Listener}（实时内容）");
        }
    }

    /// <summary>把静态会话的运行状态同步到角色列表（多开预览自动启动后也能显示为运行中）。</summary>
    private void SyncRunningFromSessions()
    {
        int sessionCount;
        int runningCount;
        lock (_sessions)
        {
            sessionCount = _sessions.Count;
            runningCount = _sessions.Values.Count(p => p.Running);
            foreach (var character in Characters)
            {
                character.Running = _sessions.TryGetValue(character.Name, out var session) && session.Running;
            }
        }

        Core.Log.Info($"频道预警状态同步：角色列表 {Characters.Count}，共享会话 {sessionCount}，运行中 {runningCount}");

        RefreshRunningState();
        OnPropertyChanged(nameof(SelectedRunning));
        OnPropertyChanged(nameof(SelectedNotRunning));
    }

    public void RestorePos()
    {
        if (Session is null)
        {
            return;
        }

        PageNotifyService.Info(Session.RestorePos() ? "重置成功" : "重置失败");
    }

    public void StopSound()
        => Session?.StopSound();

    public async Task RefreshChannelsAsync()
    {
        if (Session is null)
        {
            return;
        }

        var name = Session.Listener;
        PageNotifyService.ShowWaiting(FindString("ChannelIntelPage_RefreshChannels"));
        try
        {
            var channels = await Task.Run(() =>
            {
                var dic = GameLogHelper.GetChatChanelInfos(_logPath, Math.Max(1, GameLogsSettingService.EVELogsChannelDurationValue));
                return dic.TryGetValue(name, out var list)
                    ? list.Select(p => new ChatChanelInfoItem(p)).ToList()
                    : null;
            });
            Session.UpdateChannels(channels ?? []);
        }
        finally
        {
            PageNotifyService.HideWaiting();
        }
    }

    public async Task RefreshCharactersAsync()
    {
        PageNotifyService.ShowWaiting(FindString("ChannelIntelPage_RefreshChannels"));
        try
        {
            await LoadListenerChannelDicAsync();

            foreach (var item in _listenerChannelDic)
            {
                if (Characters.FirstOrDefault(p => p.Name == item.Key) is null)
                {
                    Characters.Add(CreateListener(item.Key));
                }
            }

            // 阶段 394：刷新角色列表后同步共享会话的运行状态
            SyncRunningFromSessions();

            SelectedCharacter = null;
        }
        finally
        {
            PageNotifyService.HideWaiting();
        }
    }

    /// <summary>把当前角色的预警设置同步到其他全部角色（含每跳声音配置的深拷贝）。</summary>
    public void ApplySettingToAll()
    {
        if (Session is null)
        {
            return;
        }

        var tip = FindString("ChannelIntelPage_ApplySettingToAll_Tip");
        var result = MessageDialog.Show(
            tip,
            FindString("ChannelIntelPage_ApplySettingToAll"),
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.OK)
        {
            return;
        }

        StopAll();
        var source = Session.Setting;
        foreach (var character in Characters)
        {
            if (character.Name == Session.Listener)
            {
                continue;
            }

            var target = GetSession(character.Name).Setting;
            target.AutoUpdateLocaltion = source.AutoUpdateLocaltion;
            target.IntelJumps = source.IntelJumps;
            target.OverlapType = source.OverlapType;
            target.OverlapNotify = source.OverlapNotify;
            target.OverlapStyle = source.OverlapStyle;
            target.MakeSound = source.MakeSound;
            target.Sounds.Clear();
            foreach (var sound in source.Sounds)
            {
                target.Sounds.Add(sound.DepthClone<ChannelIntelSoundSetting>());
            }

            target.EnsureSounds();

            target.SystemNotify = source.SystemNotify;
            target.NameDbs = [.. source.NameDbs];
            target.IgnoreWords = source.IgnoreWords;
            target.ClearWords = source.ClearWords;
            target.AutoClear = source.AutoClear;
            target.AutoClearMinute = source.AutoClearMinute;
            target.AutoDowngrade = source.AutoDowngrade;
            target.AutoDowngradeMinute = source.AutoDowngradeMinute;
            target.OverlapOpacity = source.OverlapOpacity;
            target.SubZKB = source.SubZKB;
            target.KBTime = source.KBTime;
            IntelSettingService.SetValue(target);
        }

        PageNotifyService.Success(FindString("ChannelIntelPage_ApplySettingToAll_Succes"));
    }

    private void Session_ChatContentEvent(object? sender, IEnumerable<IntelChatContent> e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || e is null)
        {
            return;
        }

        // 阶段 395：可能同时接管多个角色的会话，这里只收当前选中角色的内容（避免串台）
        if (sender is ChannelIntelSession source
            && Session is not null
            && !string.Equals(source.Listener, Session.Listener, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        lock (_chatQueueLock)
        {
            foreach (var content in e)
            {
                if (_pendingChatContents.Count >= MaxPendingChatContents)
                {
                    _pendingChatContents.Dequeue();
                }

                _pendingChatContents.Enqueue(content);
            }

            if (_chatFlushScheduled)
            {
                return;
            }

            _chatFlushScheduled = true;
        }

        _ = dispatcher.BeginInvoke(FlushPendingChatContents, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void FlushPendingChatContents()
    {
        List<IntelChatContent> batch;
        lock (_chatQueueLock)
        {
            _chatFlushScheduled = false;
            if (_pendingChatContents.Count == 0)
            {
                return;
            }

            var count = Math.Min(_pendingChatContents.Count, ChatFlushBatchSize);
            batch = new List<IntelChatContent>(count);
            for (var i = 0; i < count; i++)
            {
                batch.Add(_pendingChatContents.Dequeue());
            }

            if (_pendingChatContents.Count > 0)
            {
                _chatFlushScheduled = true;
            }
        }

        foreach (var content in batch)
        {
            ChatContents.Add(content);
        }

        while (ChatContents.Count > MaxChatContents)
        {
            ChatContents.RemoveAt(0);
        }

        if (!_chatFlushScheduled)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null)
        {
            _ = dispatcher.BeginInvoke(FlushPendingChatContents, System.Windows.Threading.DispatcherPriority.Background);
        }
        else
        {
            lock (_chatQueueLock)
            {
                _chatFlushScheduled = false;
            }
        }
    }

    /// <summary>清空频道内容显示，同时丢弃尚未批量刷新的队列。</summary>
    public void ClearChatContents()
    {
        lock (_chatQueueLock)
        {
            _pendingChatContents.Clear();
        }

        ChatContents.Clear();
    }

    /// <summary>清空 ZKB 内容显示，同时丢弃尚未批量刷新的队列。</summary>
    public void ClearZkbIntelContents()
    {
        lock (_chatQueueLock)
        {
            _pendingZkbContents.Clear();
        }

        ZKBIntelContents.Clear();
    }

    private void Session_ZKBIntelEvent(object? sender, EarlyWarningContent e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || e is null)
        {
            return;
        }

        lock (_chatQueueLock)
        {
            if (_pendingZkbContents.Count >= MaxPendingZkbContents)
            {
                _pendingZkbContents.Dequeue();
            }

            _pendingZkbContents.Enqueue(e);

            if (_zkbFlushScheduled)
            {
                return;
            }

            _zkbFlushScheduled = true;
        }

        _ = dispatcher.BeginInvoke(FlushPendingZkbContents, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void FlushPendingZkbContents()
    {
        List<EarlyWarningContent> batch;
        lock (_chatQueueLock)
        {
            _zkbFlushScheduled = false;
            if (_pendingZkbContents.Count == 0)
            {
                return;
            }

            var count = Math.Min(_pendingZkbContents.Count, ZkbFlushBatchSize);
            batch = new List<EarlyWarningContent>(count);
            for (var i = 0; i < count; i++)
            {
                batch.Add(_pendingZkbContents.Dequeue());
            }

            if (_pendingZkbContents.Count > 0)
            {
                _zkbFlushScheduled = true;
            }
        }

        foreach (var content in batch)
        {
            ZKBIntelContents.Add(content);
        }

        while (ZKBIntelContents.Count > MaxZkbIntelContents)
        {
            ZKBIntelContents.RemoveAt(0);
        }

        if (!_zkbFlushScheduled)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null)
        {
            _ = dispatcher.BeginInvoke(FlushPendingZkbContents, System.Windows.Threading.DispatcherPriority.Background);
        }
        else
        {
            lock (_chatQueueLock)
            {
                _zkbFlushScheduled = false;
            }
        }
    }

    private void Session_OnZKBError(object? sender, Exception e)
    {
        Core.Log.Error(e);
        PageNotifyService.Error($"{FindString("ChannelIntelPage_StartZKBFaild")}：{e.Message}");
    }

    /// <summary>切换语言库选择（页面多选列表回调）。</summary>
    public void SetSelectedNameDbs(IEnumerable<string> dbs)
    {
        // net8 的语言版本是 C# 12，不支持 null 条件赋值（Session?.X = v）
        if (Session is not null)
        {
            Session.SelectedNameDbs = dbs.ToList();
            // 阶段 311：立即写入 IntelSettings.json，重启后保持
            Session.SaveNameDbs();
        }
    }

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

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
