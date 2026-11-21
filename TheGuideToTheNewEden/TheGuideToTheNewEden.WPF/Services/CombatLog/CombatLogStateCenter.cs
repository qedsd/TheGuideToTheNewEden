using System.Windows;
using System.Windows.Threading;
using TheGuideToTheNewEden.Core.Models.CombatLog;
using TheGuideToTheNewEden.Core.Services.CombatLog;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Services.CombatLog;

/// <summary>一行战斗数值快照（一个分类的一个方向 / 一种遥修类型）。</summary>
public sealed class CombatLogLineSnapshot
{
    /// <summary>该行的数量文本（如 <c>45</c>、<c>100</c>）——界面用大字号显示。</summary>
    public string AmountText { get; init; } = string.Empty;

    /// <summary>数量后面的部分（单位 / 每秒 / 遥修类型，如 <c>GJ 22.5 GJ/s</c>、<c>HP 20 HP/s 护盾</c>）——界面用小字号。</summary>
    public string RestText { get; init; } = string.Empty;

    /// <summary>整行文本（数量 + 空格 + 其余部分），叠加层与探针用。</summary>
    public string Text => string.IsNullOrWhiteSpace(RestText) ? AmountText : $"{AmountText} {RestText}";

    /// <summary>方向（输入 / 输出），界面据此决定红/绿/中性。</summary>
    public CombatEventDirection Direction { get; init; }

    /// <summary>遥修类型标签（护盾 / 装甲 / 结构）；其它分类为空。</summary>
    public string SubTypeLabel { get; init; } = string.Empty;

    /// <summary>最近一轮的合计值（同一齐射的多条日志相加）。</summary>
    public double? LastValue { get; init; }

    /// <summary>按实测轮间隔算出的每秒速率；不足两轮为 null。</summary>
    public double? Rate { get; init; }

    /// <summary>最近一次测到的两轮间隔（秒）；不足两轮为 null。</summary>
    public double? IntervalSeconds { get; init; }

    /// <summary>距离该行最后一次日志已超过「过期时间」。</summary>
    public bool Expired { get; init; }

    /// <summary>悬停详情（方向、分类、装备、来源、实测间隔、时间）。</summary>
    public string Detail { get; init; } = string.Empty;
}

/// <summary>
/// 一行战斗叠加快照（一个分类 + 若干数值行）。
/// 阶段 366：颜色按"对启用方是收益还是伤害"；阶段 369：遥修按护盾 / 装甲 / 结构拆成多行。
/// </summary>
public sealed class CombatLogRow
{
    public required string Category { get; init; }

    public required string ColorKey { get; init; }

    public required int Order { get; init; }

    /// <summary>该分类的数值行（遥修最多三行：护盾 / 装甲 / 结构；其它最多两行：输入 / 输出）。</summary>
    public IReadOnlyList<CombatLogLineSnapshot> Lines { get; init; } = [];

    /// <summary>分类标签（用于叠加层文本）。</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>所有行都已过期（卡片整体置灰）。</summary>
    public bool Expired => Lines.Count == 0 || Lines.All(p => p.Expired);

    /// <summary>多开叠加层用的单行文本（分类名 + 各数值行；过期判断由叠加层自己做）。</summary>
    public string Text => string.Join(" · ", Lines.Select(p =>
        string.IsNullOrWhiteSpace(p.Text) ? Label : $"{Label} {p.Text}"));

    /// <summary>多开叠加层的悬停详情。</summary>
    public string Detail => string.Join("\n", Lines.Select(p => p.Detail));
}

/// <summary>
/// 战斗日志状态中心（阶段 357；阶段 358 按实测轮间隔计算；阶段 362 过期只置灰；
/// 阶段 363 同一个分类按方向分开记「承受 / 输出」）。
/// <list type="bullet">
///   <item>每个分类有两份互不干扰的状态：<b>承受</b>（进向：别人打你/毁你/吸你/修你）和
///         <b>输出</b>（出向：你打/毁/吸/修别人）。两边的数值、速率、过期时间各自独立。</item>
///   <item>
///     <b>速率按实测间隔算</b>：同一攻击者 + 同一装备的连续日志按"轮"分组（同一齐射的日志时间戳相同），
///     速率 = 最近几轮的合计 ÷ 这几轮之间的实际秒数，不除以写死的窗口。
///   </item>
///   <item>
///     <b>过期只置灰，不消失</b>：某一侧超过 <see cref="ExpireSeconds"/> 没有新日志 → 该侧标记过期；
///     整个分类两侧都过期时卡片置灰 + 「已过期」。整行要消失只能手动 <see cref="Clear"/> 或
///     停止监控（<see cref="Detach"/>）。
///   </item>
///   <item>
///     反跳 / ECM 没有周期性日志，只保留"承受"侧、不参与过期判断（保持阶段 358 的"常驻"语义）。
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

    /// <summary>过期时间的合法上限（"任意秒数"也不能夸张到溢出/永久挂着一堆模块）。</summary>
    private const int MaxExpireSeconds = 86400;

    /// <summary>纯内存清理：模块超过这么久没动静就丢掉（数值/速率都有快照，不影响界面）。</summary>
    private const double ModuleRetentionSeconds = 7200;

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

    /// <summary>某一个方向（承受 / 输出）在某个分类下的状态。</summary>
    private sealed class SideState
    {
        public readonly Dictionary<string, ModuleState> Modules = new(StringComparer.OrdinalIgnoreCase);

        public DateTime LastSeen = DateTime.MinValue;

        public double? LastValue;

        /// <summary>最近一次算出的速率快照（过期后数值停在这里，不再随模块清理变化）。</summary>
        public double? LastRate;

        public CombatLogEvent? LastEvent;

        public string? LastModuleKey;

        public bool HasData => LastSeen != DateTime.MinValue;
    }

    private sealed class CategoryState
    {
        /// <summary>输入侧（进向），按"子类型"分桶：遥修用 护盾/装甲/结构，其它分类用空键。</summary>
        public readonly Dictionary<string, SideState> Incoming = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>输出侧（出向），同样按子类型分桶。</summary>
        public readonly Dictionary<string, SideState> Outgoing = new(StringComparer.OrdinalIgnoreCase);
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

    /// <summary>
    /// 过期时间（秒，默认 60，可在日志监控页「战斗日志」里手输任意秒数）：
    /// 某一侧超过该秒数没有新日志就标记过期；卡片两侧都过期时置灰并提示「已过期」——**行不会消失**，
    /// 只有手动清除/停止监控才清。反跳 / ECM 没有周期日志，不参与过期判断。
    /// </summary>
    public int ExpireSeconds { get; set; } = 60;

    /// <summary>生效的过期秒数（夹到 1..<see cref="MaxExpireSeconds"/>）。</summary>
    private int EffectiveExpireSeconds => Math.Clamp(ExpireSeconds, 1, MaxExpireSeconds);

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

        if (ev.Direction == CombatEventDirection.Unknown
            && ev.Kind is CombatEventKind.RemoteRepair or CombatEventKind.RemoteCapacitor)
        {
            // 修/传电分不清"谁修谁"时无法归到任何一侧，丢弃
            return;
        }

        var definition = CombatCategories.Find(ev.CategoryKey);
        var outgoing = ev.Direction == CombatEventDirection.Outgoing;
        // 阶段 374：反跳 / ECM 两种方向都收：进向显示「被反跳 / 被干扰」，出向显示「反跳对方 / 干扰对方」。

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

            var sideMap = outgoing ? state.Outgoing : state.Incoming;
            var subType = SubTypeKeyOf(ev);
            if (!sideMap.TryGetValue(subType, out var side))
            {
                side = new SideState();
                sideMap[subType] = side;
            }

            side.LastSeen = when;
            side.LastEvent = ev;
            side.LastModuleKey = moduleKey;

            if (!side.Modules.TryGetValue(moduleKey, out var module))
            {
                module = new ModuleState();
                side.Modules[moduleKey] = module;
            }

            module.LastSeen = when;
            module.LastEvent = ev;
            if (ev.Amount is { } amount)
            {
                // 0 值表示中和器本轮确实命中（日志里会明确写 0 GJ）：仍建立轮次并显示 0，
                // 不能把它与「没有 Amount」的状态类日志混为一谈。
                AddRound(module, when, Math.Abs(amount));
                // 第一段显示"这一轮的合计"：同一齐射的多条伤害日志会并成一轮，
                // 单发伤害加起来才是"这一轮挨了多少"（毁电/遥修一轮通常只有一条，数值不变）。
                side.LastValue = module.Rounds[^1].Sum;
            }

            // 速率快照：过期后界面显示的就是最后一次算出来的值（不会因为模块被清理而变空）
            side.LastRate = RateOfRecentModules(side, when);
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

    /// <summary>
    /// 当前全部战斗行（已按优先级排序，含已过期的行，每行带承受 / 输出两侧）；无事件返回空。
    /// 叠加层只用 <see cref="CombatLogRow.Text"/>（承受侧）。
    /// </summary>
    public IReadOnlyList<CombatLogRow> GetRows(string? listener)
    {
        EnsureTimer();
        if (string.IsNullOrWhiteSpace(listener))
        {
            return [];
        }

        var expire = EffectiveExpireSeconds;
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
                var label = LanguageService.Get(CategoryLabelKey(pair.Key));
                var lines = new List<CombatLogLineSnapshot>();
                if (string.Equals(pair.Key, CombatCategories.Rep, StringComparison.OrdinalIgnoreCase))
                {
                    // 阶段 370：遥修两种方向都列（被修 / 我修别人），每种类型最多两行，按 护盾→装甲→结构 排列。
                    foreach (var repairType in RepairTypeOrder)
                    {
                        var typeKey = SubTypeKeyOf(repairType);
                        state.Incoming.TryGetValue(typeKey, out var inSide);
                        state.Outgoing.TryGetValue(typeKey, out var outSide);
                        var typeLabel = RepairTypeLabel(repairType);
                        var incomingLine = BuildLine(inSide, CombatEventDirection.Incoming, null, definition, label, typeLabel, now, expire);
                        if (incomingLine is not null)
                        {
                            lines.Add(incomingLine);
                        }

                        var outgoingLine = BuildLine(outSide, CombatEventDirection.Outgoing, null, definition, label, typeLabel, now, expire);
                        if (outgoingLine is not null)
                        {
                            lines.Add(outgoingLine);
                        }
                    }

                    // 类型识别不出来的遥修（旧日志 / 特殊模块名）仍要显示，输入、输出各一行。
                    var unknownKey = SubTypeKeyOf(RemoteRepairType.None);
                    state.Incoming.TryGetValue(unknownKey, out var unknownIn);
                    state.Outgoing.TryGetValue(unknownKey, out var unknownOut);
                    var unknownIncoming = BuildLine(unknownIn, CombatEventDirection.Incoming, null, definition, label, null, now, expire);
                    if (unknownIncoming is not null)
                    {
                        lines.Add(unknownIncoming);
                    }

                    var unknownOutgoing = BuildLine(unknownOut, CombatEventDirection.Outgoing, null, definition, label, null, now, expire);
                    if (unknownOutgoing is not null)
                    {
                        lines.Add(unknownOutgoing);
                    }
                }
                else
                {
                    state.Incoming.TryGetValue(string.Empty, out var inSide);
                    state.Outgoing.TryGetValue(string.Empty, out var outSide);
                    var incomingLine = BuildLine(inSide, CombatEventDirection.Incoming, null, definition, label, null, now, expire);
                    if (incomingLine is not null)
                    {
                        lines.Add(incomingLine);
                    }

                    var outgoingLine = BuildLine(outSide, CombatEventDirection.Outgoing, null, definition, label, null, now, expire);
                    if (outgoingLine is not null)
                    {
                        lines.Add(outgoingLine);
                    }
                }

                if (lines.Count == 0)
                {
                    continue;
                }

                rows.Add(new CombatLogRow
                {
                    Category = pair.Key,
                    Order = definition.Priority,
                    ColorKey = ColorKeyOf(pair.Key),
                    Label = label,
                    Lines = lines,
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

    /// <summary>该分类该方向里"最近一次日志还没过期"的模块速率之和（多艘船/多门炮分别算再相加）。</summary>
    private double? RateOfRecentModules(SideState side, DateTime when)
    {
        var window = EffectiveExpireSeconds;
        double sum = 0;
        var any = false;
        foreach (var module in side.Modules.Values)
        {
            if ((when - module.LastSeen).TotalSeconds > window)
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

    private static double? LastIntervalOf(SideState side)
    {
        if (!string.IsNullOrEmpty(side.LastModuleKey)
            && side.Modules.TryGetValue(side.LastModuleKey, out var module)
            && module.LastInterval.HasValue)
        {
            return module.LastInterval;
        }

        var intervals = side.Modules.Values
            .Where(p => p.LastInterval.HasValue)
            .Select(p => p.LastInterval!.Value)
            .ToList();
        return intervals.Count == 0 ? null : intervals.Max();
    }

    /// <summary>把某一侧的状态转成一行展示快照；该侧没有日志时返回 null。</summary>
    private static CombatLogLineSnapshot? BuildLine(
        SideState? side,
        CombatEventDirection direction,
        SideState? fallbackSide,
        CombatCategoryDefinition definition,
        string label,
        string? subTypeLabel,
        DateTime now,
        int expire)
    {
        var usedDirection = direction;
        var used = side;
        if (used is null || !used.HasData)
        {
            // 遥修同一类型没有"被修"日志时，退回"我修别人"（方向随之变输出，颜色按中性）。
            used = fallbackSide;
            usedDirection = direction == CombatEventDirection.Incoming
                ? CombatEventDirection.Outgoing
                : CombatEventDirection.Incoming;
        }

        if (used is null || !used.HasData)
        {
            return null;
        }

        var expired = !definition.Sticky && (now - used.LastSeen).TotalSeconds > expire;
        var interval = LastIntervalOf(used);
        var parts = BuildParts(definition, used.LastValue, used.LastRate, subTypeLabel);
        return new CombatLogLineSnapshot
        {
            AmountText = parts.Amount,
            RestText = parts.Suffix,
            Direction = usedDirection,
            SubTypeLabel = subTypeLabel ?? string.Empty,
            LastValue = used.LastValue,
            Rate = used.LastRate,
            IntervalSeconds = interval,
            Expired = expired,
            Detail = BuildSideDetail(definition, label, used, used.LastValue, used.LastRate, interval, subTypeLabel, usedDirection),
        };
    }

    private static string BuildSideDetail(
        CombatCategoryDefinition definition,
        string label,
        SideState side,
        double? lastValue,
        double? rate,
        double? interval,
        string? subTypeLabel,
        CombatEventDirection direction)
    {
        var parts = new List<string>
        {
            LanguageService.Get(direction == CombatEventDirection.Outgoing ? "CombatLog_Outgoing" : "CombatLog_Incoming"),
            label,
        };
        if (!string.IsNullOrWhiteSpace(subTypeLabel))
        {
            parts.Add(subTypeLabel);
        }

        var ev = side.LastEvent;
        if (ev is not null)
        {
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
        }

        if (definition.HasValue && lastValue is { } last)
        {
            var unit = definition.Unit ?? string.Empty;
            if (interval is { } seconds && rate is { } value)
            {
                parts.Add($"{FormatAmount(last)}{unit}/{FormatSeconds(seconds)}s = {FormatRate(value)}{unit}/s");
            }
            else
            {
                parts.Add($"{FormatAmount(last)}{unit}");
            }
        }

        parts.Add(ev is null || ev.Time == default
            ? DateTime.Now.ToString("HH:mm:ss")
            : ev.Time.ToLocalTime().ToString("HH:mm:ss"));
        return string.Join(" · ", parts);
    }

    /// <summary>
    /// 统一行文拆两段：<c>{数量}</c>（大字号）+ <c>{单位} {每秒} {单位}/s {类型}</c>（小字号）。
    /// 不足两轮时第二段只有单位；遥修末尾追加类型（护盾 / 装甲 / 结构）。
    /// </summary>
    private static (string Amount, string Suffix) BuildParts(
        CombatCategoryDefinition definition,
        double? lastValue,
        double? rate,
        string? subTypeLabel)
    {
        if (!definition.HasValue || lastValue is not { } last)
        {
            return (string.Empty, subTypeLabel ?? string.Empty);
        }

        var unit = definition.Unit ?? string.Empty;
        var rest = rate is { } value
            ? $"{unit} {FormatRate(value)} {unit}/s"
            : $"{unit}";
        if (!string.IsNullOrWhiteSpace(subTypeLabel))
        {
            rest = $"{rest} {subTypeLabel}";
        }

        return (FormatAmount(last), rest);
    }

    /// <summary>遥修类型的展示顺序与语言键。</summary>
    private static readonly RemoteRepairType[] RepairTypeOrder =
    [
        RemoteRepairType.Shield,
        RemoteRepairType.Armor,
        RemoteRepairType.Hull,
    ];

    /// <summary>子类型桶键：遥修用类型名，其它分类用空串。</summary>
    private static string SubTypeKeyOf(CombatLogEvent ev)
        => ev.Kind == CombatEventKind.RemoteRepair ? ev.RepairType.ToString() : string.Empty;

    private static string SubTypeKeyOf(RemoteRepairType repairType) => repairType.ToString();

    private static string? RepairTypeLabel(RemoteRepairType repairType) => repairType switch
    {
        RemoteRepairType.Shield => LanguageService.Get("CombatLog_RepShield"),
        RemoteRepairType.Armor => LanguageService.Get("CombatLog_RepArmor"),
        RemoteRepairType.Hull => LanguageService.Get("CombatLog_RepHull"),
        _ => null,
    };

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
        CombatCategories.ScramDisrupt => "CombatCategory_ScramDisrupt",
        CombatCategories.Neut => "CombatCategory_Neut",
        CombatCategories.Nos => "CombatCategory_Nos",
        CombatCategories.Ecm => "CombatCategory_Ecm",
        CombatCategories.Web => "CombatCategory_Web",
        CombatCategories.Painter => "CombatCategory_Painter",
        CombatCategories.WeaponDisrupt => "CombatCategory_WeaponDisrupt",
        CombatCategories.SensorDamp => "CombatCategory_SensorDamp",
        CombatCategories.Rep => "CombatCategory_Rep",
        CombatCategories.CapTransfer => "CombatCategory_CapTransfer",
        CombatCategories.Damage => "CombatCategory_Damage",
        _ => category,
    };

    private static string ColorKeyOf(string category) => category switch
    {
        CombatCategories.Scram => "danger",
        CombatCategories.ScramDisrupt => "danger",
        CombatCategories.Neut => "warning",
        CombatCategories.Nos => "info",
        CombatCategories.Ecm => "purple",
        CombatCategories.Web => "purple",
        CombatCategories.Painter => "purple",
        CombatCategories.WeaponDisrupt => "purple",
        CombatCategories.SensorDamp => "purple",
        CombatCategories.Rep => "success",
        CombatCategories.CapTransfer => "info",
        CombatCategories.Damage => "danger",
        _ => "normal",
    };

    /// <summary>
    /// 1 秒心跳：只在"某一侧刚过期 / 刚恢复（有第一条新日志）"时通知界面重画；
    /// 顺带清理很久没动静的模块（纯内存，不影响界面数值——数值都有快照）。
    /// </summary>
    private void Tick()
    {
        try
        {
            List<string>? toNotify = null;
            lock (_locker)
            {
                var now = DateTime.UtcNow;
                var expire = EffectiveExpireSeconds;
                foreach (var pair in _states)
                {
                    var signature = ExpirySignature(pair.Value, now, expire);
                    _visibleSignatures.TryGetValue(pair.Key, out var previous);
                    PruneStaleModules(pair.Value, now);
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

    /// <summary>过期状态签名：分类 key + 两侧各自的过期状态（用于判断"界面需要重画吗"）。</summary>
    private string ExpirySignature(Dictionary<string, CategoryState> categories, DateTime now, int expire)
    {
        List<string> keys = [];
        foreach (var pair in categories)
        {
            var definition = CombatCategories.Find(pair.Key);
            if (definition is null || !_enabledCategories.Contains(pair.Key))
            {
                continue;
            }

            keys.Add($"{pair.Key}:{SidesFlag(pair.Value.Incoming, definition, now, expire)}|{SidesFlag(pair.Value.Outgoing, definition, now, expire)}");
        }

        keys.Sort(StringComparer.OrdinalIgnoreCase);
        return string.Join(',', keys);
    }

    /// <summary>一组同方向、不同子类型的过期状态签名（护盾/装甲/结构各自 0/1/-）。</summary>
    private static string SidesFlag(Dictionary<string, SideState> sides, CombatCategoryDefinition definition, DateTime now, int expire)
    {
        if (sides.Count == 0)
        {
            return "-";
        }

        return string.Concat(sides
            .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .Select(p => $"{p.Key}={SideFlag(p.Value, definition, now, expire)}"));
    }

    private static char SideFlag(SideState side, CombatCategoryDefinition definition, DateTime now, int expire)
    {
        if (!side.HasData)
        {
            return '-';
        }

        return !definition.Sticky && (now - side.LastSeen).TotalSeconds > expire ? '1' : '0';
    }

    /// <summary>清掉很久没动静的模块（只影响 ToolTip 里的间隔与内存占用，界面数值用的是快照）。</summary>
    private static void PruneStaleModules(Dictionary<string, CategoryState> categories, DateTime now)
    {
        foreach (var state in categories.Values)
        {
            foreach (var side in state.Incoming.Values)
            {
                PruneSide(side, now);
            }

            foreach (var side in state.Outgoing.Values)
            {
                PruneSide(side, now);
            }
        }
    }

    private static void PruneSide(SideState side, DateTime now)
    {
        var stale = side.Modules
            .Where(p => (now - p.Value.LastSeen).TotalSeconds > ModuleRetentionSeconds)
            .Select(p => p.Key)
            .ToList();
        foreach (var key in stale)
        {
            side.Modules.Remove(key);
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
