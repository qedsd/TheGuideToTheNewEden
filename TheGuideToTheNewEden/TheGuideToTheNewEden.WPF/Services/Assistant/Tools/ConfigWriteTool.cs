using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Assistant.ConfigFiles;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 写一个白名单里的功能配置文件（阶段 152，**写操作，需确认**）。
/// <para>
/// <c>patch</c>（顶层键合并）与 <c>content</c>（整体替换）二选一；写入前会做 JSON 校验、大小上限、
/// 密钥扫描与顶层类型一致性检查，成功后旧内容备份为同名 <c>.bak</c>。
/// <c>settings.json</c> 只允许 patch 且**只能改已存在、非密钥的键**。
/// </para>
/// </summary>
public sealed class ConfigWriteTool : ITool, IPendingSummary
{
    public string Name => "config_write";

    public string DisplayNameKey => "AssistantTool_config_write";

    public string Description =>
        "修改功能配置：只改指定顶层键，或整体替换 JSON。需用户确认；旧内容自动备份。" +
        "settings.json 只能改已存在的非密钥键。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["name"] = new JsonObject { ["type"] = "string", ["description"] = "配置逻辑名（见 config_list）" },
            ["patch"] = new JsonObject { ["type"] = "object", ["description"] = "顶层键合并；settings 仅支持此方式" },
            ["content"] = new JsonObject { ["type"] = "string", ["description"] = "完整 JSON；数组配置用，二者二选一" },
            ["reason"] = new JsonObject { ["type"] = "string", ["description"] = "为什么改（显示在确认卡片上）" },
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

        JsonObject? patch = null;
        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty("patch", out var patchElement)
            && patchElement.ValueKind == JsonValueKind.Object)
        {
            patch = JsonNode.Parse(patchElement.GetRawText()) as JsonObject;
        }

        var content = ToolArgs.ReadString(args, "content");
        var result = AssistantConfigRegistry.Write(entry, patch, content);
        return Task.FromResult(new ToolResult(result.Text, ParamSummary: result.Summary));
    }

    /// <summary>确认卡片上的人话摘要：写哪个文件、改哪些键、为什么。</summary>
    public string DescribePending(JsonElement args)
    {
        var name = ToolArgs.ReadString(args, "name") ?? string.Empty;
        var entry = AssistantConfigRegistry.Find(name);
        var what = "整体替换";
        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty("patch", out var patch)
            && patch.ValueKind == JsonValueKind.Object)
        {
            var keys = patch.EnumerateObject().Select(property => property.Name).Take(8).ToList();
            what = "修改 " + string.Join("、", keys);
        }

        var reason = ToolArgs.ReadString(args, "reason");
        var suffix = string.IsNullOrWhiteSpace(reason) ? string.Empty : $"（原因：{reason.Trim()}）";
        return $"写入配置 {entry?.Name ?? name}：{what}{suffix}";
    }
}
