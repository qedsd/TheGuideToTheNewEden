using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.ChannelIntel;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.Core.Models.Map;
using TheGuideToTheNewEden.WPF.Helpers.Interop;
using TheGuideToTheNewEden.WPF.Services.GamePreview;
using TheGuideToTheNewEden.WPF.Services.Settings;
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

            // 阶段 307：全局设置 + 其它可见小窗做吸附候选
            var window = new IntelWindow(setting, intelMap, IntelGlobalSettingService.Value, () => SnapshotIntelTargets(setting.Listener));
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

        // 阶段 355：会话启动 = 该角色开始"有活跃预警"。即使没开预警小窗也要登记，
        // 否则多开预览的叠加行、设置页状态徽标都拿不到状态（自动解除也需要它）。
        ChannelIntelAlertCenter.Current.Register(setting);
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
            ChannelIntelAlertCenter.Current.Unregister(listener);
        }

        return true;
    }

    /// <summary>
    /// 运行中切换“预警声”时立即启停声音通道，不必重启整个预警会话。
    /// 原来只有 <see cref="Add"/> 在 Start 时按 MakeSound 创建一次，运行中改开关不会生效。
    /// </summary>
    public void SetSoundEnabled(string listener, bool enabled)
    {
        if (string.IsNullOrEmpty(listener))
        {
            return;
        }

        if (enabled)
        {
            if (!_sounds.ContainsKey(listener))
            {
                _sounds.Add(listener, new SoundNotifyItem(listener));
            }
        }
        else if (_sounds.TryGetValue(listener, out var item))
        {
            item.Dispose();
            _sounds.Remove(listener);
        }
    }

    public IntelWindow? GetIntelWindow(string listener)
        => _windows.TryGetValue(listener, out var window) ? window : null;

    /// <summary>阶段 307：其它可见预警小窗的物理矩形（拖动吸附候选，排除自己）。</summary>
    private IReadOnlyList<NativeMethods.RECT> SnapshotIntelTargets(string selfListener)
    {
        var targets = new List<NativeMethods.RECT>();
        foreach (var pair in _windows)
        {
            if (string.Equals(pair.Key, selfListener, StringComparison.Ordinal)
                || !pair.Value.IsVisible
                || pair.Value.WindowState == WindowState.Minimized)
            {
                continue;
            }

            var rect = pair.Value.GetRectPhysical();
            if (rect.Width > 0 && rect.Height > 0)
            {
                targets.Add(rect);
            }
        }

        return targets;
    }

    /// <summary>阶段 307：按全局设置统一所有预警小窗的尺寸。返回处理数量。</summary>
    public int ApplyUniformSize(int width, int height)
    {
        var count = 0;
        foreach (var window in _windows.Values)
        {
            window.SetUniformSize(width, height);
            count++;
        }

        return count;
    }

    /// <summary>
    /// 阶段 307：以第一个可见小窗为锚点，按全局设置的方向/对齐/间隔把其余可见小窗排开。
    /// 复用多开预览的 <see cref="PreviewLayoutCalculator"/>（物理像素坐标）。返回排列数量。
    /// </summary>
    public int ApplyAutoLayout()
    {
        var global = IntelGlobalSettingService.Value;
        var visible = new List<IntelWindow>();
        foreach (var window in _windows.Values)
        {
            if (window.IsVisible && window.WindowState != WindowState.Minimized)
            {
                visible.Add(window);
            }
        }

        if (visible.Count < 2)
        {
            return 0;
        }

        var anchor = visible[0];
        var anchorRect = anchor.GetRectPhysical();
        if (anchorRect.Width <= 0 || anchorRect.Height <= 0)
        {
            return 0;
        }

        if (!NativeMethods.TryGetMonitorWorkArea(anchor.GetHwnd(), out var workArea))
        {
            return 0;
        }

        var others = visible.Skip(1).OrderBy(w => w._setting.Listener, StringComparer.Ordinal).ToList();
        var items = others.Select(_ =>
        {
            var rect = _.GetRectPhysical();
            return new PreviewLayoutCalculator.Size(rect.Width, rect.Height);
        }).ToList();
        var positions = PreviewLayoutCalculator.Compute(
            anchorRect, items, workArea, global.LayoutMode, global.LayoutAlign, Math.Max(0, global.LayoutSpan), global.LayoutMaxPerLine);
        var count = Math.Min(others.Count, positions.Count);
        for (var i = 0; i < count; i++)
        {
            NativeMethods.MoveWindow(others[i].GetHwnd(), positions[i].X, positions[i].Y);
        }

        return count;
    }

    private readonly object _notifyLocker = new();

    public bool Notify(string listener, ChannelIntelSoundSetting? soundSetting, bool sendToast, string channel, EarlyWarningContent content)
    {
        try
        {
            lock (_notifyLocker)
            {
                // 阶段 355：星图绘制 / 弹窗显示已由 IntelWindow 订阅 ChannelIntelAlertCenter 完成，
                // 这里只负责声音与系统气泡两条通道。
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

    /// <summary>把频道原文追加到该角色的预警小窗（不要求打开预警设置页）。</summary>
    public void ChatContent(string listener, IEnumerable<IntelChatContent> contents)
    {
        var list = contents?.ToList();
        if (list is not { Count: > 0 })
        {
            return;
        }

        // 会话的文件监听在线程池回调；窗口字典与 UI 列表都必须在 UI 线程访问。
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => ChatContent(listener, list));
            return;
        }

        if (_windows.TryGetValue(listener, out var window))
        {
            window.ChatContent(list);
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
