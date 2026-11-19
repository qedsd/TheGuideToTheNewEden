using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.CombatLog;

namespace TheGuideToTheNewEden.WPF.Services.GamePreview.Overlay;

/// <summary>
/// 日志监控关键词命中 provider（阶段 357，可选）：把用户自己配置的关键词命中
/// （例如"诱导检测"）显示为预览窗叠加行。默认关闭，由多开全局设置里的开关控制。
/// </summary>
public sealed class GameLogMonitorOverlayProvider : IPreviewOverlayProvider
{
    public string Key => "monitor";

    public event Action<string?>? Changed;

    public GameLogMonitorOverlayProvider()
    {
        GameLogMonitorHitCenter.Current.Changed += OnChanged;
        Services.LanguageService.LanguageChanged += OnLanguageChanged;
    }

    private void OnChanged(string listener) => Changed?.Invoke(listener);

    private void OnLanguageChanged(object? sender, string language) => Changed?.Invoke(null);

    public IReadOnlyList<PreviewOverlayItem> GetItems(PreviewOverlayContext context)
    {
        if (!context.Global.ShowMonitorHits)
        {
            return [];
        }

        var rows = GameLogMonitorHitCenter.Current.GetRows(context.CharacterName);
        return rows.Select((row, index) => new PreviewOverlayItem(
            $"monitor:{row.ConfigName}:{index}",
            row.Text,
            PreviewOverlaySeverity.Normal,
            200 + index,
            row.Detail)).ToList();
    }
}
