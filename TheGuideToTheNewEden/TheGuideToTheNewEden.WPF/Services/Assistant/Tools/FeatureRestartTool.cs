using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Assistant.Features;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 重启一个功能（阶段 155，**写操作，需确认**）：停掉正在运行的任务 → 重读配置 → 按需重新开始。
/// <para>
/// 有运行态的功能（如多开预览）由页面注册 <see cref="IAssistantFeatureRuntime"/>，重启会作用到真实对象；
/// 没有注册运行态时只重读配置，并在回执里说明"下次进入/开始时生效"，不假装重启了正在跑的任务。
/// </para>
/// </summary>
public sealed class FeatureRestartTool : ITool, IPendingSummary
{
    public string Name => "feature_restart";

    public string DisplayNameKey => "AssistantTool_feature_restart";

    public string Description =>
        "重启一个功能：会停止它正在运行的任务、重读配置并按需重新开始。需用户确认；功能名用 feature_list 查。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["name"] = new JsonObject { ["type"] = "string", ["description"] = "功能名（见 feature_list）" },
            ["reason"] = new JsonObject { ["type"] = "string", ["description"] = "为什么重启（显示在确认卡片）" },
        },
        ["required"] = new JsonArray { "name" },
    };

    public bool RequiresConfirmation => true;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        if (!AiAssistantSettingService.FeatureToolsEnabled)
        {
            return new ToolResult("功能重启已在设置里关闭（Assistant.Features.Enabled=false）。");
        }

        var name = ToolArgs.ReadString(args, "name");
        var entry = AssistantFeatureRegistry.Find(name);
        if (entry is null)
        {
            return new ToolResult($"未知功能「{name}」。可用 feature_list 查看允许重启的功能名。");
        }

        // 重启会碰预览窗口/页面状态：必须在 UI 线程发起
        Task<string>? task = null;
        ctx.OnUiThread(() => task = AssistantFeatureRegistry.RestartAsync(entry.Name));
        if (task is null)
        {
            return new ToolResult($"{entry.Name}：无法在 UI 线程执行重启。");
        }

        var text = await task;
        return new ToolResult(text, ParamSummary: $"重启 {entry.Name}");
    }

    public string DescribePending(JsonElement args)
    {
        var name = ToolArgs.ReadString(args, "name") ?? string.Empty;
        var entry = AssistantFeatureRegistry.Find(name);
        var reason = ToolArgs.ReadString(args, "reason");
        var suffix = string.IsNullOrWhiteSpace(reason) ? string.Empty : $"（原因：{reason.Trim()}）";
        return $"重启功能 {entry?.Name ?? name}（会停止它正在运行的任务）{suffix}";
    }
}
