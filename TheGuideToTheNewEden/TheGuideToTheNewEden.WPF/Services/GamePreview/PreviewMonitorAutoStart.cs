using TheGuideToTheNewEden.WPF.ViewModels.Channel;

namespace TheGuideToTheNewEden.WPF.Services.GamePreview;

/// <summary>
/// 阶段 390：开始多开预览时，按预览项的开关**尽力**自动拉起对应的频道预警 / 日志监控。
/// <para>
/// 两个功能页的会话由各自的 ViewModel 持有（会话字典已改为静态共享）；这里懒加载一个 VM，
/// 即使对应功能页从未打开过也能用：已运行直接返回，没运行则按角色名启动。
/// 找不到会话 / 日志文件时静默失败（只记日志），不影响预览本身。
/// </para>
/// </summary>
public static class PreviewMonitorAutoStart
{
    private static readonly object Locker = new();
    private static ChannelIntelViewModel? _intel;
    private static GameLogMonitorViewModel? _monitor;

    /// <summary>按预览项的状态尝试启动：频道预警 + 战斗日志（日志监控）。</summary>
    public static void TryStart(string? listener, bool intel, bool combat)
    {
        if (string.IsNullOrWhiteSpace(listener) || (!intel && !combat))
        {
            return;
        }

        if (intel)
        {
            var vm = GetIntel();
            _ = StartAsync(() => vm.EnsureIntelStartedForPreviewAsync(listener), $"频道预警（{listener}）");
        }

        if (combat)
        {
            var vm = GetMonitor();
            _ = StartAsync(() => vm.EnsureCombatStartedForPreviewAsync(listener), $"日志监控（{listener}）");
        }
    }

    private static ChannelIntelViewModel GetIntel()
    {
        lock (Locker)
        {
            return _intel ??= new ChannelIntelViewModel();
        }
    }

    private static GameLogMonitorViewModel GetMonitor()
    {
        lock (Locker)
        {
            return _monitor ??= new GameLogMonitorViewModel();
        }
    }

    private static async Task StartAsync(Func<Task<bool>> start, string what)
    {
        try
        {
            if (!await start())
            {
                Core.Log.Info($"多开预览：{what} 未能自动启动（没有对应的会话或日志文件）");
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }
}
