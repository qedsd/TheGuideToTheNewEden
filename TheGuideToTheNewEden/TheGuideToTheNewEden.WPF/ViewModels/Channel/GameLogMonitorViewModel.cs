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
/// 日志监控页：左侧角色列表（每个角色取最新一天的日志文件），中间按配置页签设置监控
/// （配置信息 / 监控模式 / 通知方式 / 监控关键词），右侧实时显示日志内容（命中关键词的行标红）。
/// <para>
/// 监控编排：一个"角色 + 一个配置"对应一个 <see cref="GameLogMonitorSession"/>；
/// 配置来源 <c>Configs/GameLogInfoSettings.json</c>（与 WinUI 共用）；文件监听走 Core 的
/// <see cref="Core.Services.ObservableFileService"/> + <see cref="GameLogItem"/>。
/// </para>
/// 与 WinUI 的有意差异：新增/删除配置与改属性都会<b>立即持久化</b>（WinUI 只在"开始监控"时落盘，
/// 新增配置后直接关窗会丢失）。
/// </summary>
public sealed class GameLogMonitorViewModel : INotifyPropertyChanged
{
    private readonly string _logPath = GameLogsSettingService.GetGamelogsPath();
    private readonly Dictionary<string, GameLogMonitorSession> _sessions = [];

    public ObservableCollection<GameLogInfo> GameLogInfos { get; } = [];

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
        if (Setting is not null)
        {
            foreach (var config in Setting.ItemConfigs.Where(IsVisibleConfig))
            {
                ItemConfigs.Add(config);
            }
        }

        SelectedItemConfig = ItemConfigs.FirstOrDefault();
    }

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

    /// <summary>日志内容更新（页面着色显示）。</summary>
    public event GameLogItem.ContentUpdate? OnContentUpdate;

    // ---------- 初始化 ----------

    public async Task InitAsync()
    {
        PageNotifyService.ShowWaiting(FindString("General_RefreshList"));
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
            RefreshRunningState();
        }
        finally
        {
            PageNotifyService.HideWaiting();
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

    /// <summary>开始监控"当前角色的当前配置"。</summary>
    public bool Start()
    {
        if (SelectedGameLogInfo is not { } info || Setting is null || SelectedItemConfig is not { } config)
        {
            PageNotifyService.Error(FindString("General_CharacterUnselected"));
            return false;
        }

        if (config.Keys is not { Count: > 0 })
        {
            PageNotifyService.Error(FindString("GameLogMonitorPage_NoneKeyError"));
            return false;
        }

        if (_sessions.ContainsKey(config.GUID))
        {
            PageNotifyService.Info(FindString("GameLogMonitorPage_Running"));
            return false;
        }

        // 目标文件：游戏日志=日志文件本身；异常日志=同名的线程日志文件（由文件名解析出日期_时间）
        var filePath = info.FilePath;
        if (config.LogType == 1)
        {
            if (!GameLogHelper.GetGameLogDateAndThreadId(info.FilePath, out var date, out var thread))
            {
                PageNotifyService.Error(string.Format(FindString("GameLogMonitorPage_ThreadFileParseFailed"), info.FilePath));
                return false;
            }

            filePath = Path.Combine(Path.GetDirectoryName(info.FilePath)!, $"{date}_{thread}.txt");
            if (!File.Exists(filePath))
            {
                PageNotifyService.Error(string.Format(FindString("GameLogMonitorPage_ThreadFileMissing"), filePath));
                return false;
            }
        }

        var session = new GameLogMonitorSession(info, config, filePath);
        if (!session.Start())
        {
            session.Stop();
            PageNotifyService.Error(FindString("GameLogMonitorPage_AddNotifyServiceFalied"));
            return false;
        }

        session.OnContentUpdate += Session_OnContentUpdate;
        _sessions[config.GUID] = session;

        info.Running = true;
        RefreshRunningState();
        SaveSetting();
        PageNotifyService.Success($"{FindString("GameLogMonitorPage_Start")}：{config.ConfigName}");
        return true;
    }

    public void StartAll()
    {
        if (Setting is null)
        {
            return;
        }

        var current = SelectedItemConfig;
        foreach (var config in ItemConfigs.ToList())
        {
            SelectedItemConfig = config;
            Start();
        }

        SelectedItemConfig = current;
    }

    /// <summary>停止"当前角色的当前配置"。</summary>
    public void Stop()
    {
        if (SelectedItemConfig is not { } config)
        {
            return;
        }

        StopSession(config.GUID);
        RefreshRunningState();
        SaveSetting();
    }

    public void StopAll()
    {
        foreach (var guid in _sessions.Keys.ToList())
        {
            StopSession(guid);
        }

        foreach (var info in GameLogInfos)
        {
            info.Running = false;
        }

        RefreshRunningState();
    }

    private void StopSession(string guid)
    {
        if (!_sessions.TryGetValue(guid, out var session))
        {
            return;
        }

        session.OnContentUpdate -= Session_OnContentUpdate;
        session.Stop();
        _sessions.Remove(guid);

        // 该角色已无任何配置在跑时清掉"监控中"
        var info = GameLogInfos.FirstOrDefault(p => p.ListenerID == session.Info.ListenerID);
        if (info is not null && !_sessions.Values.Any(p => p.Info.ListenerID == info.ListenerID))
        {
            info.Running = false;
        }
    }

    private void Session_OnContentUpdate(GameLogItem item, IEnumerable<GameLogContent> news)
    {
        var contents = news as IList<GameLogContent> ?? news.ToList();
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

            if (SelectedGameLogInfo is not null && item.Info.ListenerID == SelectedGameLogInfo.ListenerID)
            {
                OnContentUpdate?.Invoke(item, contents);
            }
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
        if (SelectedItemConfig is { } config && _sessions.TryGetValue(config.GUID, out var session))
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
    }

    /// <summary>删除配置（若在监控中先停止），并立即持久化。</summary>
    public void RemoveConfig(GameLogItemConfig config)
    {
        if (Setting is null)
        {
            return;
        }

        StopSession(config.GUID);
        Setting.ItemConfigs.Remove(config);
        ItemConfigs.Remove(config);
        SelectedItemConfig = ItemConfigs.FirstOrDefault();
        RefreshRunningState();
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
        StopAll();
    }

    private void RefreshRunningState()
    {
        Running = _sessions.Count > 0 || GameLogInfos.Any(p => p.Running);
        OnPropertyChanged(nameof(SelectedRunning));
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
