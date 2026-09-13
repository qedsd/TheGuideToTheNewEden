using System.Collections.ObjectModel;
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
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.ChannelIntel;
using TheGuideToTheNewEden.WPF.Services.Settings;

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
    private readonly Dictionary<string, ChannelIntelSession> _sessions = [];

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
    public bool SelectedRunning => SelectedCharacter?.Running ?? false;

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
        Characters = [.. _listenerChannelDic.Keys.Select(p => new ChannelIntelListener(p))];
        OnPropertyChanged(nameof(Characters));
    }

    private async Task LoadListenerChannelDicAsync()
    {
        _listenerChannelDic.Clear();
        if (!Directory.Exists(_logPath))
        {
            return;
        }

        await Task.Run(() =>
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
        if (_sessions.TryGetValue(name, out var session))
        {
            return session;
        }

        var created = new ChannelIntelSession(name, _listenerChannelDic[name], MapSolarSystems, NameDbs);
        created.WindowStopRequested += Session_WindowStopRequested;
        _sessions.Add(name, created);
        return created;
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
    }

    private async Task<bool> StartAsync(ChannelIntelSession? session, ChannelIntelListener? listener)
    {
        if (session is null || listener is null)
        {
            PageNotifyService.Error(FindString("General_CharacterUnselected"));
            return false;
        }

        PageNotifyService.ShowWaiting($"{FindString("ChannelIntelPage_Start")}：{session.Listener}");
        try
        {
            await session.Start();
            listener.Running = true;
            UnsubscribeEvents(session);
            session.ChatContentEvent += Session_ChatContentEvent;
            session.ZKBIntelEvent += Session_ZKBIntelEvent;
            session.OnZKBError += Session_OnZKBError;
            RefreshRunningState();
            OnPropertyChanged(nameof(SelectedRunning));
            OnPropertyChanged(nameof(SelectedNotRunning));
            return true;
        }
        catch (Exception ex)
        {
            IntelWarningService.Current.Remove(session.Listener);
            Core.Log.Error(ex);
            PageNotifyService.Error(ex.Message);
            return false;
        }
        finally
        {
            PageNotifyService.HideWaiting();
        }
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

    private void StopSession(ChannelIntelSession session)
    {
        UnsubscribeEvents(session);
        session.Stop();
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
        => Running = Characters.Any(p => p.Running);

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
                    Characters.Add(new ChannelIntelListener(item.Key));
                }
            }

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
        var result = MessageBox.Show(
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
        if (dispatcher is null)
        {
            return;
        }

        _ = dispatcher.BeginInvoke(() =>
        {
            foreach (var content in e)
            {
                ChatContents.Add(content);
            }
        });
    }

    private void Session_ZKBIntelEvent(object? sender, EarlyWarningContent e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        _ = dispatcher.BeginInvoke(() => ZKBIntelContents.Add(e));
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
