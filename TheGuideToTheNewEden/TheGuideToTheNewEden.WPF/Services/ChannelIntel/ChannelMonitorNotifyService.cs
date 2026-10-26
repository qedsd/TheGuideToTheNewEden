using System.IO;
using System.Windows;
using System.Windows.Media;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Services.ChannelIntel;

/// <summary>
/// 频道监控的通知编排（对齐 WinUI 版 <c>Services/ChannelMonitorNotifyService</c>）：
/// 一个角色一个置顶消息弹窗（<see cref="GameLogMsgWindow"/>）+ 一个提示音播放器；系统通知走托盘气泡。
/// 命中弹窗"前置游戏"按钮或气泡被点击时，切到对应 EVE 客户端窗口并停止提醒。
/// 全部方法要求在 UI 线程调用。
/// </summary>
public sealed class ChannelMonitorNotifyService
{
    public static ChannelMonitorNotifyService Current { get; } = new();

    private readonly Dictionary<string, GameLogMsgWindow> _windows = [];
    private readonly Dictionary<string, MediaPlayer> _players = [];

    public bool Add(ChannelMonitorItem item)
    {
        if (item.Setting.WindowNotify)
        {
            if (_windows.ContainsKey(item.Name))
            {
                Core.Log.Error("存在相同角色名称的监控弹窗");
                return false;
            }

            var window = new GameLogMsgWindow(item.Name);
            window.OnHided += (_, _) =>
            {
                Stop(item.Name);
            };
            window.OnShowGameButtonClick += (_, _) =>
            {
                GameWindowHelper.BringGameToFront(item.Name);
                Stop(item.Name);
            };
            _windows.Add(item.Name, window);
        }

        if (item.Setting.SoundNotify)
        {
            if (_players.ContainsKey(item.Name))
            {
                Core.Log.Error("存在相同角色名称的监控声音播放器");
                return false;
            }

            var player = new MediaPlayer();
            var soundFile = string.IsNullOrEmpty(item.Setting.SoundFile)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "default.mp3")
                : item.Setting.SoundFile;
            if (!File.Exists(soundFile))
            {
                soundFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "default.mp3");
            }

            player.Open(new Uri(soundFile));
            player.MediaEnded += (_, _) =>
            {
                // RepeatSound = 循环播放
                if (item.Setting.RepeatSound)
                {
                    player.Position = TimeSpan.Zero;
                    player.Play();
                }
            };
            _players.Add(item.Name, player);
        }

        return true;
    }

    /// <summary>推送一条命中消息：弹窗 + 声音 + 系统通知。</summary>
    public void Notify(ChannelMonitorItem item, string content)
    {
        try
        {
            if (_windows.TryGetValue(item.Name, out var window))
            {
                window.Show(content);
            }

            if (_players.TryGetValue(item.Name, out var player))
            {
                player.Volume = 1.0;
                player.Pause();
                player.Position = TimeSpan.Zero;
                player.Play();
            }

            if (item.Setting.SystemNotify)
            {
                var title = $"{FindString("ChannelMonitorPage")} - {item.Name}";
                NotificationService.Show(title, content);
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    /// <summary>停止提醒：隐藏弹窗、暂停声音（不清除配置）。</summary>
    public void Stop(string name)
    {
        if (_windows.TryGetValue(name, out var window))
        {
            window.HideWindow();
        }

        if (_players.TryGetValue(name, out var player))
        {
            player.Pause();
            player.Position = TimeSpan.Zero;
        }
    }

    /// <summary>移除角色的通知资源（停止监控时调用）。</summary>
    public void Remove(string name)
    {
        if (_windows.TryGetValue(name, out var window))
        {
            window.Clear();
            window.CloseWindow();
            _windows.Remove(name);
        }

        if (_players.TryGetValue(name, out var player))
        {
            player.Stop();
            player.Close();
            _players.Remove(name);
        }
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
