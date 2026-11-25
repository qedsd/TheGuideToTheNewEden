using TheGuideToTheNewEden.WPF.Services.GamePreview.Overlay;

namespace TheGuideToTheNewEden.WPF.Services.ChannelIntel;

/// <summary>
/// 叠加层的第一个 provider（阶段 355）：把"该角色当前活跃的频道预警"变成两行信息——
/// <list type="bullet">
/// <item><b>最近</b>：活跃预警里跳数最小的那条（离角色最近的威胁）；</item>
/// <item><b>最新</b>：最近一次收到的那条（最新情报）。</item>
/// </list>
/// 两条指向同一星系时合并成一行，避免重复。
/// </summary>
public sealed class ChannelIntelOverlayProvider : IPreviewOverlayProvider
{
    /// <summary>与设置项、日志对应的稳定标识。</summary>
    public const string ProviderKey = "intel";

    public string Key => ProviderKey;

    public event Action<string?>? Changed;

    public ChannelIntelOverlayProvider()
    {
        var center = ChannelIntelAlertCenter.Current;
        center.Changed += OnCenterChanged;
        center.ListenersChanged += OnCenterListenersChanged;
        center.TimeTick += OnCenterTimeTick;
        LanguageService.LanguageChanged += OnLanguageChanged;
    }

    public IReadOnlyList<PreviewOverlayItem> GetItems(PreviewOverlayContext context)
    {
        // 全局总开关关闭时覆盖所有单项设置
        if (!context.Global.ShowIntelAlert || !context.Item.ShowIntelAlert)
        {
            return [];
        }

        var name = context.CharacterName;
        if (string.IsNullOrWhiteSpace(name))
        {
            return [];
        }

        var center = ChannelIntelAlertCenter.Current;
        if (!center.IsListening(name))
        {
            return [];
        }

        var nearest = center.GetNearest(name);
        var latest = center.GetLatest(name);
        if (nearest is null && latest is null)
        {
            return [];
        }

        var now = DateTime.Now;

        if (nearest is not null
            && latest is not null
            && nearest.SolarSystemId == latest.SolarSystemId)
        {
            // 同一条既是最近也是最新：合并成一枚胶囊
            return
            [
                new PreviewOverlayItem(
                    "intel.both",
                    LanguageService.Format(
                        "GamePreview_OverlayBoth",
                        latest.Jumps,
                        ElapsedMinutes(now, latest.LastSeenAt)),
                    Severity(latest, isNearest: true),
                    Order: 0,
                    Detail: BuildDetail(latest, now),
                    Chip: BuildChip("GamePreview_OverlayTagBoth", latest, now)),
            ];
        }

        var rows = new List<PreviewOverlayItem>(2);
        if (nearest is not null)
        {
            rows.Add(new PreviewOverlayItem(
                "intel.nearest",
                LanguageService.Format(
                    "GamePreview_OverlayNearest",
                    nearest.Jumps,
                    ElapsedMinutes(now, nearest.LastSeenAt)),
                Severity(nearest, isNearest: true),
                Order: 0,
                Detail: BuildDetail(nearest, now),
                Chip: BuildChip("GamePreview_OverlayTagNearest", nearest, now)));
        }

        if (latest is not null)
        {
            rows.Add(new PreviewOverlayItem(
                "intel.latest",
                LanguageService.Format(
                    "GamePreview_OverlayLatest",
                    latest.Jumps,
                    ElapsedMinutes(now, latest.LastSeenAt)),
                Severity(latest, isNearest: false),
                Order: 1,
                Detail: BuildDetail(latest, now),
                Chip: BuildChip("GamePreview_OverlayTagLatest", latest, now)));
        }

        return rows;
    }

    /// <summary>
    /// 预警胶囊（阶段 382）：标签（最近 / 最新 / 最近最新）+ 跳数 + 「跳 · Nmin」。
    /// 配色交给用户设置（<c>Tone = "alert"</c>）；降级时整枚变淡。
    /// </summary>
    private static PreviewOverlayChip BuildChip(string tagKey, ChannelIntelAlertInfo alert, DateTime now)
        => new(
            PreviewOverlayChipKind.Intel,
            LanguageService.Get(tagKey),
            alert.Jumps.ToString(),
            LanguageService.Get("EarlyWarningPage_Jumps"),
            $"{ElapsedMinutes(now, alert.LastSeenAt)}min",
            "alert",
            Dimmed: alert.Downgraded);

    /// <summary>
    /// 悬停详情：第一行给系统 / 跳数 / 时间，第二行给来源频道 + 发言人 + 原文。
    /// 多行文本，叠加窗按原样折行显示。
    /// </summary>
    private static string BuildDetail(ChannelIntelAlertInfo alert, DateTime now)
    {
        var jumpsUnit = LanguageService.Get("EarlyWarningPage_Jumps");
        var head = $"{alert.SolarSystemName} · {alert.Jumps} {jumpsUnit} · {ElapsedMinutes(now, alert.LastSeenAt)}min";

        var channel = string.Equals(alert.Channel, "KB", StringComparison.OrdinalIgnoreCase)
            ? LanguageService.Get("IntelWindow_KB")
            : alert.Channel;
        var speaker = alert.Content.SpeakerName;
        var body = string.IsNullOrWhiteSpace(speaker)
            ? alert.Content.Content
            : $"{speaker}: {alert.Content.Content}";

        var source = string.IsNullOrWhiteSpace(channel) ? body : $"{channel} · {body}";
        return string.IsNullOrWhiteSpace(source) ? head : $"{head}\n{source}";
    }

    /// <summary>多久以前收到（分钟，向下取整；至少 0）。</summary>
    private static int ElapsedMinutes(DateTime now, DateTime lastSeenAt)
    {
        var minutes = (int)Math.Floor((now - lastSeenAt).TotalMinutes);
        return Math.Max(0, minutes);
    }

    /// <summary>着色：最近＝危险红，最新＝提示橙；已降级的预警降为普通色（与星图"降级仍保留"一致）。</summary>
    private static PreviewOverlaySeverity Severity(ChannelIntelAlertInfo alert, bool isNearest)
    {
        if (alert.Downgraded)
        {
            return PreviewOverlaySeverity.Normal;
        }

        return isNearest ? PreviewOverlaySeverity.Critical : PreviewOverlaySeverity.Caution;
    }

    private void OnCenterChanged(object? sender, ChannelIntelAlertChange change)
        => Changed?.Invoke(change.Listener);

    private void OnCenterListenersChanged(object? sender, EventArgs e)
        => Changed?.Invoke(null);

    /// <summary>心跳：只通知"有活跃预警"的角色，用于刷新"Nmin"。</summary>
    private void OnCenterTimeTick(object? sender, EventArgs e)
    {
        foreach (var listener in ChannelIntelAlertCenter.Current.ActiveListeners)
        {
            Changed?.Invoke(listener);
        }
    }

    private void OnLanguageChanged(object? sender, string language)
        => Changed?.Invoke(null);
}
