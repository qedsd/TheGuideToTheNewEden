using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.ChannelIntel;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Business;
using TheGuideToTheNewEden.WPF.Services.ChannelIntel;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.ViewModels.Channel;

/// <summary>
/// 频道查价页：左侧角色列表（每角色一个 <see cref="ChannelMarketSession"/>），中间勾选监控频道，
/// 右侧基本设置（触发关键词/物品分隔符）与市场星域选择。
/// 命中查价请求后由 <see cref="ChannelMarketService"/> 弹出置顶结果窗展示报价。
/// </summary>
public sealed class ChannelMarketViewModel : INotifyPropertyChanged
{
    private readonly string _logPath;
    private readonly Dictionary<string, List<ChatChanelInfoItem>> _listenerChannelDic = [];
    private readonly Dictionary<string, ChannelMarketSession> _sessions = [];

    public ObservableCollection<ChannelIntelListener> Characters { get; } = [];

    private ChannelIntelListener? _selectedCharacter;

    public ChannelIntelListener? SelectedCharacter
    {
        get => _selectedCharacter;
        set
        {
            if (Set(ref _selectedCharacter, value))
            {
                UpdateSelectedCharacter();
                OnPropertyChanged(nameof(HasSession));
                OnPropertyChanged(nameof(SelectedRunning));
                OnPropertyChanged(nameof(SelectedNotRunning));
            }
        }
    }

    public bool HasSession => SelectedCharacter is not null;

    /// <summary>选中角色正在监控（停止按钮/设置区可用性的状态源）。</summary>
    public bool SelectedRunning => SelectedCharacter?.Running ?? false;

    /// <summary>选中角色未在监控（开始按钮显隐）。与 <see cref="SelectedRunning"/> 同源计算，
    /// 两者必须在同一处一起通知——否则绑定到漏通知的那个会静默保持控件默认值（按钮常显）。</summary>
    public bool SelectedNotRunning => !SelectedRunning;

    private List<ChatChanelInfoItem> _chatChanelInfos = [];

    public List<ChatChanelInfoItem> ChatChanelInfos
    {
        get => _chatChanelInfos;
        private set => Set(ref _chatChanelInfos, value);
    }

    private ChannelMarketSession? _session;

    public ChannelMarketSession? Session
    {
        get => _session;
        private set
        {
            if (Set(ref _session, value))
            {
                OnPropertyChanged(nameof(HasSession));
                SelectedRegionName = _session is null
                    ? string.Empty
                    : Core.Services.DB.MapRegionService.Query(_session.Setting.MarketRegionID)?.RegionName ?? string.Empty;
            }
        }
    }

    private string _selectedRegionName = string.Empty;

    /// <summary>市场星域按钮上显示的名称。</summary>
    public string SelectedRegionName
    {
        get => _selectedRegionName;
        private set => Set(ref _selectedRegionName, value);
    }

    private List<MapRegion> _regions = [];

    public List<MapRegion> Regions
    {
        get => _regions;
        private set => Set(ref _regions, value);
    }

    private bool _running;

    public bool Running
    {
        get => _running;
        private set => Set(ref _running, value);
    }

    public ChannelMarketViewModel()
    {
        _logPath = GameLogsSettingService.GetChatlogsPath();
        _ = InitDicAsync();
        _ = LoadRegionsAsync();
        ChannelMarketService.Current.Start();
    }

    private async Task InitDicAsync()
    {
        var dic = new Dictionary<string, List<ChatChanelInfoItem>>();
        if (Directory.Exists(_logPath))
        {
            await Task.Run(() =>
            {
                var result = GameLogHelper.GetChatChanelInfos(_logPath, Math.Max(1, GameLogsSettingService.EVELogsChannelDurationValue));
                if (result is not null)
                {
                    foreach (var item in result)
                    {
                        dic.Add(item.Key, item.Value.Select(p => new ChatChanelInfoItem(p)).ToList());
                    }
                }
            });
        }

        Characters.Clear();
        foreach (var key in dic.Keys)
        {
            Characters.Add(new ChannelIntelListener(key));
        }

        _listenerChannelDic.Clear();
        foreach (var pair in dic)
        {
            _listenerChannelDic[pair.Key] = pair.Value;
        }
    }

    private async Task LoadRegionsAsync()
    {
        var regions = await Core.Services.DB.MapRegionService.QueryAllAsync();
        Regions = regions.OrderBy(p => p.RegionID).ToList();
    }

    private void UpdateSelectedCharacter()
    {
        if (SelectedCharacter is null)
        {
            Session = null;
            ChatChanelInfos = [];
            return;
        }

        Session = GetSession(SelectedCharacter.Name);
        ChatChanelInfos = _listenerChannelDic.TryGetValue(SelectedCharacter.Name, out var list)
            ? list
            : [];

        // 恢复上次勾选的频道（按日志文件路径）
        if (Session.Setting.Channels is { Count: > 0 })
        {
            foreach (var info in ChatChanelInfos)
            {
                info.IsChecked = Session.Setting.Channels.Contains(info.Info.FilePath);
            }
        }
        else
        {
            ChatChanelInfos.ForEach(p => p.IsChecked = false);
        }
    }

    private ChannelMarketSession GetSession(string name)
    {
        if (_sessions.TryGetValue(name, out var session))
        {
            return session;
        }

        var created = new ChannelMarketSession(name);
        _sessions.Add(name, created);
        return created;
    }

    /// <summary>选择市场星域（页面弹层回调）。</summary>
    public void SelectRegion(MapRegion region)
    {
        if (Session is null)
        {
            return;
        }

        Session.Setting.MarketRegionID = region.RegionID;
        SelectedRegionName = region.RegionName;
        Session.Save();
    }

    public async Task StartAsync()
    {
        if (Session is null || SelectedCharacter is null)
        {
            PageNotifyService.Error(FindString("General_CharacterUnselected"));
            return;
        }

        Session.SetSelectedChannels(ChatChanelInfos.Where(p => p.IsChecked).Select(p => p.Info.FilePath));
        if (Session.Setting.Channels is not { Count: > 0 })
        {
            PageNotifyService.Error(FindString("ChannelMarket_NoChannel"));
            return;
        }

        Session.Start();
        SelectedCharacter.Running = true;
        RefreshRunningState();
        OnPropertyChanged(nameof(SelectedRunning));
        OnPropertyChanged(nameof(SelectedNotRunning));
        PageNotifyService.Success(FindString("ChannelMarket_Started"));
        await Task.CompletedTask;
    }

    public async Task StartAllAsync()
    {
        foreach (var character in Characters)
        {
            var previous = SelectedCharacter;
            SelectedCharacter = character;
            await StartAsync();
            SelectedCharacter = previous;
        }
    }

    public void StopSelected()
    {
        if (Session is null || SelectedCharacter is null)
        {
            return;
        }

        StopSession(Session, SelectedCharacter);
    }

    public void StopAll()
    {
        foreach (var session in _sessions.Values)
        {
            var listener = Characters.FirstOrDefault(p => p.Name == session.Setting.CharacterName);
            StopSession(session, listener);
        }
    }

    private void StopSession(ChannelMarketSession session, ChannelIntelListener? listener)
    {
        session.Stop();
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

    public async Task RefreshChannelsAsync()
    {
        if (SelectedCharacter is null)
        {
            return;
        }

        PageNotifyService.ShowWaiting(FindString("ChannelIntelPage_RefreshChannels"));
        try
        {
            var name = SelectedCharacter.Name;
            var dic = await Task.Run(() =>
                GameLogHelper.GetChatChanelInfos(_logPath, Math.Max(1, GameLogsSettingService.EVELogsChannelDurationValue)));
            if (dic.TryGetValue(name, out var list))
            {
                _listenerChannelDic[name] = list.Select(p => new ChatChanelInfoItem(p)).ToList();
            }
            else
            {
                _listenerChannelDic.Remove(name);
            }

            UpdateSelectedCharacter();
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
            var selected = SelectedCharacter?.Name;
            await InitDicAsync();
            SelectedCharacter = selected is null ? null : Characters.FirstOrDefault(p => p.Name == selected);
        }
        finally
        {
            PageNotifyService.HideWaiting();
        }
    }

    /// <summary>把当前角色的查价设置同步到其他全部角色。</summary>
    public void ApplySettingToAll()
    {
        if (Session is null)
        {
            return;
        }

        var result = MessageBox.Show(
            FindString("ChannelMarket_ApplySettingToAll_Tip"),
            FindString("ChannelMarket_ApplySettingToAll"),
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.OK)
        {
            return;
        }

        StopAll();
        foreach (var character in Characters)
        {
            if (character.Name == Session.Setting.CharacterName)
            {
                continue;
            }

            var target = GetSession(character.Name).Setting;
            target.KeyWord = Session.Setting.KeyWord;
            target.ItemsSeparator = Session.Setting.ItemsSeparator;
            target.MarketRegionID = Session.Setting.MarketRegionID;
            target.Channels = [.. Session.Setting.Channels];
            ChannelMarketSettingService.SetValue(target);
        }

        PageNotifyService.Success(FindString("ChannelMarket_ApplySettingToAll_Succes"));
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
