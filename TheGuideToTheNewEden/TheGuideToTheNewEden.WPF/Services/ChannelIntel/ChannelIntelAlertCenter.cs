using System.Windows;
using System.Windows.Threading;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.ChannelIntel;

namespace TheGuideToTheNewEden.WPF.Services.ChannelIntel;

/// <summary>活跃预警发生的变化类型。</summary>
public enum ChannelIntelAlertChangeKind
{
    /// <summary>新预警（或在原星系上再次出现，跳数/时间被刷新）。</summary>
    Activated,

    /// <summary>被解除（解除关键词 / 自动解除 / 手动清除）。</summary>
    Cleared,

    /// <summary>被降级（自动降级），预警仍然存在。</summary>
    Downgraded,

    /// <summary>该角色全部预警被清空（清除全部 / 本地位置变化 / 会话停止）。</summary>
    ClearedAll,
}

/// <summary>一条活跃预警的只读快照。</summary>
public sealed class ChannelIntelAlertInfo
{
    public required EarlyWarningContent Content { get; init; }

    public required int SolarSystemId { get; init; }

    public required string SolarSystemName { get; init; }

    public required int Jumps { get; init; }

    /// <summary>最近一次收到（或刷新）该预警的本地时间——"多少分钟前"与自动解除都以它为准。</summary>
    public required DateTime LastSeenAt { get; init; }

    public bool Downgraded { get; init; }

    /// <summary>来源频道；ZKB 击杀为 "KB"（用于预警小窗补一行"击杀"）。</summary>
    public string? Channel { get; init; }
}

/// <summary>一次状态变化的载荷。</summary>
public sealed class ChannelIntelAlertChange
{
    public required string Listener { get; init; }

    public required ChannelIntelAlertChangeKind Kind { get; init; }

    public IReadOnlyList<ChannelIntelAlertInfo> Alerts { get; init; } = [];
}

/// <summary>
/// 频道预警的**活跃预警状态中心**（阶段 355）。
/// <para>
/// 背景：原先"哪些星系正在预警、什么时候该自动解除/降级"只存在于 <c>IntelWindow</c> 的私有字段
/// （<c>_startTimes</c> + <c>IntelMapView</c> 内部集合 + 窗内定时器）。多开预览窗需要"最近/最新预警"时既读不到，
/// 未勾"弹窗"（没有预警小窗）时这些状态更是根本不存在。
/// </para>
/// <para>
/// 因此把状态上移到这里，成为唯一状态源：
/// <list type="bullet">
/// <item>会话（<see cref="ChannelIntelSession"/>）把 Intel / Clear 写进来；</item>
/// <item>自动解除 / 自动降级由这里的 10 秒定时器统一执行（**不再依赖预警小窗是否存在**）；</item>
/// <item>预警小窗与多开预览的叠加层都只是订阅者，不再各自维护状态。</item>
/// </list>
/// </para>
/// <para>查询方法可在任意线程读取（内部加锁）；写入方法应在 UI 线程调用，非 UI 线程会被自动调度回去。</para>
/// </summary>
public sealed class ChannelIntelAlertCenter
{
    /// <summary>自动解除 / 自动降级的检查周期（与原先小窗内定时器一致）。</summary>
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(10);

    public static ChannelIntelAlertCenter Current { get; } = new();

    private sealed class ActiveAlert
    {
        public required EarlyWarningContent Content { get; init; }
        public string? Channel { get; init; }
        public bool Downgraded { get; set; }
        public DateTime LastSeenAt { get; set; }
    }

    private sealed class ListenerState
    {
        public required ChannelIntelSetting Setting { get; set; }
        public Dictionary<int, ActiveAlert> Alerts { get; } = [];
    }

    private readonly Dictionary<string, ListenerState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private DispatcherTimer? _timer;
    private bool _ticking;

    private ChannelIntelAlertCenter()
    {
    }

    /// <summary>任意活跃预警发生了变化（增 / 删 / 降级 / 清空）。</summary>
    public event EventHandler<ChannelIntelAlertChange>? Changed;

    /// <summary>在跑预警的角色集合变化（会话启动 / 停止）。</summary>
    public event EventHandler? ListenersChanged;

    /// <summary>定时心跳：用于"多少分钟前"这类随时间变化的展示刷新（10 秒一次，仅有活跃预警时触发）。</summary>
    public event EventHandler? TimeTick;

    private static Dispatcher? UiDispatcher => Application.Current?.Dispatcher;

    // ---------- 查询 ----------

    /// <summary>该角色是否有运行中的频道预警会话。</summary>
    public bool IsListening(string? listener)
    {
        if (string.IsNullOrWhiteSpace(listener))
        {
            return false;
        }

        lock (_lock)
        {
            return _states.ContainsKey(listener);
        }
    }

    /// <summary>当前活跃预警条数（含已降级的，与"必要时隐藏"的判据一致）。</summary>
    public int ActiveCount(string? listener)
    {
        if (string.IsNullOrWhiteSpace(listener))
        {
            return 0;
        }

        lock (_lock)
        {
            return _states.TryGetValue(listener, out var state) ? state.Alerts.Count : 0;
        }
    }

    /// <summary>最近一次收到的活跃预警（用于"最新"角标）。</summary>
    public ChannelIntelAlertInfo? GetLatest(string? listener)
    {
        var alerts = Snapshot(listener);
        return alerts
            .OrderByDescending(p => p.LastSeenAt)
            .ThenBy(p => p.Jumps)
            .FirstOrDefault();
    }

    /// <summary>活跃预警里跳数最小的一条（用于"最近"角标；并列时取更新的）。</summary>
    public ChannelIntelAlertInfo? GetNearest(string? listener)
    {
        var alerts = Snapshot(listener);
        return alerts
            .OrderBy(p => p.Jumps)
            .ThenByDescending(p => p.LastSeenAt)
            .FirstOrDefault();
    }

    /// <summary>当前全部活跃预警（按收到时间升序）。</summary>
    public IReadOnlyList<ChannelIntelAlertInfo> Snapshot(string? listener)
    {
        if (string.IsNullOrWhiteSpace(listener))
        {
            return [];
        }

        lock (_lock)
        {
            if (!_states.TryGetValue(listener, out var state))
            {
                return [];
            }

            return state.Alerts.Values.Select(ToInfo).ToList();
        }
    }

    /// <summary>当前有活跃预警的角色名列表（心跳只刷新这些，避免无谓重排）。</summary>
    public IReadOnlyList<string> ActiveListeners
    {
        get
        {
            lock (_lock)
            {
                return _states
                    .Where(pair => pair.Value.Alerts.Count > 0)
                    .Select(pair => pair.Key)
                    .ToList();
            }
        }
    }

    private static ChannelIntelAlertInfo ToInfo(ActiveAlert alert) => new()
    {
        Content = alert.Content,
        SolarSystemId = alert.Content.SolarSystemId,
        SolarSystemName = alert.Content.SolarSystemName,
        Jumps = alert.Content.Jumps,
        LastSeenAt = alert.LastSeenAt,
        Downgraded = alert.Downgraded,
        Channel = alert.Channel,
    };

    // ---------- 生命周期 ----------

    /// <summary>会话开始预警时登记（重复登记只更新设置引用）。</summary>
    public void Register(ChannelIntelSetting setting)
    {
        if (setting is null || string.IsNullOrWhiteSpace(setting.Listener))
        {
            return;
        }

        if (TryDispatch(() => Register(setting)))
        {
            return;
        }

        lock (_lock)
        {
            if (_states.TryGetValue(setting.Listener, out var existing))
            {
                existing.Setting = setting;
                return;
            }

            _states[setting.Listener] = new ListenerState { Setting = setting };
        }

        EnsureTimer();
        ListenersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>会话停止时注销：清空该角色全部预警并通知订阅者。</summary>
    public void Unregister(string? listener)
    {
        if (string.IsNullOrWhiteSpace(listener))
        {
            return;
        }

        if (TryDispatch(() => Unregister(listener)))
        {
            return;
        }

        bool removed;
        lock (_lock)
        {
            removed = _states.Remove(listener);
        }

        if (!removed)
        {
            return;
        }

        Changed?.Invoke(this, new ChannelIntelAlertChange
        {
            Listener = listener,
            Kind = ChannelIntelAlertChangeKind.ClearedAll,
        });
        ListenersChanged?.Invoke(this, EventArgs.Empty);
        StopTimerIfIdle();
    }

    // ---------- 写入 ----------

    /// <summary>
    /// 会话收到预警 / 解除时写入。<see cref="IntelChatType.Intel"/> 覆盖更新（同星系原地刷新），
    /// <see cref="IntelChatType.Clear"/> 删除。
    /// </summary>
    public void Report(EarlyWarningContent content, string? channel = null)
    {
        if (content is null || string.IsNullOrWhiteSpace(content.Listener))
        {
            return;
        }

        if (TryDispatch(() => Report(content, channel)))
        {
            return;
        }

        var listener = content.Listener;
        var kind = content.IntelType == IntelChatType.Clear
            ? ChannelIntelAlertChangeKind.Cleared
            : ChannelIntelAlertChangeKind.Activated;

        ChannelIntelAlertInfo? info = null;
        lock (_lock)
        {
            if (!_states.TryGetValue(listener, out var state))
            {
                // 会话未登记：宁可不记，也不要凭一条消息造出"在跑预警"的假状态（IsListening 会被骗）
                Core.Log.Warn($"频道预警：收到未登记角色的预警，已忽略（{listener}）");
                return;
            }

            var now = DateTime.Now;
            if (kind == ChannelIntelAlertChangeKind.Cleared)
            {
                if (state.Alerts.Remove(content.SolarSystemId, out var existing))
                {
                    info = ToInfo(existing);
                }
            }
            else if (state.Alerts.TryGetValue(content.SolarSystemId, out var alert))
            {
                // 同一星系重复预警：刷新跳数与时间，保持"最近 / 最新"口径
                alert.Content.Jumps = content.Jumps;
                alert.Content.SolarSystemName = content.SolarSystemName;
                alert.Content.Content = content.Content;
                alert.Content.Time = content.Time;
                alert.LastSeenAt = now;
                alert.Downgraded = false;
                info = ToInfo(alert);
            }
            else
            {
                alert = new ActiveAlert
                {
                    Content = content,
                    Channel = channel,
                    LastSeenAt = now,
                };
                state.Alerts[content.SolarSystemId] = alert;
                info = ToInfo(alert);
            }
        }

        if (info is null)
        {
            return;
        }

        Changed?.Invoke(this, new ChannelIntelAlertChange
        {
            Listener = listener,
            Kind = kind,
            Alerts = [info],
        });
        StopTimerIfIdle();
    }

    /// <summary>按星系解除（星图右键）。</summary>
    public void ClearSystems(string? listener, IEnumerable<int> systemIds)
    {
        var ids = systemIds?.Distinct().ToList();
        if (string.IsNullOrWhiteSpace(listener) || ids is not { Count: > 0 })
        {
            return;
        }

        if (TryDispatch(() => ClearSystems(listener, ids)))
        {
            return;
        }

        List<ChannelIntelAlertInfo> removed = [];
        lock (_lock)
        {
            if (!_states.TryGetValue(listener, out var state))
            {
                return;
            }

            foreach (var id in ids)
            {
                if (state.Alerts.Remove(id, out var alert))
                {
                    removed.Add(ToInfo(alert));
                }
            }
        }

        if (removed.Count == 0)
        {
            return;
        }

        Changed?.Invoke(this, new ChannelIntelAlertChange
        {
            Listener = listener,
            Kind = ChannelIntelAlertChangeKind.Cleared,
            Alerts = removed,
        });
        StopTimerIfIdle();
    }

    /// <summary>清空该角色全部预警（清除全部 / 本地位置变化）。</summary>
    public void ClearAll(string? listener)
    {
        if (string.IsNullOrWhiteSpace(listener))
        {
            return;
        }

        if (TryDispatch(() => ClearAll(listener)))
        {
            return;
        }

        int count;
        lock (_lock)
        {
            if (!_states.TryGetValue(listener, out var state))
            {
                return;
            }

            count = state.Alerts.Count;
            state.Alerts.Clear();
        }

        if (count == 0)
        {
            return;
        }

        Changed?.Invoke(this, new ChannelIntelAlertChange
        {
            Listener = listener,
            Kind = ChannelIntelAlertChangeKind.ClearedAll,
        });
        StopTimerIfIdle();
    }

    /// <summary>把指定星系降到"已降级"（自动降级）。</summary>
    public void Downgrade(string? listener, IEnumerable<int> systemIds)
    {
        var ids = systemIds?.Distinct().ToList();
        if (string.IsNullOrWhiteSpace(listener) || ids is not { Count: > 0 })
        {
            return;
        }

        if (TryDispatch(() => Downgrade(listener, ids)))
        {
            return;
        }

        List<ChannelIntelAlertInfo> changed = [];
        lock (_lock)
        {
            if (!_states.TryGetValue(listener, out var state))
            {
                return;
            }

            foreach (var id in ids)
            {
                if (state.Alerts.TryGetValue(id, out var alert) && !alert.Downgraded)
                {
                    alert.Downgraded = true;
                    changed.Add(ToInfo(alert));
                }
            }
        }

        if (changed.Count == 0)
        {
            return;
        }

        Changed?.Invoke(this, new ChannelIntelAlertChange
        {
            Listener = listener,
            Kind = ChannelIntelAlertChangeKind.Downgraded,
            Alerts = changed,
        });
    }

    // ---------- 定时器：自动解除 / 自动降级 / 分钟刷新 ----------

    private void EnsureTimer()
    {
        if (_timer is not null)
        {
            return;
        }

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TickInterval,
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void StopTimerIfIdle()
    {
        if (_timer is null)
        {
            return;
        }

        lock (_lock)
        {
            if (_states.Count > 0)
            {
                return;
            }
        }

        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_ticking)
        {
            // 本轮结果会触发 Changed，订阅方可能在回调里再触发：直接跳过重入
            return;
        }

        _ticking = true;
        try
        {
            var now = DateTime.Now;
            List<(string Listener, ChannelIntelSetting Setting, List<ActiveAlert> Alerts)> snapshot;
            lock (_lock)
            {
                snapshot = _states
                    .Select(pair => (pair.Key, pair.Value.Setting, pair.Value.Alerts.Values.ToList()))
                    .ToList();
            }

            var changes = new List<ChannelIntelAlertChange>();
            var hasAlerts = false;

            foreach (var (listener, setting, alerts) in snapshot)
            {
                if (alerts.Count == 0)
                {
                    continue;
                }

                hasAlerts = true;

                if (setting.AutoClear && setting.AutoClearMinute > 0)
                {
                    var expired = alerts
                        .Where(p => (now - p.LastSeenAt).TotalMinutes >= setting.AutoClearMinute)
                        .Select(p => p.Content.SolarSystemId)
                        .ToList();
                    if (RemoveLocked(listener, expired, out var cleared) && cleared.Count > 0)
                    {
                        changes.Add(new ChannelIntelAlertChange
                        {
                            Listener = listener,
                            Kind = ChannelIntelAlertChangeKind.Cleared,
                            Alerts = cleared,
                        });
                    }
                }

                if (setting.AutoDowngrade && setting.AutoDowngradeMinute > 0)
                {
                    var targets = alerts
                        .Where(p => !p.Downgraded
                                    && (now - p.LastSeenAt).TotalMinutes >= setting.AutoDowngradeMinute)
                        .Select(p => p.Content.SolarSystemId)
                        .ToList();
                    if (DowngradeLocked(listener, targets, out var downgraded) && downgraded.Count > 0)
                    {
                        changes.Add(new ChannelIntelAlertChange
                        {
                            Listener = listener,
                            Kind = ChannelIntelAlertChangeKind.Downgraded,
                            Alerts = downgraded,
                        });
                    }
                }
            }

            foreach (var change in changes)
            {
                Changed?.Invoke(this, change);
            }

            if (hasAlerts || changes.Count > 0)
            {
                TimeTick?.Invoke(this, EventArgs.Empty);
            }

            StopTimerIfIdle();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
        finally
        {
            _ticking = false;
        }
    }

    private bool RemoveLocked(string listener, List<int> ids, out List<ChannelIntelAlertInfo> removed)
    {
        removed = [];
        lock (_lock)
        {
            if (!_states.TryGetValue(listener, out var state))
            {
                return false;
            }

            foreach (var id in ids)
            {
                if (state.Alerts.Remove(id, out var alert))
                {
                    removed.Add(ToInfo(alert));
                }
            }
        }

        return removed.Count > 0;
    }

    private bool DowngradeLocked(string listener, List<int> ids, out List<ChannelIntelAlertInfo> downgraded)
    {
        downgraded = [];
        lock (_lock)
        {
            if (!_states.TryGetValue(listener, out var state))
            {
                return false;
            }

            foreach (var id in ids)
            {
                if (state.Alerts.TryGetValue(id, out var alert) && !alert.Downgraded)
                {
                    alert.Downgraded = true;
                    downgraded.Add(ToInfo(alert));
                }
            }
        }

        return downgraded.Count > 0;
    }

    /// <summary>非 UI 线程调用时调度回 UI 线程并返回 true（调用方应立即返回）。</summary>
    private static bool TryDispatch(Action action)
    {
        var dispatcher = UiDispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            return false;
        }

        dispatcher.BeginInvoke(action);
        return true;
    }
}
