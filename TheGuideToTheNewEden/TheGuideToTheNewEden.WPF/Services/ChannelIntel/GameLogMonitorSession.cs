using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.ChannelIntel;
using TheGuideToTheNewEden.Core.Models.CombatLog;
using TheGuideToTheNewEden.Core.Services.CombatLog;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services.CombatLog;
using TheGuideToTheNewEden.WPF.Services.Settings;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Services.ChannelIntel;

/// <summary>
/// 日志监控会话（阶段 362）：**一个角色一个会话**，多个 <see cref="GameLogItemConfig"/> 只是同一份
/// 日志上的不同关键词过滤器，共用同一个 <see cref="GameLogItem"/>、同一个窗口和一个读取线程。
/// <list type="bullet">
///   <item>一个 <see cref="GameLogItem"/>：合成配置的关键词是所有配置的并集，只负责增量读日志；</item>
///   <item>每条新日志按各配置自己的正则判断命中；模式 0 命中即通知，模式 1 只记时间由统一定时器判定；</item>
///   <item>窗口每角色一个，消息标签用配置名区分；声音按配置分别播放；</item>
///   <item>战斗日志仍由 <see cref="CombatLogFeedService"/> 单独解析，但共用同一个窗口（窗口注册表按角色合并）。</item>
/// </list>
/// </summary>
public sealed class GameLogMonitorSession
{
    public GameLogInfo Info { get; }

    public IReadOnlyList<GameLogItemConfig> Configs { get; }

    /// <summary>实际被监控的文件（游戏日志为日志文件本身）。</summary>
    public string FilePath { get; }

    public int ListenerID => Info.ListenerID;

    private sealed class ConfigState
    {
        public DateTime LastTriggeredTime = DateTime.MaxValue;
    }

    private readonly GameLogItem _gameLogItem;
    private readonly Dictionary<string, ConfigState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SoundNotifyItem> _sounds = new(StringComparer.OrdinalIgnoreCase);
    private GameLogMsgWindow? _msgWindow;
    private System.Timers.Timer? _delayTimer;
    private readonly object _locker = new();
    private bool _showShipInTitle;
    private int _windowOpacity;
    private bool _running;
    private bool _combatEnabled;
    private bool _combatAttached;

    /// <summary>原始内容更新（供页面着色显示）。</summary>
    public event GameLogItem.ContentUpdate? OnContentUpdate;

    public GameLogMonitorSession(
        GameLogInfo info,
        IReadOnlyList<GameLogItemConfig> configs,
        string filePath,
        bool showShipInTitle = true,
        int windowOpacity = 100,
        bool combatEnabled = false)
    {
        Info = info;
        Configs = configs ?? Array.Empty<GameLogItemConfig>();
        FilePath = filePath;
        _showShipInTitle = showShipInTitle;
        _windowOpacity = windowOpacity;
        _combatEnabled = combatEnabled;

        foreach (var config in Configs)
        {
            _states[config.GUID] = new ConfigState();
        }

        // 一个角色只读一份文件：把各配置的关键词并集交给 GameLogItem，只用于标记 Important；
        // 真正的"哪条命中哪个配置"在 OnContentUpdate 里按配置自己的正则判断。
        var monitorConfig = new GameLogItemConfig(0) { ConfigName = "monitor" };
        foreach (var config in Configs)
        {
            foreach (var key in config.Keys)
            {
                monitorConfig.Keys.Add(key);
            }
        }

        _gameLogItem = new GameLogItem(info, monitorConfig, filePath);
    }

    /// <summary>切换标题里的舰船显示（对已打开的窗口即时生效）。</summary>
    public void SetShowShipInTitle(bool show)
    {
        _showShipInTitle = show;
        _msgWindow?.SetShowShip(show);
    }

    /// <summary>设置窗口背板不透明度（阶段 361）。</summary>
    public void SetWindowOpacity(int opacity)
    {
        _windowOpacity = opacity;
        _msgWindow?.SetWindowOpacity(opacity);
    }

    /// <summary>开始监控；注册失败返回 false。</summary>
    public bool Start()
    {
        if (!Core.Services.ObservableFileService.Add(_gameLogItem))
        {
            return false;
        }

        _running = true;
        _gameLogItem.OnContentUpdate += GameLogItem_OnContentUpdate;

        var dispatcher = Application.Current?.Dispatcher;
        dispatcher?.Invoke(() =>
        {
            if (Configs.Any(p => p.WindowNotify) || _combatEnabled)
            {
                Func<string> titleProvider = () => Info.ListenerName;
                _msgWindow = GameLogWindowRegistry.Acquire(
                    Info.ListenerName,
                    "monitor",
                    () => new GameLogMsgWindow(
                        Info.ListenerName,
                        Info.ListenerName,
                        showShip: _showShipInTitle,
                        titleProvider: titleProvider,
                        windowOpacity: _windowOpacity,
                        settingListenerId: Info.ListenerID,
                        combatOnly: !Configs.Any(p => p.WindowNotify)));
                _msgWindow.EnableMonitor();
                _msgWindow.SetTitleProvider(titleProvider);
                _msgWindow.OnHided += (_, _) => StopSounds();
                _msgWindow.OnShowGameButtonClick += (_, _) =>
                {
                    GameWindowHelper.BringGameToFront(Info.ListenerName);
                    StopNotify();
                };
            }

            foreach (var config in Configs)
            {
                if (config.SoundNotify)
                {
                    _sounds[config.GUID] = new SoundNotifyItem(Info.ListenerName);
                }
            }
        });

        if (_combatEnabled)
        {
            CombatLogStateCenter.Current.Attach(Info.ListenerName);
            _combatAttached = true;
        }

        if (Configs.Any(p => p.MonitorMode == 1))
        {
            StartDelayTimer();
        }

        return true;
    }

    private void GameLogItem_OnContentUpdate(GameLogItem item, IEnumerable<GameLogContent> news)
    {
        try
        {
            var contents = news as IList<GameLogContent> ?? news.ToList();
            OnContentUpdate?.Invoke(item, contents);

            foreach (var content in contents)
            {
                var source = content.SourceContent;
                if (string.IsNullOrEmpty(source))
                {
                    continue;
                }

                if (_combatEnabled)
                {
                    ParseCombat(content);
                }

                foreach (var config in Configs)
                {
                    if (!Matches(config, source))
                    {
                        continue;
                    }

                    if (config.MonitorMode == 0)
                    {
                        Notify(config, source);
                    }
                    else if (_states.TryGetValue(config.GUID, out var state))
                    {
                        lock (_locker)
                        {
                            state.LastTriggeredTime = DateTime.Now;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private void ParseCombat(GameLogContent content)
    {
        try
        {
            var ev = CombatLogParser.TryParse(content, Info.ListenerName);
            if (ev is null)
            {
                return;
            }

            CombatCategoryResolver.Resolve(ev);
            CombatLogStateCenter.Current.Report(Info.ListenerName, ev);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    /// <summary>运行中切换战斗日志开关：只影响是否解析战斗行，不重启关键词监控。</summary>
    public void SetCombatEnabled(bool enabled)
    {
        if (_combatEnabled == enabled)
        {
            return;
        }

        _combatEnabled = enabled;
        if (!_running)
        {
            return;
        }

        if (enabled && !_combatAttached)
        {
            CombatLogStateCenter.Current.Attach(Info.ListenerName);
            _combatAttached = true;
        }
        else if (!enabled && _combatAttached)
        {
            CombatLogStateCenter.Current.Detach(Info.ListenerName);
            _combatAttached = false;
        }
    }
    private static bool Matches(GameLogItemConfig config, string content)
    {
        foreach (var key in config.Keys)
        {
            try
            {
                if (Regex.IsMatch(content, key.Pattern))
                {
                    return true;
                }
            }
            catch
            {
                // 单个非法正则不影响其他关键词
            }
        }

        return false;
    }

    /// <summary>模式 1 的统一判定定时器：1 秒轮询所有配置，各自超过判定间隔后通知一次。</summary>
    private void StartDelayTimer()
    {
        _delayTimer = new System.Timers.Timer
        {
            AutoReset = false,
            Interval = 1000,
        };
        _delayTimer.Elapsed += (_, _) =>
        {
            try
            {
                var now = DateTime.Now;
                foreach (var config in Configs)
                {
                    if (config.MonitorMode != 1 || !_states.TryGetValue(config.GUID, out var state))
                    {
                        continue;
                    }

                    bool expired;
                    lock (_locker)
                    {
                        expired = state.LastTriggeredTime != DateTime.MaxValue
                                  && (now - state.LastTriggeredTime).TotalSeconds > config.DisappearDelay;
                        if (expired)
                        {
                            state.LastTriggeredTime = DateTime.MaxValue;
                        }
                    }

                    if (expired)
                    {
                        Notify(config, FindString("GameLogMonitorPage_DelayExpireTip"));
                    }
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
            finally
            {
                if (_running)
                {
                    try
                    {
                        _delayTimer?.Start();
                    }
                    catch (Exception ex)
                    {
                        Core.Log.Error(ex);
                    }
                }
            }
        };
        _delayTimer.Start();
    }

    /// <summary>弹窗 + 声音 + 托盘通知（按配置自己的通知设置）。</summary>
    private void Notify(GameLogItemConfig config, string message)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (config.WindowNotify && _msgWindow is not null)
                {
                    _msgWindow.Show(message, config.ConfigName);
                }

                if (config.SoundNotify && _sounds.TryGetValue(config.GUID, out var sound))
                {
                    sound.Play(new ChannelIntelSoundSetting
                    {
                        FilePath = config.SoundFile,
                        Volume = 100,
                        Loop = config.RepeatSound,
                    });
                }

                if (config.SystemNotify)
                {
                    NotificationService.Show($"{FindString("Nav.GameLogMonitor")} - {Info.ListenerName}", message);
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        });
    }

    /// <summary>用户关闭/隐藏窗口时停掉所有声音。</summary>
    public void StopNotify()
    {
        StopSounds();
        var dispatcher = Application.Current?.Dispatcher;
        dispatcher?.Invoke(() => _msgWindow?.HideWindow());
    }

    private void StopSounds()
    {
        foreach (var sound in _sounds.Values)
        {
            sound.StopSound();
        }
    }

    public void ShowWindow() => _msgWindow?.ShowWindow();

    public void Stop()
    {
        _running = false;
        _delayTimer?.Stop();
        _delayTimer?.Dispose();
        _delayTimer = null;

        _gameLogItem.OnContentUpdate -= GameLogItem_OnContentUpdate;
        Core.Services.ObservableFileService.Remove(_gameLogItem);

        if (_combatAttached)
        {
            CombatLogStateCenter.Current.Detach(Info.ListenerName);
            _combatAttached = false;
        }

        var dispatcher = Application.Current?.Dispatcher;
        dispatcher?.Invoke(() =>
        {
            foreach (var sound in _sounds.Values)
            {
                sound.Dispose();
            }

            _sounds.Clear();
            _msgWindow?.DisableMonitor();
            GameLogWindowRegistry.Release(Info.ListenerName, "monitor");
            _msgWindow = null;
        });
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
