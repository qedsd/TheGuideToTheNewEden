using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Assistant.ConfigFiles;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 把功能配置回滚到一次自动备份（阶段 153，**写操作，需确认**）。
/// <para>
/// 备份由 <c>config_write</c> 在每次改动前自动生成（<c>Configs/.backups</c>，每份配置保留最近 20 份）；
/// 省略备份标识时用最新一份。回滚 <c>settings.json</c> 后会重新载入键值字典。
/// </para>
/// </summary>
public sealed class ConfigRestoreTool : ITool, IPendingSummary
{
    public string Name => "config_restore";

    public string DisplayNameKey => "AssistantTool_config_restore";

    public string Description =>
        "把功能配置回滚到自动备份：省略备份标识则用最新一份。写操作需确认；部分设置重启后生效。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["name"] = new JsonObject { ["type"] = "string", ["description"] = "配置逻辑名（见 config_list）" },
            ["backup"] = new JsonObject { ["type"] = "string", ["description"] = "备份标识（省略 = 最新一份）" },
        },
        ["required"] = new JsonArray { "name" },
    };

    public bool RequiresConfirmation => true;

    public bool IsAction => false;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        if (!AiAssistantSettingService.ConfigToolsEnabled)
        {
            return Task.FromResult(new ToolResult("配置读写已在设置里关闭（Assistant.Config.Enabled=false）。"));
        }

        var name = ToolArgs.ReadString(args, "name");
        var entry = AssistantConfigRegistry.Find(name);
        if (entry is null)
        {
            return Task.FromResult(new ToolResult(
                $"未知配置「{name}」。可用 config_list 查看允许访问的配置名（不接受文件路径）。"));
        }

        var result = AssistantConfigRegistry.Restore(entry, ToolArgs.ReadString(args, "backup"));
        return Task.FromResult(new ToolResult(result.Text, ParamSummary: result.Summary));
    }

    public string DescribePending(JsonElement args)
    {
        var name = ToolArgs.ReadString(args, "name") ?? string.Empty;
        var entry = AssistantConfigRegistry.Find(name);
        var backup = ToolArgs.ReadString(args, "backup");
        var target = string.IsNullOrWhiteSpace(backup) ? "最新一份备份" : backup.Trim();
        return $"回滚配置 {entry?.Name ?? name} 到 {target}";
    }
}
