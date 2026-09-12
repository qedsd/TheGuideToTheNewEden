using System.IO;
using System.Text;
using System.Windows;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.ChannelIntel;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.Core.Models.Map;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Services.ChannelIntel;

/// <summary>
/// 一个角色的频道预警会话（对齐 WinUI 版 <c>Models/ChannelIntel</c>）：
/// 持有该角色的预警设置与勾选频道；Start 时构建 N 跳星系图、注册预警小窗/声音、
/// 为每个勾选频道创建 Core 的 <see cref="ChannelIntelObserver"/> 并挂到
/// <see cref="Core.Services.ObservableFileService"/>（300ms 轮询增量读聊天日志）；
/// 可选自动定位（本地频道）、ZKB 击杀预警。
/// Observer 事件来自后台线程，本类负责把需要 UI 的部分调度回主线程。
/// </summary>
public sealed class ChannelIntelSession
{
    private readonly List<MapSolarSystemBase> _mapSolarSystems;
    private readonly List<string> _nameDbs;
    private IntelSolarSystemMap _intelMap = null!;
    private readonly List<ChannelIntelObserver> _observers = [];
    private ChannelIntelObserver? _localObserver;
    private Core.Intel.ZKBIntel? _zkbIntel;

    private readonly MapSolarSystemBase _nullSolarSystem = new()
    {
        SolarSystemID = -1,
        SolarSystemName = string.Empty,
    };

    private List<ChatChanelInfoItem> _chatChanelInfos;

    /// <summary>当前角色所有聊天频道（每个频道是该频道最新一次会话的日志文件）。</summary>
    public List<ChatChanelInfoItem> ChatChanelInfos
    {
        get => _chatChanelInfos;
        set => _chatChanelInfos = value;
    }

    public string Listener { get; }

    public ChannelIntelSetting Setting { get; }

    private List<string> _selectedNameDbs = [];

    /// <summary>预警星系名语言库（第一项 "default(en)" 使用主库）。</summary>
    public List<string> SelectedNameDbs
    {
        get => _selectedNameDbs;
        set => _selectedNameDbs = value ?? [];
    }

    private MapSolarSystemBase _localSolarSystem = null!;

    /// <summary>角色当前所在星系（预警中心）。</summary>
    public MapSolarSystemBase LocalSolarSystem
    {
        get => _localSolarSystem;
        set => _localSolarSystem = value;
    }

    /// <summary>星系搜索选择：设置 LocationID 并同步本地位置（页面选择器调用）。</summary>
    public void SetSearchSolarSystem(MapSolarSystem system)
    {
        LocalSolarSystem = system;
        Setting.LocationID = system.SolarSystemID;
    }

    public bool Running { get; private set; }

    /// <summary>原始频道消息（页面"频道内容"页签显示）。</summary>
    public event EventHandler<IEnumerable<IntelChatContent>>? ChatContentEvent;

    /// <summary>ZKB 击杀预警（页面显示）。</summary>
    public event EventHandler<EarlyWarningContent>? ZKBIntelEvent;

    /// <summary>ZKB 预警错误。</summary>
    public event EventHandler<Exception>? OnZKBError;

    /// <summary>预警小窗上的"停止预警"按钮被点击（VM 据此复位状态）。</summary>
    public event EventHandler? WindowStopRequested;

    public ChannelIntelSession(string listener, List<ChatChanelInfoItem> chatChanelInfos, List<MapSolarSystemBase> mapSolarSystems, List<string> nameDbs)
    {
        Listener = listener;
        _chatChanelInfos = chatChanelInfos;
        _mapSolarSystems = mapSolarSystems;
        _nameDbs = nameDbs;
        var setting = IntelSettingService.GetValue(listener);
        if (setting is null)
        {
            setting = new ChannelIntelSetting
            {
                Listener = listener,
            };
        }

        Setting = setting;
        LoadSetting();
        Setting.PropertyChanged += Setting_PropertyChanged;
    }

    private void LoadSetting()
    {
        if (Setting.ChannelIDs is { Count: > 0 })
        {
            foreach (var id in Setting.ChannelIDs)
            {
                var target = ChatChanelInfos.FirstOrDefault(p => p.ChannelID == id);
                if (target is not null)
                {
                    target.IsChecked = true;
                }
            }
        }
        else
        {
            ChatChanelInfos.ForEach(p => p.IsChecked = false);
        }

        LocalSolarSystem = Setting.LocationID > 1
            ? _mapSolarSystems.FirstOrDefault(p => p.SolarSystemID == Setting.LocationID) ?? _nullSolarSystem
            : _nullSolarSystem;

        if (Setting.NameDbs is { Count: > 0 })
        {
            SelectedNameDbs = _nameDbs.Where(db => Setting.NameDbs.Contains(db)).ToList();
        }
        else
        {
            SelectedNameDbs = [_nameDbs.FirstOrDefault() ?? string.Empty];
        }

        if (Setting.AutoUpdateLocaltion)
        {
            _ = UpdateCharacterLocation();
        }
    }

    private void SaveSetting()
    {
        Setting.ChannelIDs = ChatChanelInfos.Where(p => p.IsChecked).Select(p => p.ChannelID).ToList();
        Setting.NameDbs = SelectedNameDbs.ToList();
        IntelSettingService.SetValue(Setting);
    }

    private void Setting_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(EarlyWarningSetting.AutoUpdateLocaltion):
                // 勾选自动更新位置时立即解析当前位置
                if (Setting.AutoUpdateLocaltion)
                {
                    _ = UpdateCharacterLocation();
                }

                break;
            case nameof(EarlyWarningSetting.IntelJumps):
            {
                // 跳数变化时同步增删每跳的声音配置
                if (!double.IsNaN(Setting.IntelJumps))
                {
                    var diff = (int)(Setting.IntelJumps - Setting.Sounds.Count + 1);
                    if (diff < 0)
                    {
                        for (var i = 0; i < -diff; i++)
                        {
                            Setting.Sounds.RemoveAt(Setting.Sounds.Count - 1);
                        }
                    }
                    else
                    {
                        for (var i = 0; i < diff; i++)
                        {
                            Setting.Sounds.Add(new ChannelIntelSoundSetting
                            {
                                Id = Setting.Sounds.Count,
                            });
                        }
                    }
                }

                break;
            }
        }
    }

    /// <summary>从本地频道的最近消息解析角色当前所在星系。</summary>
    private async Task UpdateCharacterLocation()
    {
        var localChat = ChatChanelInfos.FirstOrDefault(p => p.ChannelID == "local");
        if (localChat is null)
        {
            return;
        }

        List<string> newLines;
        using (var fs = new FileStream(localChat.Info.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            var buffer = new byte[1024];
            var builder = new StringBuilder();
            int read;
            while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
            {
                builder.Append(Encoding.Unicode.GetString(buffer, 0, read));
            }

            newLines = builder.Length > 0 ? builder.ToString().Split(['\n', '\r']).ToList() : [];
        }

        if (newLines.Count == 0)
        {
            return;
        }

        var systemId = -1;
        foreach (var line in newLines)
        {
            var chatContent = IntelChatContent.Create(line);
            if (chatContent is null)
            {
                continue;
            }

            var id = await Core.EVEHelpers.ChatLogHelper.TryGetCharacterLocationAsync(chatContent, _nameDbs);
            if (id != -1)
            {
                systemId = id;
            }
        }

        if (systemId != -1)
        {
            LocalSolarSystem = _mapSolarSystems.FirstOrDefault(p => p.SolarSystemID == systemId) ?? _nullSolarSystem;
            Setting.LocationID = systemId;
        }
    }

    private async Task<Dictionary<string, int>> GetSolarSystemNames()
    {
        var names = new Dictionary<string, int>();
        foreach (var item in SelectedNameDbs)
        {
            // 第一项 default(en) 使用主库
            var dbPath = item == _nameDbs.FirstOrDefault() ? Core.Config.DBPath : item;
            var result = await Core.Services.DB.MapSolarSystemNameService.QueryAllAsync(dbPath);
            if (result is { Count: > 0 })
            {
                foreach (var solar in result)
                {
                    names.TryAdd(solar.SolarSystemName, solar.SolarSystemID);
                }
            }
        }

        return names;
    }

    public void UpdateChannels(List<ChatChanelInfoItem> chatChanelInfos)
    {
        ChatChanelInfos = chatChanelInfos;
        if (Setting.ChannelIDs is { Count: > 0 })
        {
            foreach (var id in Setting.ChannelIDs)
            {
                var target = ChatChanelInfos.FirstOrDefault(p => p.ChannelID == id);
                if (target is not null)
                {
                    target.IsChecked = true;
                }
            }
        }
        else
        {
            ChatChanelInfos.ForEach(p => p.IsChecked = false);
        }
    }

    public async Task Start()
    {
        if (Setting.LocationID <= 0)
        {
            throw new Exception($"{Listener}: {FindString("ChannelIntelPage_Error_NoLocaction")}");
        }

        if (LocalSolarSystem.IsSpecial())
        {
            throw new Exception($"{Listener}: {FindString("ChannelIntelPage_Error_SystemNotSupport")}");
        }

        if (ChatChanelInfos is not { Count: > 0 })
        {
            return;
        }

        _observers.Clear();
        var checkedItems = ChatChanelInfos.Where(p => p.IsChecked).ToList();
        if (checkedItems.Count == 0)
        {
            throw new Exception($"{Listener}: {FindString("ChannelIntelPage_Error_UnselectedChatChanel")}");
        }

        _intelMap = await Core.EVEHelpers.SolarSystemPosHelper.GetIntelSolarSystemMapAsync(Setting.LocationID, Setting.IntelJumps);
        Core.EVEHelpers.SolarSystemPosHelper.ResetXY(_intelMap.GetAllSolarSystem());
        if (!IntelWarningService.Current.Add(Setting, _intelMap))
        {
            throw new Exception(FindString("ChannelIntelPage_Error_WarningServiceError"));
        }

        foreach (var ch in checkedItems)
        {
            var observer = new ChannelIntelObserver(ch.Info, Setting)
            {
                IntelMap = _intelMap,
            };
            if (Core.Services.ObservableFileService.Add(observer))
            {
                observer.SolarSystemNames = await GetSolarSystemNames();
                observer.OnContentUpdate += Observer_OnContentUpdate;
                observer.OnWarningUpdate += Observer_OnWarningUpdate;
                _observers.Add(observer);

                if (Setting.AutoUpdateLocaltion && ch.ChannelID == "local")
                {
                    observer.OnContentUpdate += Observer_LocalChanged;
                    _localObserver = observer;
                }
            }
        }

        if (Setting.AutoUpdateLocaltion && _localObserver is null)
        {
            // 自动定位需要监控本地频道，即使它不在预警频道里
            var localChat = ChatChanelInfos.FirstOrDefault(p => p.ChannelID == "local");
            if (localChat is not null)
            {
                var observer = new ChannelIntelObserver(localChat.Info, Setting);
                if (Core.Services.ObservableFileService.Add(observer))
                {
                    observer.SolarSystemNames = await GetSolarSystemNames();
                    observer.OnContentUpdate += Observer_LocalChanged;
                    _observers.Add(observer);
                    _localObserver = observer;
                }
            }
        }

        if (Setting.SubZKB)
        {
            _zkbIntel = new Core.Intel.ZKBIntel(Setting, _intelMap);
            if (await _zkbIntel.Start())
            {
                _zkbIntel.OnError += ZKBIntel_OnError;
                _zkbIntel.OnWarningUpdate += ZkbIntel_OnWarningUpdate;
            }
            else
            {
                throw new Exception(FindString("ChannelIntelPage_StartZKBFaild"));
            }
        }

        var intelWindow = IntelWarningService.Current.GetIntelWindow(Setting.Listener);
        if (intelWindow is not null)
        {
            intelWindow.StopRequested += IntelWindow_StopRequested;
        }

        Running = true;
        SaveSetting();
    }

    private void ZKBIntel_OnError(object? sender, Exception e)
        => OnZKBError?.Invoke(this, e);

    private void Observer_OnWarningUpdate(ChannelIntelObserver observer, IEnumerable<EarlyWarningContent> news)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        _ = dispatcher.BeginInvoke(() =>
        {
            foreach (var ch in news)
            {
                if (ch.IntelType == IntelChatType.Intel)
                {
                    ChannelIntelSoundSetting? soundSetting = null;
                    if (Setting.Sounds.Count >= ch.Jumps)
                    {
                        soundSetting = Setting.Sounds[ch.Jumps];
                    }

                    IntelWarningService.Current.Notify(observer.ChatChanelInfo.Listener, soundSetting, Setting.SystemNotify, observer.ChatChanelInfo.ChannelName, ch);
                }
                else
                {
                    // Clear：只更新预警小窗，不播声音
                    IntelWarningService.Current.GetIntelWindow(observer.ChatChanelInfo.Listener)?.Intel(ch);
                }
            }
        });
    }

    private void Observer_OnContentUpdate(ChannelIntelObserver observer, IEnumerable<IntelChatContent> news)
        => ChatContentEvent?.Invoke(this, news);

    private void Observer_LocalChanged(ChannelIntelObserver observer, IEnumerable<IntelChatContent> news)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            foreach (var content in news.Reverse())// 从后往前找，新消息位于后面
            {
                var id = await Core.EVEHelpers.ChatLogHelper.TryGetCharacterLocationAsync(content, _nameDbs);
                if (id > 0)
                {
                    await dispatcher.InvokeAsync(async () =>
                    {
                        Setting.LocationID = id;
                        _intelMap = await Core.EVEHelpers.SolarSystemPosHelper.GetIntelSolarSystemMapAsync(Setting.LocationID, Setting.IntelJumps);
                        Core.EVEHelpers.SolarSystemPosHelper.ResetXY(_intelMap.GetAllSolarSystem());
                        foreach (var item in _observers)
                        {
                            item.IntelMap = _intelMap;
                        }

                        IntelWarningService.Current.UpdateWindowHome(Setting.Listener, _intelMap);
                    });
                    break;
                }
            }
        });
    }

    private void ZkbIntel_OnWarningUpdate(object? sender, EarlyWarningContent e)
    {
        ZKBIntelEvent?.Invoke(this, e);
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        _ = dispatcher.BeginInvoke(() =>
        {
            var span = DateTime.UtcNow - e.Time;
            var desc = span.TotalMinutes > 1
                ? $" {span.TotalMinutes:N1}{FindString("EarlyWarningPage_Befor_Min")}"
                : $" {span.TotalSeconds:N0}{FindString("EarlyWarningPage_Befor_Sec")}";
            e.Content += desc;
            ChannelIntelSoundSetting? soundSetting = null;
            if (Setting.Sounds.Count >= e.Jumps)
            {
                soundSetting = Setting.Sounds[e.Jumps];
            }

            IntelWarningService.Current.Notify((sender as Core.Intel.ZKBIntel)?.GetListener() ?? Listener, soundSetting, Setting.SystemNotify, "KB", e);
        });
    }

    private void IntelWindow_StopRequested(object? sender, EventArgs e)
        => WindowStopRequested?.Invoke(this, EventArgs.Empty);

    public void Stop()
    {
        Core.Services.ObservableFileService.Remove(_observers);
        _observers.Clear();
        Running = false;
        _localObserver = null;
        if (_zkbIntel is not null)
        {
            _zkbIntel.OnError -= ZKBIntel_OnError;
            _zkbIntel.OnWarningUpdate -= ZkbIntel_OnWarningUpdate;
            _zkbIntel.Stop();
            _zkbIntel = null;
        }

        IntelWarningService.Current.Remove(Setting?.Listener);
    }

    public void StopSound()
    {
        if (!string.IsNullOrEmpty(Setting?.Listener))
        {
            IntelWarningService.Current.StopSound(Setting.Listener);
        }
    }

    public bool RestorePos()
        => !string.IsNullOrEmpty(Setting?.Listener) && IntelWarningService.Current.RestoreWindowPos(Setting.Listener);

    public List<ChannelIntelObserver> GetObservers() => _observers;

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
