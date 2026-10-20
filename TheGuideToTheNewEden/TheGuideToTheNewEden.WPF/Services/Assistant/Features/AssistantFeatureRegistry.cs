using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Features;

/// <summary>
/// AI 可重启的功能清单（阶段 155）。
/// <para>
/// **重启语义**：优先调用页面/VM 注册的 <see cref="IAssistantFeatureRuntime"/>——它们能停掉正在运行的
/// 预览窗口/会话、重读配置并按需重新开始；没有注册运行态时退回"只重读配置，下次进入/开始时生效"。
/// 这样接口对每个功能都存在，但不会假装重启了没有运行的任务。
/// </para>
/// </summary>
public static class AssistantFeatureRegistry
{
    /// <summary>一个可重启功能。</summary>
    public sealed record Entry(
        string Name,
        string Description,
        string? ConfigName,
        string Capability,
        Func<Task<string>> Fallback);

    /// <summary>全部条目（顺序即展示顺序）。</summary>
    public static IReadOnlyList<Entry> Entries { get; } =
    [
        new("gamepreview", "多开预览（停止现有浮窗 → 重读配置 → 按需重新开始）", "gamepreview", "有运行态",
            RestartGamePreviewFallback),
        new("settings", "共用键值设置（重读 settings.json、AI 助手与翻译设置）", "settings", "重读设置",
            RestartSettingsAsync),
        new("translation", "AI 翻译/术语设置（重读模型与翻译参数）", "settings", "重读设置",
            RestartTranslationAsync),
        new("channelintel", "频道预警（停止全部会话 → 重读设置 → 重开原本在跑的角色）", "intel", "有运行态", NoRuntime("频道预警")),
        new("channelmonitor", "频道监控（停止全部会话 → 重读设置 → 重开原本在跑的角色）", "channelmonitor", "有运行态", NoRuntime("频道监控")),
        new("channelscan", "频道统计（一次性计算，无长驻任务）", "channelscan", "无长驻任务", NoRuntime("频道统计")),
        new("channelmarket", "频道查价（停止全部会话 → 重读设置 → 重开原本在跑的角色）", "channelmarket", "有运行态", NoRuntime("频道查价")),
        new("channeltranslation", "频道翻译（停止全部会话 → 重读设置 → 重开原本在跑的角色）", "channeltranslation", "有运行态", NoRuntime("频道翻译")),
        new("gamelog", "日志监控（停止全部会话 → 重读设置 → 重开原本在跑的配置）", "gamelog", "有运行态", NoRuntime("日志监控")),
        new("map", "星图（配置在下次进入星图页时读取）", "map", "无长驻任务", NoRuntime("星图")),
        new("scalper", "倒货（下次进入倒货页时读取配置）", "scalper", "无长驻任务", NoRuntime("倒货")),
        new("appraisal", "估价（下次进入估价页时读取配置）", "appraisal", "无长驻任务", NoRuntime("估价")),
        new("market", "市场（下次进入市场页时读取设置）", null, "无长驻任务", NoRuntime("市场")),
        new("links", "快速链接（下次进入链接页时读取）", "links", "无长驻任务", NoRuntime("快速链接")),
        new("home", "主页快捷方式（下次进入主页时读取）", "homeshortcuts", "无长驻任务", NoRuntime("主页")),
        new("evesettings", "EVE 配置同步设置（下次进入该页时读取）", "evesettings", "无长驻任务", NoRuntime("配置同步")),
    ];

    public static Entry? Find(string? name)
        => Entries.FirstOrDefault(entry => string.Equals(entry.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>给模型看的列表：名称、能力、配置、当前是否有运行态。</summary>
    public static string ListText()
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("可重启的功能（name：说明；关联配置；当前状态）：");
        foreach (var entry in Entries)
        {
            var live = AssistantFeatureRuntime.IsLive(entry.Name) ? "运行态在线" : "未注册运行态";
            var config = entry.ConfigName is null ? "无独立配置" : $"配置 {entry.ConfigName}";
            builder.AppendLine($"- {entry.Name}（{entry.Capability}；{config}；{live}）：{entry.Description}");
        }

        builder.AppendLine("用 feature_restart 带 name 执行；重启需用户确认。");
        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// 重启：优先用已注册的运行态；没有则执行条目的回退动作（通常是重读配置）。
    /// </summary>
    public static async Task<string> RestartAsync(string? name)
    {
        var entry = Find(name);
        if (entry is null)
        {
            return $"未知功能「{name}」。可用 feature_list 查看允许重启的功能名。";
        }

        if (AssistantFeatureRuntime.TryGet(entry.Name, out var runtime))
        {
            try
            {
                return await runtime.RestartAsync();
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                return $"{entry.Name} 重启失败：{ex.GetType().Name}: {ex.Message}";
            }
        }

        try
        {
            return await entry.Fallback();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return $"{entry.Name} 重读配置失败：{ex.GetType().Name}: {ex.Message}";
        }
    }

    // ------------------------------------------------------------------
    //  各功能的回退动作（没有运行态时）
    // ------------------------------------------------------------------

    private static Task<string> RestartGamePreviewFallback()
    {
        GamePreviewSettingService.Current.CancelPendingSave();
        GamePreviewSettingService.Current.ReloadFromDisk();
        return Task.FromResult("已重新读取 GamePreviewSetting.json；多开页面未打开，没有运行中的预览需要重启。");
    }

    private static Task<string> RestartSettingsAsync()
    {
        SettingsService.Initialize();
        AiAssistantSettingService.Initialize();
        TranslationSettingService.Initialize();
        return Task.FromResult(
            "已重新读取 settings.json（键值、AI 助手与翻译设置）。主题/语言/快捷键等启动期设置需重启应用后完全生效。");
    }

    private static Task<string> RestartTranslationAsync()
    {
        TranslationSettingService.Initialize();
        return Task.FromResult("已重新读取 AI 翻译与模型设置；下一句翻译会使用新参数。");
    }

    /// <summary>没有注册运行态时的诚实回执：不假装重启了正在跑的任务。</summary>
    private static Func<Task<string>> NoRuntime(string displayName)
        => () => Task.FromResult(
            $"{displayName} 当前没有已注册的运行态（页面可能未打开）。配置改动会在下次进入页面/开始任务时生效；"
            + "如果它正在运行，请先在对应页面停止再开始。");
}
