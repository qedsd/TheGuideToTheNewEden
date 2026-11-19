using System.Windows;
using System.Windows.Threading;
using TheGuideToTheNewEden.Core.Models.CombatLog;
using TheGuideToTheNewEden.Core.Services.CombatLog;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Services.CombatLog;

/// <summary>一行战斗叠加快照（分类 + 最新单次值 + 窗口值 + 颜色/详情）。</summary>
public sealed class CombatLogRow
{
    public required string Category { get; init; }

    public required string Text { get; init; }

    public required string ColorKey { get; init; }

    public required int Order { get; init; }

    public string? Detail { get; init; }

    public double? LastValue { get; init; }

    public double? WindowValue { get; init; }

    public double? Dps { get; init; }
}

/// <summary>
/// 战斗日志状态中心（阶段 357）：每个角色一份"分类 → 最近事件"状态。
/// <list type="bullet">
///   <item>只保留"进"向事件（别人打你/修你/干扰你）；出向直接丢弃。</item>
///   <item>5 秒窗口（可配 5/8/10）：同类新事件立即刷新；窗口内没有事件就整行消失。</item>
///   <item>数值类显示"最新单次 + 窗口值"；承伤第二段是 DPS（窗口值 ÷ 窗口秒数）。</item>
///   <item>由日志监控的"战斗日志"开关驱动 <see cref="Attach"/> / <see cref="Detach"/>；
///         多开叠加层与日志监控小窗都只读这里。</item>
/// </list>
/// </summary>
public sealed class CombatLogStateCenter
{
    public static CombatLogStateCenter Current { get; } = new();

    private sealed class CategoryState
    {
        public readonly List<(DateTime Time, double Value)> Window = [];
        public double? LastValue;
        public DateTime LastSeen = DateTime.MinValue;
        public CombatLogEvent? LastEvent;
    }

    private readonly object _locker = new();
    private readonly Dictionary<string, Dictionary<string, CategoryState>> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _attached = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _enabledCategories = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pendingNotify = new(StringComparer.OrdinalIgnoreCase);
    private DispatcherTimer? _timer;

    private CombatLogStateCenter()
    {
        foreach (var definition in CombatCategories.All.Where(p => p.DefaultEnabled))
        {
            _enabledCategories.Add(definition.Key);
        }
    }

    /// <summary>1 秒心跳挂到 UI Dispatcher（首次查询/上报可能来自后台日志线程）。</summary>
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
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    /// <summary>窗口/过期秒数（由日志监控"战斗日志"设置同步）。</summary>
    public int TtlSeconds { get; set; } = 5;

    public bool IsAttached(string? listener)
        => !string.IsNullOrWhiteSpace(listener) && _attached.Contains(listener);

    public IReadOnlyCollection<string> AttachedListeners
    {
        get
        {
            lock (_locker)
            {
                return _attached.ToList();
            }
        }
    }

    /// <summary>某分类是否启用（日志监控页的分类裁剪）。</summary>
    public bool IsCategoryEnabled(string category)
    {
        lock (_locker)
        {
            return _enabledCategories.Contains(category);
        }
    }

    public void SetCategoryEnabled(string category, bool enabled)
    {
        lock (_locker)
        {
            if (enabled)
            {
                _enabledCategories.Add(category);
            }
            else
            {
                _enabledCategories.Remove(category);
            }
        }

        NotifyAll();
    }

    public IReadOnlyList<string> EnabledCategories
    {
        get
        {
            lock (_locker)
            {
                return _enabledCategories.ToList();
            }
        }
    }

    /// <summary>启用分类（从设置恢复）。</summary>
    public void SetEnabledCategories(IEnumerable<string> categories)
    {
        lock (_locker)
        {
            _enabledCategories.Clear();
            foreach (var category in categories)
            {
                _enabledCategories.Add(category);
            }
        }

        NotifyAll();
    }

    /// <summary>日志监控战斗开关打开时挂上角色（重复调用无副作用）。</summary>
    public void Attach(string listener)
    {
        if (string.IsNullOrWhiteSpace(listener))
        {
            return;
        }

        lock (_locker)
        {
            if (!_attached.Add(listener))
            {
                return;
            }

            _states[listener] = new Dictionary<string, CategoryState>(StringComparer.OrdinalIgnoreCase);
        }

        RaiseChanged(listener);
    }

    /// <summary>日志监控战斗开关关闭时摘掉角色并清空数据。</summary>
    public void Detach(string listener)
    {
        if (string.IsNullOrWhiteSpace(listener))
        {
            return;
        }

        lock (_locker)
        {
            _attached.Remove(listener);
            _states.Remove(listener);
        }

        RaiseChanged(listener);
    }

    /// <summary>日志线程调用：记录一条已分类的事件。</summary>
    public void Report(string listener, CombatLogEvent ev)
    {
        if (string.IsNullOrWhiteSpace(listener) || ev?.CategoryKey is null)
        {
            return;
        }

        // 出向（我去打/修别人）不显示；方向不明时只对"攻击类"宽容按进处理
        if (ev.Direction == CombatEventDirection.Outgoing)
        {
            return;
        }

        if (ev.Direction == CombatEventDirection.Unknown
            && ev.Kind is CombatEventKind.RemoteRepair or CombatEventKind.RemoteCapacitor)
        {
            return;
        }

        lock (_locker)
        {
            if (!_attached.Contains(listener))
            {
                return;
            }

            if (!_states.TryGetValue(listener, out var categories))
            {
                categories = new Dictionary<string, CategoryState>(StringComparer.OrdinalIgnoreCase);
                _states[listener] = categories;
            }

            if (!categories.TryGetValue(ev.CategoryKey, out var state))
            {
                state = new CategoryState();
                categories[ev.CategoryKey] = state;
            }

            var now = DateTime.Now;
            state.LastSeen = now;
            state.LastEvent = ev;
            if (ev.Amount.HasValue)
            {
                state.LastValue = ev.Amount;
                state.Window.Add((now, Math.Abs(ev.Amount.Value)));
            }
        }

        RaiseChanged(listener);
    }

    /// <summary>清空某角色的所有战斗行（停监控时）。</summary>
    public void Clear(string listener)
    {
        lock (_locker)
        {
            _states.Remove(listener);
        }

        RaiseChanged(listener);
    }

    /// <summary>当前活跃行（已按优先级排序）；无事件返回空。</summary>
    public IReadOnlyList<CombatLogRow> GetRows(string? listener)
    {
        EnsureTimer();
        if (string.IsNullOrWhiteSpace(listener))
        {
            return [];
        }

        var ttl = Math.Max(1, TtlSeconds);
        var now = DateTime.Now;
        var rows = new List<CombatLogRow>();

        lock (_locker)
        {
            if (!_states.TryGetValue(listener, out var categories))
            {
                return rows;
            }

            foreach (var pair in categories)
            {
                var definition = CombatCategories.Find(pair.Key);
                if (definition is null || !_enabledCategories.Contains(pair.Key))
                {
                    continue;
                }

                var state = pair.Value;
                state.Window.RemoveAll(p => (now - p.Time).TotalSeconds > ttl);
                var hasWindow = state.Window.Count > 0;
                var hasLast = (now - state.LastSeen).TotalSeconds <= ttl;
                if (!hasWindow && !hasLast)
                {
                    continue;
                }

                var label = LanguageService.Get(CategoryLabelKey(pair.Key));
                string text;
                if (definition.HasValue)
                {
                    var unit = definition.Unit ?? string.Empty;
                    var last = state.LastValue ?? 0;
                    if (definition.Key == CombatCategories.Damage)
                    {
                        var window = state.Window.Sum(p => p.Value);
                        var dps = window / ttl;
                        text = $"{label} {Format(last)}{unit} {Format(dps)}DPS";
                    }
                    else
                    {
                        var window = state.Window.Sum(p => p.Value);
                        text = $"{label} {Format(last)}{unit} {Format(window)}{unit}";
                    }
                }
                else
                {
                    text = label;
                }

                rows.Add(new CombatLogRow
                {
                    Category = pair.Key,
                    Order = definition.Priority,
                    Text = text,
                    ColorKey = ColorKeyOf(pair.Key),
                    Detail = BuildDetail(label, state.LastEvent, definition, state.Window.Sum(p => p.Value), ttl),
                    LastValue = state.LastValue,
                    WindowValue = definition.HasValue ? state.Window.Sum(p => p.Value) : null,
                    Dps = definition.Key == CombatCategories.Damage && definition.HasValue
                        ? state.Window.Sum(p => p.Value) / ttl
                        : null,
                });
            }
        }

        return rows
            .OrderBy(p => p.Order)
            .ToList();
    }

    public bool HasRows(string? listener) => GetRows(listener).Count > 0;

    /// <summary>可见分类的文本（供设置页预览）。</summary>
    public IReadOnlyList<(string Category, bool Enabled)> CategoryOptions
        => CombatCategories.All
            .Select(p => (p.Key, IsCategoryEnabled(p.Key)))
            .ToList();

    private static string CategoryLabelKey(string category) => category switch
    {
        CombatCategories.Scram => "CombatCategory_Scram",
        CombatCategories.Neut => "CombatCategory_Neut",
        CombatCategories.Nos => "CombatCategory_Nos",
        CombatCategories.Ecm => "CombatCategory_Ecm",
        CombatCategories.Rep => "CombatCategory_Rep",
        CombatCategories.CapTransfer => "CombatCategory_CapTransfer",
        CombatCategories.Damage => "CombatCategory_Damage",
        _ => category,
    };

    private static string ColorKeyOf(string category) => category switch
    {
        CombatCategories.Scram => "danger",
        CombatCategories.Neut => "warning",
        CombatCategories.Nos => "info",
        CombatCategories.Ecm => "purple",
        CombatCategories.Rep => "success",
        CombatCategories.CapTransfer => "info",
        CombatCategories.Damage => "danger",
        _ => "normal",
    };

    private static string BuildDetail(string label, CombatLogEvent? ev, CombatCategoryDefinition definition, double window, int ttl)
    {
        if (ev is null)
        {
            return label;
        }

        var parts = new List<string> { label };
        if (!string.IsNullOrWhiteSpace(ev.ModuleName))
        {
            parts.Add(ev.ModuleName);
        }

        if (!string.IsNullOrWhiteSpace(ev.Pilot))
        {
            var actor = ev.Pilot;
            if (!string.IsNullOrWhiteSpace(ev.Ship))
            {
                actor += $" ({ev.Ship})";
            }

            parts.Add(actor);
        }

        if (definition.HasValue)
        {
            var unit = definition.Unit ?? string.Empty;
            parts.Add($"最近一次 {Format(ev.Amount ?? 0)}{unit}");
            parts.Add($"{ttl} 秒内 {Format(window)}{unit}");
        }

        parts.Add(ev.Time.ToLocalTime().ToString("HH:mm:ss"));
        return string.Join(" · ", parts);
    }

    private static string Format(double value) => value.ToString("0");

    private void Tick()
    {
        try
        {
            List<string>? toNotify = null;
            lock (_locker)
            {
                var now = DateTime.Now;
                var ttl = Math.Max(1, TtlSeconds);
                foreach (var pair in _states)
                {
                    // 有活跃数据、或刚过期（需要把行刷掉）都要通知
                    var anyActive = pair.Value.Values.Any(state =>
                        state.Window.Count > 0 || (now - state.LastSeen).TotalSeconds <= ttl);
                    var anyTracked = pair.Value.Count > 0;
                    if (anyActive || anyTracked)
                    {
                        toNotify ??= [];
                        toNotify.Add(pair.Key);
                    }
                }

                // 清掉已空的状态，避免 1 秒定时器永远为旧数据空转
                foreach (var pair in _states.ToList())
                {
                    foreach (var (category, state) in pair.Value.ToList())
                    {
                        if (state.Window.Count == 0 && (now - state.LastSeen).TotalSeconds > ttl)
                        {
                            pair.Value.Remove(category);
                        }
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

    /// <summary>数据变化（每角色）；由 UI 线程回调。</summary>
    public event Action<string>? Changed;

    private void NotifyAll()
    {
        foreach (var listener in AttachedListeners)
        {
            RaiseChanged(listener);
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
            return;
        }

        // 后台日志线程：合并到一次 UI 调度，避免高频战斗行把消息队列打满
        lock (_locker)
        {
            if (!_pendingNotify.Add(listener))
            {
                return;
            }
        }

        dispatcher.BeginInvoke(new Action(() =>
        {
            lock (_locker)
            {
                _pendingNotify.Remove(listener);
            }

            Changed?.Invoke(listener);
        }));
    }
}
