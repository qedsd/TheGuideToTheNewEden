using System.IO;
using System.Windows;
using System.Windows.Media;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.ChannelIntel;
using TheGuideToTheNewEden.Core.Models.Map;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Services.ChannelIntel;

/// <summary>
/// 预警通知编排：一个角色绑定一个置顶预警小窗（<see cref="IntelWindow"/>）与一个声音播放器，
/// 系统通知走托盘气泡（点击气泡停止报警声音）。
/// 对齐 WinUI 版 <c>Services/WarningService</c>。全部方法要求在 UI 线程调用（会话层负责调度）。
/// </summary>
public sealed class IntelWarningService
{
    public static IntelWarningService Current { get; } = new();

    private readonly Dictionary<string, IntelWindow> _windows = [];
    private readonly Dictionary<string, SoundNotifyItem> _sounds = [];

    private IntelWarningService()
    {
        // 点击预警气泡 → 停止全部报警声音（对齐 WinUI 的 Toast 点击行为）
        NotificationService.NotificationClicked += (_, _) => StopAllSounds();
    }

    public bool Add(ChannelIntelSetting setting, IntelSolarSystemMap intelMap)
    {
        if (setting.OverlapNotify)
        {
            if (_windows.ContainsKey(setting.Listener))
            {
                Core.Log.Error("存在相同角色名称WarningWindow");
                return false;
            }

            var window = new IntelWindow(setting, intelMap);
            _windows.Add(setting.Listener, window);
            if (setting.OverlapType == 0)
            {
                window.Show();
            }
        }

        if (setting.MakeSound)
        {
            if (_sounds.ContainsKey(setting.Listener))
            {
                Core.Log.Error("存在相同角色名称SoundNotifyItem");
                return false;
            }

            _sounds.Add(setting.Listener, new SoundNotifyItem(setting.Listener));
        }

        return true;
    }

    public bool Remove(string listener)
    {
        if (!string.IsNullOrEmpty(listener))
        {
            if (_windows.TryGetValue(listener, out var window))
            {
                window.Dispose();
            }

            _windows.Remove(listener);
            if (_sounds.TryGetValue(listener, out var item))
            {
                item.Dispose();
            }

            _sounds.Remove(listener);
        }

        return true;
    }

    public IntelWindow? GetIntelWindow(string listener)
        => _windows.TryGetValue(listener, out var window) ? window : null;

    private readonly object _notifyLocker = new();

    public bool Notify(string listener, ChannelIntelSoundSetting? soundSetting, bool sendToast, string channel, EarlyWarningContent content)
    {
        try
        {
            lock (_notifyLocker)
            {
                if (_windows.TryGetValue(listener, out var window))
                {
                    window.ShowWindow();
                    window.Intel(content);
                }

                // 不对自己的发言播放声音
                if (_sounds.TryGetValue(listener, out var sound) && sound.Listener != content.SpeakerName)
                {
                    sound.Play(soundSetting);
                }

                if (sendToast)
                {
                    var earlyWarning = Application.Current?.TryFindResource("ShellPage_EarlyWarning") as string ?? "频道预警";
                    var jumps = Application.Current?.TryFindResource("EarlyWarningPage_Jumps") as string ?? "跳";
                    NotificationService.Show($"{earlyWarning}：{content.SolarSystemName} {content.Jumps}{jumps}", $"{channel}: {content.Content}");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return false;
        }
    }

    public void UpdateWindowHome(string listener, IntelSolarSystemMap intelMap)
    {
        if (_windows.TryGetValue(listener, out var window))
        {
            window.UpdateHome(intelMap);
        }
    }

    public bool RestoreWindowPos(string listener)
    {
        if (_windows.TryGetValue(listener, out var window))
        {
            window.RestoreWindowPos();
            return true;
        }

        return false;
    }

    public void StopSound(string listener)
    {
        if (!string.IsNullOrEmpty(listener) && _sounds.TryGetValue(listener, out var sound))
        {
            sound.StopSound();
        }
    }

    public void StopAllSounds()
    {
        foreach (var sound in _sounds.Values)
        {
            sound.StopSound();
        }
    }

    public void Dispose()
    {
        foreach (var window in _windows.Values)
        {
            window.Dispose();
        }

        _windows.Clear();
        foreach (var player in _sounds.Values)
        {
            player.Dispose();
        }

        _sounds.Clear();
    }
}

/// <summary>
/// 声音报警：每跳可配独立文件/音量/循环（<see cref="ChannelIntelSoundSetting"/>），
/// 未配置时播放 <c>Resources/default.mp3</c>。WPF 版用 <see cref="MediaPlayer"/>
/// （循环通过 MediaEnded 重播实现，WinRT MediaPlayer 的 IsLoopingEnabled 在 WPF 没有对应属性）。
/// 需在 UI 线程使用（MediaPlayer 为 DispatcherObject）。
/// </summary>
public sealed class SoundNotifyItem
{
    private static string DefaultFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "default.mp3");

    public string Listener { get; }

    private sealed class PlayerItem
    {
        public MediaPlayer Player { get; init; } = null!;
        public bool Loop { get; set; }
    }

    private readonly Dictionary<string, PlayerItem> _players = [];

    public SoundNotifyItem(string listener)
    {
        Listener = listener;
    }

    public void Play(ChannelIntelSoundSetting? soundSetting)
    {
        var soundFile = soundSetting is null || string.IsNullOrEmpty(soundSetting.FilePath) ? DefaultFile : soundSetting.FilePath;
        if (!File.Exists(soundFile))
        {
            soundFile = DefaultFile;
        }

        if (!_players.TryGetValue(soundFile, out var item))
        {
            var player = new MediaPlayer();
            player.Open(new Uri(soundFile));
            item = new PlayerItem { Player = player };
            player.MediaEnded += (_, _) =>
            {
                // MediaPlayer 没有 IsLoopingEnabled：循环在播完时重播
                if (item.Loop)
                {
                    player.Position = TimeSpan.Zero;
                    player.Play();
                }
            };
            _players.Add(soundFile, item);
        }

        item.Loop = soundSetting?.Loop ?? false;
        item.Player.Volume = (soundSetting?.Volume ?? 100) / 100.0;
        item.Player.Pause();
        item.Player.Position = TimeSpan.Zero;
        item.Player.Play();
    }

    public void StopSound()
    {
        foreach (var item in _players.Values)
        {
            item.Player.Pause();
            item.Player.Position = TimeSpan.Zero;
        }
    }

    public void Dispose()
    {
        foreach (var item in _players.Values)
        {
            item.Player.Stop();
            item.Player.Close();
        }

        _players.Clear();
    }
}
