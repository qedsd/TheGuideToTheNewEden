using TheGuideToTheNewEden.WPF.Services.Business;
using TheGuideToTheNewEden.WPF.Services.ChannelIntel;
using TheGuideToTheNewEden.WPF.Services.GamePreview;
using TheGuideToTheNewEden.WPF.Services.KB;

namespace TheGuideToTheNewEden.WPF.Services.Home;

/// <summary>一个功能的运行态（用于主页磁贴角标）。</summary>
/// <param name="Count">涉及的实例数（角色数 / 窗口数 / 收藏数）；0 表示"只在跑、没有数量概念"。</param>
public sealed record HomeFeatureStatus(int Count);

/// <summary>
/// 主页磁贴角标的数据源：只暴露<b>各处已经存在的运行态</b>，不新增任何后台任务。
/// <list type="bullet">
///   <item><c>channel-intel</c>：<see cref="IntelWarningService.ListenerCount"/>（几个角色开了预警）；</item>
///   <item><c>channel-monitor</c>：<see cref="ChannelMonitorNotifyService.ItemCount"/>（几个角色在监控）；</item>
///   <item><c>game-preview</c>：<see cref="PreviewWindowManager.TotalRunningCount"/>（几个预览窗口）；</item>
///   <item><c>zkb</c>：<see cref="ZkbKillStreamHub.IsRunning"/>（击杀流已连接，无数量）；</item>
///   <item><c>market</c>：收藏物品数（&gt;0 才显示角标）。</item>
/// </list>
/// 取不到 / 没在跑一律返回 null（不显示角标）。
/// </summary>
public static class HomeFeatureStatusService
{
    public static HomeFeatureStatus? Get(string featureId) => featureId switch
    {
        "channel-intel" => Positive(IntelWarningService.Current.ListenerCount),
        "channel-monitor" => Positive(ChannelMonitorNotifyService.Current.ItemCount),
        "game-preview" => Positive(PreviewWindowManager.TotalRunningCount),
        "zkb" => ZkbKillStreamHub.Current.IsRunning ? new HomeFeatureStatus(0) : null,
        "market" => Positive(MarketStarService.Current.GetIds().Count),
        _ => null,
    };

    private static HomeFeatureStatus? Positive(int count) => count > 0 ? new HomeFeatureStatus(count) : null;
}
