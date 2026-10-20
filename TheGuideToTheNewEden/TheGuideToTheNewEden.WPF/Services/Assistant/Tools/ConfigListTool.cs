using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Assistant.ConfigFiles;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 列出 AI 可读写的功能配置文件（阶段 152）：逻辑名 + 文件 + 是否可写 + 用途。
/// 改配置前先用它找目标，再用 <c>config_read</c> 看现状。
/// </summary>
public sealed class ConfigListTool : ITool
{
    public string Name => "config_list";

    public string DisplayNameKey => "AssistantTool_config_list";

    public string Description =>
        "列出可读写的功能配置文件（名称/文件/可写/用途）。改配置前先确认目标；仅白名单文件可访问。";

    public JsonObject? Parameters => null;

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
        => Task.FromResult(new ToolResult(
            AiAssistantSettingService.ConfigToolsEnabled
                ? AssistantConfigRegistry.ListText()
                : "配置读写已在设置里关闭（Assistant.Config.Enabled=false）。"));
}
