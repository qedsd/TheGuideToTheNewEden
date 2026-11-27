using TheGuideToTheNewEden.Core.Models.CombatLog;
using TheGuideToTheNewEden.Core.Services.CombatLog;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.CombatLog;

namespace TheGuideToTheNewEden.WPF.Services.GamePreview.Overlay;

/// <summary>
/// 战斗日志 provider（阶段 357；阶段 382 改为结构化胶囊）：把 <see cref="CombatLogStateCenter"/> 里
/// 该角色的分类行（反跳 / 毁电 / 吸电 / 遥修 / 传电 / 伤害…）接到预览窗叠加层。
/// <para>
/// 只输出 provider 侧能确定的东西：分类、主值、单位、速率、遥修各层占比与配色语义；
/// **换行、行数预算、折叠、字号**都由视图按真实可用宽度决定。
/// </para>
/// <para>
/// 数据源依赖日志监控的"战斗日志"开关（与频道预警同模型）：没有该角色的战斗会话就没有胶囊，
/// 单项设置里的状态徽标由页面 VM 给出。
/// </para>
/// </summary>
public sealed class CombatLogOverlayProvider : IPreviewOverlayProvider
{
    public string Key => "combat";

    public event Action<string?>? Changed;

    public CombatLogOverlayProvider()
    {
        CombatLogStateCenter.Current.Changed += OnCenterChanged;
        LanguageService.LanguageChanged += OnLanguageChanged;
    }

    private void OnCenterChanged(string listener) => Changed?.Invoke(listener);

    private void OnLanguageChanged(object? sender, string language) => Changed?.Invoke(null);

    public IReadOnlyList<PreviewOverlayItem> GetItems(PreviewOverlayContext context)
    {
        if (!context.Global.ShowCombatInfo || !context.Item.ShowCombatInfo)
        {
            return [];
        }

        if (!CombatLogStateCenter.Current.IsAttached(context.CharacterName))
        {
            return [];
        }

        // 阶段 387：与日志监控一致——承受（▼）与输出（▲）两个方向都出胶囊，且只显示未过期的行；
        // 遥修按方向分别把各层聚合成一枚胶囊（盾/甲/构微条同样按方向算）。
        var rows = CombatLogStateCenter.Current.GetRows(context.CharacterName);
        if (rows.Count == 0)
        {
            return [];
        }

        var items = new List<PreviewOverlayItem>(rows.Count * 2);
        foreach (var row in rows)
        {
            var definition = CombatCategories.Find(row.Category);

            // 阶段 387：与日志监控一致——承受（▼）与输出（▲）两个方向都出胶囊。
            foreach (var outgoing in new[] { false, true })
            {
                var direction = outgoing ? CombatEventDirection.Outgoing : CombatEventDirection.Incoming;
                var lines = row.Lines
                    .Where(p => p.Direction == direction && !p.Expired)
                    .ToList();
                if (lines.Count == 0)
                {
                    continue;
                }

                var tone = ToneOf(row.Category, outgoing);
                var detail = string.Join("\n", lines.Select(p => p.Detail));

                PreviewOverlayChip chip;
                if (definition is { HasValue: false })
                {
                    // 反跳 / 干扰 / 网子…：挂着的一种状态，没有数值。
                    chip = new PreviewOverlayChip(
                        PreviewOverlayChipKind.State,
                        ShortLabel(row.Category),
                        Tone: tone,
                        Outgoing: outgoing);
                }
                else if (string.Equals(row.Category, CombatCategories.Rep, StringComparison.OrdinalIgnoreCase))
                {
                    chip = BuildRepChip(lines, definition, tone, outgoing);
                }
                else
                {
                    var line = lines[0];
                    var kind = string.Equals(row.Category, CombatCategories.Damage, StringComparison.OrdinalIgnoreCase)
                        ? PreviewOverlayChipKind.Damage
                        : PreviewOverlayChipKind.Value;
                    chip = new PreviewOverlayChip(
                        kind,
                        ShortLabel(row.Category),
                        FormatAmount(line.LastValue),
                        definition?.Unit ?? string.Empty,
                        FormatRate(line.Rate),
                        tone,
                        Outgoing: outgoing);
                }

                items.Add(new PreviewOverlayItem(
                    $"combat:{row.Category}" + (outgoing ? ".out" : ""),
                    row.Text,
                    SeverityOf(row.ColorKey),
                    OrderOf(row.Category, definition) + (outgoing ? 1 : 0),
                    detail,
                    row.ColorKey,
                    chip));
            }
        }

        return items;
    }

    /// <summary>
    /// 遥修聚合（阶段 382）：主值 = 各层最近值之和；速率 = 各层速率之和；
    /// 微条 = 各层速率占比（速率全 0 时退回最近值占比）；不足两层的非零层不画微条。
    /// </summary>
    private static PreviewOverlayChip BuildRepChip(
        IReadOnlyList<CombatLogLineSnapshot> lines,
        CombatCategoryDefinition? definition,
        string tone,
        bool outgoing)
    {
        double total = 0;
        var rateSum = 0d;
        foreach (var line in lines)
        {
            total += line.LastValue ?? 0;
            rateSum += line.Rate ?? 0;
        }

        var shares = LayerShares(lines, rateSum);
        return new PreviewOverlayChip(
            PreviewOverlayChipKind.Rep,
            ShortLabel(CombatCategories.Rep),
            FormatAmount(total),
            definition?.Unit ?? "HP",
            FormatRate(rateSum > 0 ? rateSum : null),
            tone,
            shares,
            Outgoing: outgoing);
    }

    /// <summary>把遥修的各层行折算成 盾 / 甲 / 构 三段占比；不足两个非零层时返回 null。</summary>
    private static IReadOnlyList<double>? LayerShares(IReadOnlyList<CombatLogLineSnapshot> incoming, double rateSum)
    {
        if (incoming.Count < 2)
        {
            return null;
        }

        var slots = new double[3];
        foreach (var line in incoming)
        {
            var slot = RepairSlotOf(line.SubTypeLabel);
            if (slot < 0)
            {
                continue;
            }

            slots[slot] += rateSum > 0 ? (line.Rate ?? 0) : (line.LastValue ?? 0);
        }

        var sum = slots[0] + slots[1] + slots[2];
        if (sum <= 0 || slots.Count(p => p > 0) < 2)
        {
            return null;
        }

        return new[] { slots[0] / sum, slots[1] / sum, slots[2] / sum };
    }

    /// <summary>遥修类型标签 → 微条段位（0 盾 / 1 甲 / 2 构）；认不出返回 -1。</summary>
    private static int RepairSlotOf(string? subTypeLabel)
    {
        if (string.IsNullOrWhiteSpace(subTypeLabel))
        {
            return -1;
        }

        if (string.Equals(subTypeLabel, LanguageService.Get("CombatLog_RepShield"), StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (string.Equals(subTypeLabel, LanguageService.Get("CombatLog_RepArmor"), StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (string.Equals(subTypeLabel, LanguageService.Get("CombatLog_RepHull"), StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return -1;
    }

    /// <summary>叠加层的数值排序：伤害最前，其次毁电 / 吸电 / 遥修 / 传电。</summary>
    private static int OrderOf(string category, CombatCategoryDefinition? definition) => category switch
    {
        CombatCategories.Damage => 110,
        CombatCategories.Neut => 120,
        CombatCategories.Nos => 130,
        CombatCategories.Rep => 140,
        CombatCategories.CapTransfer => 150,
        _ => 100 + (definition?.Priority ?? 0),
    };

    /// <summary>
    /// 胶囊配色语义（与日志监控一致：按"对我的收益 / 伤害"而不是方向）：
    /// 承受 = 红（电子战紫），输出 = 绿；遥修 / 传电给出去是中性白。
    /// </summary>
    private static string ToneOf(string category, bool outgoing)
    {
        if (outgoing)
        {
            return category is CombatCategories.Rep or CombatCategories.CapTransfer ? "neutral" : "benefit";
        }

        return category switch
        {
            CombatCategories.Scram => "harm",
            CombatCategories.Ecm
                or CombatCategories.Web
                or CombatCategories.Painter
                or CombatCategories.WeaponDisrupt
                or CombatCategories.SensorDamp
                or CombatCategories.EwOther => "ew",
            CombatCategories.Neut
                or CombatCategories.Nos
                or CombatCategories.Damage => "harm",
            CombatCategories.Rep
                or CombatCategories.CapTransfer => "benefit",
            _ => "neutral",
        };
    }

    /// <summary>叠加层上的短标签（2 字），完整分类名进悬停详情。</summary>
    private static string ShortLabel(string category) => LanguageService.Get(ShortLabelKey(category));

    private static string ShortLabelKey(string category) => category switch
    {
        CombatCategories.Scram => "CombatShort_Scram",
        CombatCategories.Ecm => "CombatShort_Ecm",
        CombatCategories.Web => "CombatShort_Web",
        CombatCategories.Painter => "CombatShort_Painter",
        CombatCategories.WeaponDisrupt => "CombatShort_WeaponDisrupt",
        CombatCategories.SensorDamp => "CombatShort_SensorDamp",
        CombatCategories.EwOther => "CombatShort_EwOther",
        CombatCategories.Neut => "CombatShort_Neut",
        CombatCategories.Nos => "CombatShort_Nos",
        CombatCategories.Rep => "CombatShort_Rep",
        CombatCategories.CapTransfer => "CombatShort_CapTransfer",
        CombatCategories.Damage => "CombatShort_Damage",
        _ => category,
    };

    private static string FormatAmount(double? value) => value is { } number ? number.ToString("0") : string.Empty;

    /// <summary>速率文字（每秒），不足两轮时为空——与状态中心的口径一致。</summary>
    private static string FormatRate(double? rate)
    {
        if (rate is not { } value || value <= 0)
        {
            return string.Empty;
        }

        var text = value >= 100 ? value.ToString("0") : value.ToString("0.#");
        return $"{text}/s";
    }

    private static PreviewOverlaySeverity SeverityOf(string colorKey) => colorKey switch
    {
        "danger" => PreviewOverlaySeverity.Critical,
        "warning" => PreviewOverlaySeverity.Caution,
        _ => PreviewOverlaySeverity.Normal,
    };
}
