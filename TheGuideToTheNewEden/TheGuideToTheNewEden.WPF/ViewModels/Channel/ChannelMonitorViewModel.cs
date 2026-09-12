using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.ChannelIntel;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.ViewModels.Channel;

/// <summary>
/// 频道监控页：左侧角色列表（<see cref="ChannelMonitorItem"/>），中间勾选要监控的频道，
/// 右侧通知方式/关键词（正则）设置与命中的频道内容。
/// 监控走 Core 的 <see cref="ChatlogObservableItem"/>（增量读日志 + 正则匹配 Important），
/// 命中后由 <see cref="ChannelMonitorNotifyService"/> 弹窗/声音/系统通知。
/// </summary>
public sealed class ChannelMonitorViewModel : INotifyPropertyChanged
{
    private readonly Dictionary<string, List<ChatChanelInfoItem>> _listenerChannelDic = [];
    private readonly Dictionary<string, List<ChatlogObservableItem>> _runningItems = [];

    public ObservableCollection<ChannelMonitorItem> Characters { get; } = [];

    private ChannelMonitorItem? _selectedCharacter;

    public ChannelMonitorItem? SelectedCharacter
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

    private bool _running;

    public bool Running
    {
        get => _running;
        private set => Set(ref _running, value);
    }

    /// <summary>命中关键词的消息（页面"频道内容"显示，仅 Important）。</summary>
    public event EventHandler<(string Name, IEnumerable<ChatContent> Contents)>? OnContentUpdate;

    public ChannelMonitorViewModel()
    {
        _ = InitDicAsync();
    }

    private async Task InitDicAsync()
    {
        var logPath = GameLogsSettingService.GetChatlogsPath();
        var dic = new Dictionary<string, List<ChatChanelInfoItem>>();
        if (Directory.Exists(logPath))
        {
            await Task.Run(() =>
            {
                var result = GameLogHelper.GetChatChanelInfos(logPath, Math.Max(1, GameLogsSettingService.EVELogsChannelDurationValue));
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
            Characters.Add(new ChannelMonitorItem { Name = key, Setting = GetSetting(key) });
        }

        _listenerChannelDic.Clear();
        foreach (var pair in dic)
        {
            _listenerChannelDic[pair.Key] = pair.Value;
        }
    }

    private static ChannelMonitorSetting GetSetting(string name)
        => ChannelMonitorSettingService.GetValue(name) ?? new ChannelMonitorSetting { Name = name };

    private void UpdateSelectedCharacter()
    {
        if (SelectedCharacter is null)
        {
            ChatChanelInfos = [];
            return;
        }

        ChatChanelInfos = _listenerChannelDic.TryGetValue(SelectedCharacter.Name, out var list)
            ? list
            : [];

        // 恢复上次勾选的频道
        if (SelectedCharacter.Setting.SelectedChannels is { Count: > 0 })
        {
            foreach (var info in ChatChanelInfos)
            {
                info.IsChecked = SelectedCharacter.Setting.SelectedChannels.Contains(info.Info.FilePath);
            }
        }
        else
        {
            ChatChanelInfos.ForEach(p => p.IsChecked = false);
        }
    }

    private async Task<bool> StartAsync(ChannelMonitorItem? item)
    {
        if (item is null)
        {
            PageNotifyService.Error(FindString("General_CharacterUnselected"));
            return false;
        }

        if (item.Setting.Keys is not { Count: > 0 })
        {
            PageNotifyService.Error(FindString("GameLogMonitorPage_NoneKeyError"));
            return false;
        }

        var checkedChannels = ChatChanelInfos.Where(p => p.IsChecked).Select(p => p.Info).ToList();
        if (checkedChannels.Count == 0)
        {
            PageNotifyService.Error(FindString("ChannelMonitorPage_NoneSelectedChannel"));
            return false;
        }

        if (!ChannelMonitorNotifyService.Current.Add(item))
        {
            PageNotifyService.Error(FindString("GameLogMonitorPage_AddNotifyServiceFalied"));
            return false;
        }

        var items = new List<ChatlogObservableItem>();
        foreach (var channel in checkedChannels)
        {
            var observableItem = new ChatlogObservableItem(channel, item);
            if (Core.Services.ObservableFileService.Add(observableItem))
            {
                observableItem.OnContentUpdate += ObservableItem_OnContentUpdate;
                items.Add(observableItem);
            }
        }

        _runningItems[item.Name] = items;
        ChannelMonitorSettingService.SetValue(item.Setting);
        item.Running = true;
        RefreshRunningState();
        OnPropertyChanged(nameof(SelectedRunning));
        OnPropertyChanged(nameof(SelectedNotRunning));
        PageNotifyService.Success($"{FindString("ChannelMonitorPage_Start")}：{item.Name}");
        await Task.CompletedTask;
        return true;
    }

    public Task StartAsync() => StartAsync(SelectedCharacter);

    public async Task StartAllAsync()
    {
        foreach (var character in Characters)
        {
            await StartAsync(character);
        }
    }

    public void StopSelected()
    {
        if (SelectedCharacter is not null)
        {
            Stop(SelectedCharacter);
        }
    }

    public void StopAll()
    {
        foreach (var character in Characters.Where(p => p.Running))
        {
            Stop(character);
        }
    }

    private void Stop(ChannelMonitorItem item)
    {
        ChannelMonitorNotifyService.Current.Stop(item.Name);
        if (_runningItems.TryGetValue(item.Name, out var items))
        {
            foreach (var observableItem in items)
            {
                observableItem.OnContentUpdate -= ObservableItem_OnContentUpdate;
            }

            Core.Services.ObservableFileService.Remove(items);
            _runningItems.Remove(item.Name);
        }

        ChannelMonitorNotifyService.Current.Remove(item.Name);
        item.Running = false;
        RefreshRunningState();
        OnPropertyChanged(nameof(SelectedRunning));
        OnPropertyChanged(nameof(SelectedNotRunning));
    }

    public async Task RefreshListAsync()
    {
        PageNotifyService.ShowWaiting(FindString("ChannelMonitorPage_RefreshList"));
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

    public void PickSoundFile()
    {
        if (SelectedCharacter is null)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Audio|*.mp3;*.wav;*.wma;*.flac|All|*.*",
        };
        if (dialog.ShowDialog() == true)
        {
            SelectedCharacter.Setting.SoundFile = dialog.FileName;
        }
    }

    public void AddKey()
        => SelectedCharacter?.Setting.Keys.Add(new ChannelMonitorKey(".*"));

    public void DeleteKey(ChannelMonitorKey key)
        => SelectedCharacter?.Setting.Keys.Remove(key);

    public void RefreshChannelList()
    {
        if (SelectedCharacter is null)
        {
            return;
        }

        if (_listenerChannelDic.TryGetValue(SelectedCharacter.Name, out var list))
        {
            ChatChanelInfos = list;
            UpdateSelectedCharacter();
        }
        else
        {
            ChatChanelInfos = [];
        }
    }

    private void ObservableItem_OnContentUpdate(ChatlogObservableItem sender, IEnumerable<ChatContent> news)
    {
        var name = sender.ChannelMonitorItem?.Name ?? string.Empty;
        var important = news.Where(p => p.Important).ToList();
        if (important.Count > 0)
        {
            OnContentUpdate?.Invoke(this, (name, important));
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is not null)
            {
                _ = dispatcher.BeginInvoke(() =>
                {
                    var item = Characters.FirstOrDefault(p => p.Name == name);
                    if (item is not null)
                    {
                        foreach (var msg in important)
                        {
                            ChannelMonitorNotifyService.Current.Notify(item, msg.SourceContent);
                        }
                    }
                });
            }
        }
    }

    private void RefreshRunningState()
        => Running = Characters.Any(p => p.Running);

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
