using System.Windows;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.ChannelIntel;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Services.ChannelIntel;

/// <summary>
/// 日志监控会话（对齐 WinUI 版 <c>Models/GameLogMonitor</c>）：一个"角色 + 一个日志配置"对应一个会话。
/// <list type="bullet">
///   <item>把 Core 的 <see cref="GameLogItem"/> 注册到 <see cref="Core.Services.ObservableFileService"/>（增量读日志、正则标记命中）；</item>
///   <item><b>模式 0（出现关键词时通知）</b>：命中即通知；</item>
///   <item><b>模式 1（停止出现关键词后通知）</b>：命中只记录时间，1 秒轮询发现"超过判定间隔没有再命中"才通知一次；</item>
///   <item>通知走三通道：弹窗（<see cref="GameLogMsgWindow"/>，与频道监控共用）/ 声音（复用 <see cref="SoundNotifyItem"/>）/ 托盘气泡。</item>
/// </list>
/// </summary>
public sealed class GameLogMonitorSession
{
    public GameLogInfo Info { get; }

    public GameLogItemConfig Config { get; }

    /// <summary>会话键（配置 GUID，会话内稳定）。</summary>
    public string Key => Config.GUID;

    /// <summary>实际被监控的文件（游戏日志为日志文件本身；异常日志为同名的线程日志文件）。</summary>
    public string FilePath { get; }

    private readonly GameLogItem _gameLogItem;
    private GameLogMsgWindow? _msgWindow;
    private SoundNotifyItem? _sound;
    private System.Timers.Timer? _delayTimer;
    private DateTime _lastTriggeredTime = DateTime.MaxValue;
    private readonly object _locker = new();
    private bool _running;

    /// <summary>原始内容更新（供页面着色显示）。</summary>
    public event GameLogItem.ContentUpdate? OnContentUpdate;

    public GameLogMonitorSession(GameLogInfo info, GameLogItemConfig config, string filePath)
    {
        Info = info;
        Config = config;
        FilePath = filePath;
        _gameLogItem = new GameLogItem(info, config, filePath);
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
            if (Config.WindowNotify)
            {
                _msgWindow = new GameLogMsgWindow(
                    Info.ListenerName,
                    $"{FindString("Nav.GameLogMonitor")} - {Info.ListenerName} - {Config.ConfigName}");
                _msgWindow.OnHided += (_, _) => _sound?.StopSound();
                _msgWindow.OnShowGameButtonClick += (_, _) =>
                {
                    GameWindowHelper.BringGameToFront(Info.ListenerName);
                    StopNotify();
                };
            }

            if (Config.SoundNotify)
            {
                _sound = new SoundNotifyItem(Info.ListenerName);
            }
        });

        if (Config.MonitorMode == 1)
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

            var important = contents.Where(p => p.Important).ToList();
            if (important.Count == 0)
            {
                return;
            }

            if (Config.MonitorMode == 0)
            {
                // 模式 0：命中即通知
                foreach (var content in important)
                {
                    Notify(content.SourceContent);
                }
            }
            else
            {
                // 模式 1：只刷新最后命中时间，由定时器判定"已停止触发"
                lock (_locker)
                {
                    _lastTriggeredTime = DateTime.Now;
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    /// <summary>模式 1 的判定定时器：1 秒轮询，超过判定间隔未再命中则通知一次。</summary>
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
                var expired = false;
                lock (_locker)
                {
                    if (_lastTriggeredTime != DateTime.MaxValue
                        && (DateTime.Now - _lastTriggeredTime).TotalSeconds > Config.DisappearDelay)
                    {
                        _lastTriggeredTime = DateTime.MaxValue;
                        expired = true;
                    }
                }

                if (expired)
                {
                    Notify(FindString("GameLogMonitorPage_DelayExpireTip"));
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

    /// <summary>弹窗 + 声音 + 托盘通知。</summary>
    private void Notify(string message)
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
                if (Config.WindowNotify && _msgWindow is not null)
                {
                    _msgWindow.Show(message);
                }

                if (Config.SoundNotify && _sound is not null)
                {
                    _sound.Play(new ChannelIntelSoundSetting
                    {
                        FilePath = Config.SoundFile,
                        Volume = 100,
                        Loop = Config.RepeatSound,
                    });
                }

                if (Config.SystemNotify)
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

    /// <summary>停止本次提醒（暂停声音、隐藏弹窗）；不停止监控本身。</summary>
    public void StopNotify()
    {
        var dispatcher = Application.Current?.Dispatcher;
        dispatcher?.Invoke(() =>
        {
            _sound?.StopSound();
            _msgWindow?.HideWindow();
        });
    }

    public void Stop()
    {
        _running = false;
        _delayTimer?.Stop();
        _delayTimer?.Dispose();
        _delayTimer = null;

        _gameLogItem.OnContentUpdate -= GameLogItem_OnContentUpdate;
        Core.Services.ObservableFileService.Remove(_gameLogItem);

        var dispatcher = Application.Current?.Dispatcher;
        dispatcher?.Invoke(() =>
        {
            _sound?.Dispose();
            _sound = null;
            _msgWindow?.CloseWindow();
            _msgWindow = null;
        });
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
