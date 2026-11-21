using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.CombatLog;

namespace TheGuideToTheNewEden.WPF.Services.GamePreview.Overlay;

/// <summary>
/// 战斗日志 provider（阶段 357）：把 <see cref="CombatLogStateCenter"/> 里该角色的分类行
/// （反跳 / 毁电 / 吸电 / 干扰 / 遥修 / 传电 / 承伤）接到预览窗叠加层。
/// <para>
/// 数据源依赖日志监控的"战斗日志"开关（与频道预警同模型）：没有该角色的战斗会话就没有行，
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
        Services.LanguageService.LanguageChanged += OnLanguageChanged;
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

        // 阶段 369/370：叠加层只看"输入"侧（别人打你/毁你/吸你/修你）且只显示未过期的行；
        // 遥修分行时把各类型的输入行拼在一条里（输出行不进叠加层）。
        var rows = CombatLogStateCenter.Current.GetRows(context.CharacterName)
            .Where(row => row.Lines.Any(p => p.Direction == Core.Models.CombatLog.CombatEventDirection.Incoming && !p.Expired))
            .ToList();
        if (rows.Count == 0)
        {
            return [];
        }

        return rows.Select(row =>
        {
            var text = string.Join(" · ", row.Lines
                .Where(p => p.Direction == Core.Models.CombatLog.CombatEventDirection.Incoming && !p.Expired)
                .Select(p => string.IsNullOrWhiteSpace(p.Text) ? row.Label : $"{row.Label} {p.Text}"));
            var detail = string.Join("\n", row.Lines
                .Where(p => p.Direction == Core.Models.CombatLog.CombatEventDirection.Incoming && !p.Expired)
                .Select(p => p.Detail));
            return new PreviewOverlayItem(
                $"combat:{row.Category}",
                text,
                SeverityOf(row.ColorKey),
                100 + row.Order,
                detail)
            {
                ColorKey = row.ColorKey,
            };
        }).ToList();
    }

    private static PreviewOverlaySeverity SeverityOf(string colorKey) => colorKey switch
    {
        "danger" => PreviewOverlaySeverity.Critical,
        "warning" => PreviewOverlaySeverity.Caution,
        _ => PreviewOverlaySeverity.Normal,
    };
}
