using System.Windows;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Services.CombatLog;
using TheGuideToTheNewEden.WPF.Services.ChannelIntel;
using TheGuideToTheNewEden.WPF.Services.Settings;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Services.CombatLog;

/// <summary>
/// 战斗日志会话服务（阶段 357）：每个角色一份 <see cref="GameLogItem"/>，逐行喂给解析器，
/// 解析结果写进 <see cref="CombatLogStateCenter"/>；同时维护该角色的"战斗日志"小窗。
/// <para>
/// 入口在日志监控页的「战斗日志」页签：开关打开 → <see cref="Start"/>；关闭 → <see cref="Stop"/>。
/// 多开叠加层只读状态中心，不自己读日志；因此"多开战斗行"依赖日志监控的战斗开关（与频道预警同模型）。
/// </para>
/// </summary>
public sealed class CombatLogFeedService
{
    public static CombatLogFeedService Current { get; } = new();

    private sealed class Feed
    {
        public required GameLogInfo Info { get; init; }

        public required GameLogItem Item { get; init; }

        public GameLogMsgWindow? Window { get; set; }
    }

    private readonly Dictionary<string, Feed> _feeds = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _locker = new();

    /// <summary>切换标题里的舰船显示（对已打开的战斗日志窗口即时生效）。</summary>
    public void SetShowShipInTitle(long listenerId, bool show)
    {
        lock (_locker)
        {
            foreach (var feed in _feeds.Values.Where(p => p.Info.ListenerID == listenerId))
            {
                feed.Window?.SetShowShip(show);
            }
        }
    }

    /// <summary>会话启停（多开状态徽标用）。</summary>
    public event Action? RunningChanged;

    /// <summary>某角色战斗日志是否在跑（多开状态徽标用）。</summary>
    public bool IsRunning(string? listener)
    {
        if (string.IsNullOrWhiteSpace(listener))
        {
            return false;
        }

        lock (_locker)
        {
            return _feeds.ContainsKey(listener);
        }
    }

    /// <summary>在跑的角色名。</summary>
    public IReadOnlyList<string> RunningListeners
    {
        get
        {
            lock (_locker)
            {
                return _feeds.Keys.ToList();
            }
        }
    }

    /// <summary>开始（已在跑则直接显示窗口并返回 true）。</summary>
    public bool Start(GameLogInfo info)
    {
        if (info is null || string.IsNullOrWhiteSpace(info.ListenerName))
        {
            return false;
        }

        var listener = info.ListenerName;
        lock (_locker)
        {
            if (_feeds.TryGetValue(listener, out var existing))
            {
                ShowWindow(existing);
                return true;
            }
        }

        RefreshLatestFile(info);

        var item = new GameLogItem(info, new GameLogItemConfig(0) { ConfigName = "combat_log" }, info.FilePath);
        if (!Core.Services.ObservableFileService.Add(item))
        {
            Core.Log.Error($"战斗日志注册文件监控失败：{info.FilePath}");
            return false;
        }

        item.OnContentUpdate += (_, news) => OnContentUpdate(listener, news);

        var feed = new Feed { Info = info, Item = item };
        lock (_locker)
        {
            _feeds[listener] = feed;
        }

        CombatLogStateCenter.Current.Attach(listener);
        ShowWindow(feed);
        Core.Log.Info($"战斗日志已启动：{listener} ({info.FilePath})");
        RunningChanged?.Invoke();
        return true;
    }

    /// <summary>停止：摘掉文件监控、清空状态、关闭小窗。</summary>
    public void Stop(string? listener)
    {
        if (string.IsNullOrWhiteSpace(listener))
        {
            return;
        }

        Feed? feed = null;
        lock (_locker)
        {
            if (_feeds.Remove(listener, out var removed))
            {
                feed = removed;
            }
        }

        if (feed is null)
        {
            return;
        }

        Core.Services.ObservableFileService.Remove(feed.Item);
        CombatLogStateCenter.Current.Detach(listener);

        GameLogWindowRegistry.Release(listener, "combat");
        feed.Window = null;

        Core.Log.Info($"战斗日志已停止：{listener}");
        RunningChanged?.Invoke();
    }

    public void StopAll()
    {
        foreach (var listener in RunningListeners)
        {
            Stop(listener);
        }
    }

    /// <summary>打开（或重新打开）某角色的战斗日志小窗。</summary>
    public void ShowWindow(string listener)
    {
        Feed? feed;
        lock (_locker)
        {
            _feeds.TryGetValue(listener, out feed);
        }

        if (feed is not null)
        {
            ShowWindow(feed);
        }
    }

    private void ShowWindow(Feed feed)
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
                if (feed.Window is null)
                {
                    var showShip = GameLogInfoSettingService.GetValue(feed.Info.ListenerID)?.ShowShipInTitle ?? true;
                    Func<string> titleProvider = () => feed.Info.ListenerName;
                    feed.Window = GameLogWindowRegistry.Acquire(
                        feed.Info.ListenerName,
                        "combat",
                        () => new GameLogMsgWindow(feed.Info.ListenerName, feed.Info.ListenerName, combatOnly: true, showShip: showShip, titleProvider: titleProvider));
                    feed.Window.SetShowShip(showShip);
                }

                feed.Window.ShowWindow();
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        });
    }

    private static void OnContentUpdate(string listener, IEnumerable<Core.Models.EVELogs.GameLogContent> news)
    {
        try
        {
            foreach (var content in news)
            {
                if (content is null)
                {
                    continue;
                }

                var ev = CombatLogParser.TryParse(content, listener);
                if (ev is null)
                {
                    continue;
                }

                CombatCategoryResolver.Resolve(ev);
                CombatLogStateCenter.Current.Report(listener, ev);
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    /// <summary>开跑前把文件路径刷新到该角色最新一份（与日志监控一致；运行中轮换由 GameLogItem.IsReplaced 跟随）。</summary>
    private static void RefreshLatestFile(GameLogInfo info)
    {
        try
        {
            var latest = Core.Helpers.GameLogHelper.GetLatestGameLogInfos(GameLogsSettingService.GetGamelogsPath());
            var newest = latest?.FirstOrDefault(p => p.ListenerID == info.ListenerID);
            if (newest is not null)
            {
                info.FilePath = newest.FilePath;
                info.ListenerName = newest.ListenerName;
                info.StartTime = newest.StartTime;
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private static string FindString(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;
}
