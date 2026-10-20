using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Assistant.ConfigFiles;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 读取一个白名单里的功能配置文件（阶段 152）：密钥类键会被省略；过大的文件只返回结构摘要。
/// </summary>
public sealed class ConfigReadTool : ITool
{
    public string Name => "config_read";

    public string DisplayNameKey => "AssistantTool_config_read";

    public string Description =>
        "读取功能配置 JSON（密钥省略，大文件只给结构摘要）。改前先读；也可列出自动备份。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["name"] = new JsonObject { ["type"] = "string", ["description"] = "配置逻辑名（见 config_list）" },
            ["backups"] = new JsonObject { ["type"] = "boolean", ["description"] = "true = 只列出自动备份" },
        },
        ["required"] = new JsonArray { "name" },
    };

    public bool RequiresConfirmation => false;

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

        if (ToolArgs.ReadBool(args, "backups", false))
        {
            var backups = AssistantConfigRegistry.GetBackups(entry);
            if (backups.Count == 0)
            {
                return Task.FromResult(new ToolResult(
                    $"{entry.Name} 暂无自动备份（第一次写入前不会产生备份）。",
                    ParamSummary: $"列出 {entry.Name} 的备份"));
            }

            var text = $"{entry.Name} 的自动备份（新的在前）：\n"
                       + string.Join("\n", backups.Take(20).Select(backup => $"- {backup.FileName}（{backup.SizeBytes} 字节）"))
                       + "\n用 config_restore 并带上备份标识即可回滚（省略 = 最新一份）。";
            return Task.FromResult(new ToolResult(text, ParamSummary: $"列出 {entry.Name} 的备份"));
        }

        var result = AssistantConfigRegistry.Read(entry);
        return Task.FromResult(new ToolResult(result.Text, ParamSummary: $"读取配置 {entry.Name}"));
    }
}
