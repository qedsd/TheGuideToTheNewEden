using System.Windows;
using System.Windows.Threading;

namespace TheGuideToTheNewEden.WPF.Services.CombatLog;

/// <summary>一行"日志监控命中"叠加信息。</summary>
public sealed class GameLogMonitorHitRow
{
    public required string ConfigName { get; init; }

    public required string Text { get; init; }

    public required string Detail { get; init; }
}

/// <summary>
/// 日志监控关键词命中的"最近命中"状态（阶段 357，可选叠加）。
/// <para>
/// 战斗日志提供的是分类状态；这里提供的是用户自己配置的关键词命中（例如"诱导检测"），
/// 供多开叠加层的可选开关使用。关键词命中可能很稀疏，所以保留 <see cref="TtlSeconds"/> 秒（默认 30）。
/// </para>
/// </summary>
public sealed class GameLogMonitorHitCenter
{
    public static GameLogMonitorHitCenter Current { get; } = new();

    private sealed class Hit
    {
        public DateTime Time;
        public string ConfigName = string.Empty;
        public string Content = string.Empty;
    }

    private readonly object _locker = new();
    private readonly Dictionary<string, List<Hit>> _hits = new(StringComparer.OrdinalIgnoreCase);
    private DispatcherTimer? _timer;

    private GameLogMonitorHitCenter()
    {
    }

    /// <summary>
    /// 1 秒清理定时器必须挂在 UI Dispatcher 上：本类的第一次访问可能来自日志读取线程
    /// （<c>GameLogMonitorSession.Notify</c> 在后台线程），在那条线程上创建 DispatcherTimer 不会 Tick。
    /// </summary>
    private void EnsureTimer()
    {
        if (_timer is not null)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || !dispatcher.CheckAccess())
        {
            return;
        }

        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _timer.Tick += (_, _) => Prune();
        _timer.Start();
    }

    public int TtlSeconds { get; set; } = 30;

    /// <summary>一次关键词命中（日志线程调用）。</summary>
    public void Report(string listener, string configName, string content)
    {
        if (string.IsNullOrWhiteSpace(listener) || string.IsNullOrWhiteSpace(configName))
        {
            return;
        }

        lock (_locker)
        {
            if (!_hits.TryGetValue(listener, out var list))
            {
                list = [];
                _hits[listener] = list;
            }

            list.Add(new Hit { Time = DateTime.Now, ConfigName = configName, Content = content ?? string.Empty });
            if (list.Count > 40)
            {
                list.RemoveRange(0, list.Count - 40);
            }
        }

        RaiseChanged(listener);
    }

    /// <summary>当前有效的命中行（同一配置只保留最新一条，最多 3 行）。</summary>
    public IReadOnlyList<GameLogMonitorHitRow> GetRows(string? listener)
    {
        EnsureTimer();
        if (string.IsNullOrWhiteSpace(listener))
        {
            return [];
        }

        var ttl = Math.Max(1, TtlSeconds);
        var now = DateTime.Now;
        List<Hit> snapshot;
        lock (_locker)
        {
            if (!_hits.TryGetValue(listener, out var list))
            {
                return [];
            }

            snapshot = list.Where(p => (now - p.Time).TotalSeconds <= ttl).ToList();
        }

        return snapshot
            .GroupBy(p => p.ConfigName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(p => p.Time).First())
            .OrderByDescending(p => p.Time)
            .Take(3)
            .Select(hit => new GameLogMonitorHitRow
            {
                ConfigName = hit.ConfigName,
                Text = string.Format(
                    Application.Current?.TryFindResource("GameLogMonitorHit_Row") as string ?? "{0}",
                    hit.ConfigName),
                Detail = $"{hit.ConfigName}\n{hit.Content}\n{hit.Time:HH:mm:ss}",
            })
            .ToList();
    }

    public event Action<string>? Changed;

    private void Prune()
    {
        try
        {
            var ttl = Math.Max(1, TtlSeconds);
            var now = DateTime.Now;
            List<string>? toNotify = null;
            lock (_locker)
            {
                foreach (var pair in _hits.ToList())
                {
                    var before = pair.Value.Count;
                    pair.Value.RemoveAll(p => (now - p.Time).TotalSeconds > ttl);
                    if (pair.Value.Count != before)
                    {
                        toNotify ??= [];
                        toNotify.Add(pair.Key);
                    }
                }
            }

            if (toNotify is null)
            {
                return;
            }

            foreach (var listener in toNotify)
            {
                RaiseChanged(listener);
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private void RaiseChanged(string listener)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            Changed?.Invoke(listener);
            return;
        }

        if (dispatcher.CheckAccess())
        {
            Changed?.Invoke(listener);
        }
        else
        {
            dispatcher.BeginInvoke(new Action(() => Changed?.Invoke(listener)));
        }
    }
}
