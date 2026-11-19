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

        var rows = CombatLogStateCenter.Current.GetRows(context.CharacterName);
        if (rows.Count == 0)
        {
            return [];
        }

        return rows.Select(row => new PreviewOverlayItem(
            $"combat:{row.Category}",
            row.Text,
            SeverityOf(row.ColorKey),
            100 + row.Order,
            row.Detail)
        {
            ColorKey = row.ColorKey,
        }).ToList();
    }

    private static PreviewOverlaySeverity SeverityOf(string colorKey) => colorKey switch
    {
        "danger" => PreviewOverlaySeverity.Critical,
        "warning" => PreviewOverlaySeverity.Caution,
        _ => PreviewOverlaySeverity.Normal,
    };
}
