using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Assistant.Features;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>列出可被 AI 重启的功能（阶段 155）。</summary>
public sealed class FeatureListTool : ITool
{
    public string Name => "feature_list";

    public string DisplayNameKey => "AssistantTool_feature_list";

    public string Description =>
        "列出可重启的功能（说明、关联配置、是否有运行态）。重启会停止该功能正在运行的任务。";

    public JsonObject? Parameters => null;

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
        => Task.FromResult(new ToolResult(
            AiAssistantSettingService.FeatureToolsEnabled
                ? AssistantFeatureRegistry.ListText()
                : "功能重启已在设置里关闭（Assistant.Features.Enabled=false）。"));
}
