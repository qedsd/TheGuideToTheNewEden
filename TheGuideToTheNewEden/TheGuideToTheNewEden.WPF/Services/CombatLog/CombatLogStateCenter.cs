using System.Windows;
using System.Windows.Threading;
using TheGuideToTheNewEden.Core.Models.CombatLog;
using TheGuideToTheNewEden.Core.Services.CombatLog;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Services.CombatLog;

/// <summary>一行战斗叠加快照（分类 + 最近一轮合计 + 实测速率 + 颜色/详情）。</summary>
public sealed class CombatLogRow
{
    public required string Category { get; init; }

    public required string Text { get; init; }

    public required string ColorKey { get; init; }

    public required int Order { get; init; }

    public string? Detail { get; init; }

    /// <summary>最近一轮的合计值（同一齐射的多条日志相加；无值分类为 null）。</summary>
    public double? LastValue { get; init; }

    /// <summary>按实测轮间隔算出的每秒速率（承伤是 DPS，毁电/吸电/传电是 GJ/s，遥修是 HP/s）；不足两轮为 null。</summary>
    public double? Rate { get; init; }

    /// <summary>最近一次测到的两轮间隔（秒）；不足两轮为 null。</summary>
    public double? IntervalSeconds { get; init; }
}

/// <summary>
/// 战斗日志状态中心（阶段 357；阶段 358 改为按实测轮间隔计算 + 一次性状态常驻）。
/// <list type="bullet">
///   <item>只保留"进"向事件（别人打你/修你/干扰你）；出向直接丢弃。</item>
///   <item>
///     <b>速率按实测间隔算</b>：同一攻击者 + 同一装备的连续日志按"轮"分组（同一齐射的日志时间戳相同），
///     速率 = 最近几轮的合计 ÷ 这几轮之间的实际秒数，不再除以写死的 5 秒窗口。
///   </item>
///   <item>
///     <b>保留时长也是实测的</b>：某分类超过"实测轮间隔 + 宽限"没有新日志才隐藏（下限是设置里的
///     <see cref="TtlSeconds"/>，上限 60 秒），因此大型中和器这类慢周期装备不会闪断。
///   </item>
///   <item>
///     <b>一次性状态</b>（反跳 / ECM）：对面不停止就不会再刷日志，故不按时间消失，
///     保留到用户手动 <see cref="Clear"/> 或停止监控（<see cref="Detach"/>）。
///   </item>
///   <item>由日志监控的"战斗日志"开关驱动 <see cref="Attach"/> / <see cref="Detach"/>；
///         多开叠加层与日志监控小窗都只读这里。</item>
/// </list>
/// </summary>
public sealed class CombatLogStateCenter
{
    public static CombatLogStateCenter Current { get; } = new();

    /// <summary>同一模块（同攻击者 + 同装备）在这个间隔内的多条日志算同一轮齐射（日志时间戳精度 1 秒）。</summary>
    private const double RoundGapSeconds = 1.0;

    /// <summary>超过这么久没动静，下一发重新开始计轮（不把几分钟的空档算进速率）。</summary>
    private const double RoundResetSeconds = 60;

    /// <summary>每个模块保留的轮数。</summary>
    private const int MaxRoundsPerModule = 8;

    /// <summary>速率最多平均最近这么多段间隔（1 = 严格"前后两轮"）。</summary>
    private const int MaxRateIntervals = 3;

    /// <summary>实测周期之外再给的宽限（避免慢周期装备闪断一秒）。</summary>
    private const double KeepAliveGraceSeconds = 1.5;

    /// <summary>自适应保留时长上限（异常长间隔不会把行留在界面上太久）。</summary>
    private const double MaxKeepAliveSeconds = 60;

    /// <summary>一轮（一次齐射 / 一次中和 / 一次遥修周期）。</summary>
    private sealed class Round
    {
        public Round(DateTime start, double value)
        {
            Start = start;
            Last = start;
            Sum = value;
            Count = 1;
        }

        public DateTime Start { get; }

        public DateTime Last { get; set; }

        public double Sum { get; set; }

        public int Count { get; set; }
    }

    /// <summary>一个攻击者的一个装备（速率与间隔的最小单位）。</summary>
    private sealed class ModuleState
    {
        public readonly List<Round> Rounds = [];

        public DateTime LastSeen = DateTime.MinValue;

        /// <summary>最近两轮开始时间的差（秒）；不足两轮为 null。</summary>
        public double? LastInterval;

        public CombatLogEvent? LastEvent;
    }

    private sealed class CategoryState
    {
        public readonly Dictionary<string, ModuleState> Modules = new(StringComparer.OrdinalIgnoreCase);

        public DateTime LastSeen = DateTime.MinValue;

        public double? LastValue;

        public CombatLogEvent? LastEvent;

        public string? LastModuleKey;
    }

    private readonly object _locker = new();
    private readonly Dictionary<string, Dictionary<string, CategoryState>> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _visibleSignatures = new(StringComparer.OrdinalIgnoreCase);
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

    /// <summary>保留时长下限（秒）：实测周期更长的分类会自动延长到"周期 + 宽限"，见 <see cref="KeepAliveOf"/>。</summary>
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
            _visibleSignatures[listener] = string.Empty;
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
            _visibleSignatures.Remove(listener);
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

        // 日志时间是 UTC（EVE 日志口径）；缺省时退回当前 UTC，保证间隔计算的口径一致
        var when = ev.Time == default ? DateTime.UtcNow : ev.Time;
        var moduleKey = BuildModuleKey(ev);

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

            state.LastSeen = when;
            state.LastEvent = ev;
            state.LastModuleKey = moduleKey;

            if (!state.Modules.TryGetValue(moduleKey, out var module))
            {
                module = new ModuleState();
                state.Modules[moduleKey] = module;
            }

            module.LastSeen = when;
            module.LastEvent = ev;
            if (ev.Amount is { } amount)
            {
                AddRound(module, when, Math.Abs(amount));
                // 第一段显示"这一轮的合计"：同一齐射的多条伤害日志会并成一轮，
                // 单发伤害加起来才是"这一轮挨了多少"（毁电/遥修一轮通常只有一条，数值不变）。
                state.LastValue = module.Rounds[^1].Sum;
            }
        }

        RaiseChanged(listener);
    }

    /// <summary>手动清除某角色的所有战斗行（反跳 / ECM 没有"停止"日志，只能这样收掉）。</summary>
    public void Clear(string listener)
    {
        if (string.IsNullOrWhiteSpace(listener))
        {
            return;
        }

        lock (_locker)
        {
            if (!_attached.Contains(listener))
            {
                return;
            }

            _states[listener] = new Dictionary<string, CategoryState>(StringComparer.OrdinalIgnoreCase);
            _visibleSignatures[listener] = string.Empty;
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
        var now = DateTime.UtcNow;
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
                if (!IsVisible(state, definition, now, ttl))
                {
                    continue;
                }

                var label = LanguageService.Get(CategoryLabelKey(pair.Key));
                var rate = RateOfActiveModules(state, now, ttl);
                var interval = LastIntervalOf(state);
                var text = BuildText(definition, label, state.LastValue, rate);

                rows.Add(new CombatLogRow
                {
                    Category = pair.Key,
                    Order = definition.Priority,
                    Text = text,
                    ColorKey = ColorKeyOf(pair.Key),
                    Detail = BuildDetail(label, state.LastEvent, definition, state.LastValue, interval, rate),
                    LastValue = state.LastValue,
                    Rate = rate,
                    IntervalSeconds = interval,
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

    /// <summary>某分类当前保留时长（秒）：设置下限、实测周期 + 宽限、上限三者取中。</summary>
    public double GetKeepAliveSeconds(string? listener, string category)
    {
        lock (_locker)
        {
            var ttl = Math.Max(1, TtlSeconds);
            if (string.IsNullOrWhiteSpace(listener)
                || !_states.TryGetValue(listener, out var categories)
                || !categories.TryGetValue(category, out var state))
            {
                return ttl;
            }

            return KeepAliveOf(state, ttl);
        }
    }

    // ---------- 轮 / 间隔 / 速率 ----------

    /// <summary>把一条带数值的日志并进"当前轮"，或者按实测间隔开启新一轮。</summary>
    private static void AddRound(ModuleState module, DateTime when, double value)
    {
        var rounds = module.Rounds;
        if (rounds.Count > 0 && (when - rounds[^1].Last).TotalSeconds > RoundResetSeconds)
        {
            // 空档太久：当成新的交战，不把这段间隔算进速率
            rounds.Clear();
            module.LastInterval = null;
        }

        if (rounds.Count == 0 || (when - rounds[^1].Last).TotalSeconds > RoundGapSeconds)
        {
            if (rounds.Count > 0)
            {
                module.LastInterval = (when - rounds[^1].Start).TotalSeconds;
            }

            if (rounds.Count >= MaxRoundsPerModule)
            {
                rounds.RemoveAt(0);
            }

            rounds.Add(new Round(when, value));
            return;
        }

        var round = rounds[^1];
        round.Sum += value;
        round.Count++;
        round.Last = when;
    }

    /// <summary>最近几轮的合计 ÷ 这几轮之间的实际秒数（不足两轮返回 null）。</summary>
    private static double? RateOf(ModuleState module)
    {
        var rounds = module.Rounds;
        if (rounds.Count < 2)
        {
            return null;
        }

        var count = Math.Min(rounds.Count, MaxRateIntervals + 1);
        var first = rounds[^count];
        var last = rounds[^1];
        var span = (last.Start - first.Start).TotalSeconds;
        if (span <= 0.2)
        {
            return null;
        }

        double sum = 0;
        for (var i = rounds.Count - count + 1; i < rounds.Count; i++)
        {
            sum += rounds[i].Sum;
        }

        return sum / span;
    }

    /// <summary>该分类里仍在生效的模块速率之和（多艘船/多门炮分别算再相加）。</summary>
    private static double? RateOfActiveModules(CategoryState state, DateTime now, int ttl)
    {
        double sum = 0;
        var any = false;
        foreach (var module in state.Modules.Values)
        {
            if (!IsModuleActive(module, now, ttl))
            {
                continue;
            }

            if (RateOf(module) is { } rate)
            {
                sum += rate;
                any = true;
            }
        }

        return any ? sum : null;
    }

    private static bool IsModuleActive(ModuleState module, DateTime now, int ttl)
        => (now - module.LastSeen).TotalSeconds <= ModuleKeepAliveOf(module, ttl);

    private static double ModuleKeepAliveOf(ModuleState module, int ttl)
        => module.LastInterval is { } interval
            ? Math.Min(MaxKeepAliveSeconds, Math.Max(ttl, interval + KeepAliveGraceSeconds))
            : ttl;

    /// <summary>分类保留时长：默认是设置的下限，实测周期更长时按周期延长。</summary>
    private static double KeepAliveOf(CategoryState state, int ttl)
    {
        var interval = state.Modules.Values
            .Select(p => p.LastInterval ?? 0)
            .DefaultIfEmpty(0)
            .Max();
        return interval <= 0
            ? ttl
            : Math.Min(MaxKeepAliveSeconds, Math.Max(ttl, interval + KeepAliveGraceSeconds));
    }

    private static double? LastIntervalOf(CategoryState state)
    {
        if (!string.IsNullOrEmpty(state.LastModuleKey)
            && state.Modules.TryGetValue(state.LastModuleKey, out var module)
            && module.LastInterval.HasValue)
        {
            return module.LastInterval;
        }

        var intervals = state.Modules.Values
            .Where(p => p.LastInterval.HasValue)
            .Select(p => p.LastInterval!.Value)
            .ToList();
        return intervals.Count == 0 ? null : intervals.Max();
    }

    private static bool IsVisible(CategoryState state, CombatCategoryDefinition definition, DateTime now, int ttl)
    {
        // 反跳 / ECM 只在作用上时有一条日志：对面不停止就再也不会刷，不能按超时消失
        if (definition.Sticky)
        {
            return true;
        }

        return (now - state.LastSeen).TotalSeconds <= KeepAliveOf(state, ttl);
    }

    private static string BuildText(CombatCategoryDefinition definition, string label, double? lastValue, double? rate)
    {
        if (!definition.HasValue || lastValue is not { } last)
        {
            return label;
        }

        var unit = definition.Unit ?? string.Empty;
        if (rate is not { } value)
        {
            return $"{label} {FormatAmount(last)}{unit}";
        }

        return definition.Key == CombatCategories.Damage
            ? $"{label} {FormatAmount(last)}{unit} {FormatRate(value)}DPS"
            : $"{label} {FormatAmount(last)}{unit} {FormatRate(value)}{unit}/s";
    }

    private static string BuildDetail(
        string label,
        CombatLogEvent? ev,
        CombatCategoryDefinition definition,
        double? lastValue,
        double? interval,
        double? rate)
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

        if (definition.HasValue && lastValue is { } last)
        {
            var unit = definition.Unit ?? string.Empty;
            if (interval is { } seconds && rate is { } value)
            {
                var rateUnit = definition.Key == CombatCategories.Damage ? "DPS" : $"{unit}/s";
                parts.Add($"{FormatAmount(last)}{unit}/{FormatSeconds(seconds)}s = {FormatRate(value)}{rateUnit}");
            }
            else
            {
                parts.Add($"{FormatAmount(last)}{unit}");
            }
        }

        parts.Add(ev.Time == default
            ? DateTime.Now.ToString("HH:mm:ss")
            : ev.Time.ToLocalTime().ToString("HH:mm:ss"));
        return string.Join(" · ", parts);
    }

    private static string FormatAmount(double value) => value.ToString("0");

    private static string FormatRate(double value) => value >= 100 ? value.ToString("0") : value.ToString("0.#");

    private static string FormatSeconds(double value) => value.ToString("0.#");

    /// <summary>速率的最小单位：同攻击者 + 同装备（不同船/不同炮各自一轮，避免间隔互相污染）。</summary>
    private static string BuildModuleKey(CombatLogEvent ev)
    {
        var module = string.IsNullOrWhiteSpace(ev.ModuleName) ? "?" : ev.ModuleName.Trim();
        var pilot = string.IsNullOrWhiteSpace(ev.Pilot) ? "-" : ev.Pilot.Trim();
        return $"{module}\u001F{pilot}";
    }

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

    /// <summary>
    /// 1 秒心跳：只在"可见行集合"发生变化时通知（行文本不随时间变，所以不需要每秒刷新）。
    /// 同时清掉已过期的分类/模块状态。
    /// </summary>
    private void Tick()
    {
        try
        {
            List<string>? toNotify = null;
            lock (_locker)
            {
                var now = DateTime.UtcNow;
                var ttl = Math.Max(1, TtlSeconds);
                foreach (var pair in _states)
                {
                    var signature = VisibleSignature(pair.Value, now, ttl);
                    _visibleSignatures.TryGetValue(pair.Key, out var previous);
                    PruneExpired(pair.Value, now, ttl);
                    if (string.Equals(previous, signature, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    _visibleSignatures[pair.Key] = signature;
                    toNotify ??= [];
                    toNotify.Add(pair.Key);
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

    private string VisibleSignature(Dictionary<string, CategoryState> categories, DateTime now, int ttl)
    {
        List<string> keys = [];
        foreach (var pair in categories)
        {
            var definition = CombatCategories.Find(pair.Key);
            if (definition is null || !_enabledCategories.Contains(pair.Key))
            {
                continue;
            }

            if (IsVisible(pair.Value, definition, now, ttl))
            {
                keys.Add(pair.Key);
            }
        }

        keys.Sort(StringComparer.OrdinalIgnoreCase);
        return string.Join(',', keys);
    }

    private static void PruneExpired(Dictionary<string, CategoryState> categories, DateTime now, int ttl)
    {
        foreach (var pair in categories.ToList())
        {
            var definition = CombatCategories.Find(pair.Key);
            var state = pair.Value;

            var expiredModules = state.Modules
                .Where(p => !IsModuleActive(p.Value, now, ttl))
                .Select(p => p.Key)
                .ToList();
            foreach (var key in expiredModules)
            {
                state.Modules.Remove(key);
            }

            // 一次性状态一直留着，直到用户清除或停止监控
            if (definition is not null
                && definition.Sticky
                && state.LastSeen != DateTime.MinValue)
            {
                continue;
            }

            if (definition is null || (now - state.LastSeen).TotalSeconds > KeepAliveOf(state, ttl))
            {
                categories.Remove(pair.Key);
            }
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
