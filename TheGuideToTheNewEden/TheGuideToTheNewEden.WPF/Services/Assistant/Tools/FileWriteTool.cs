using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Assistant.Files;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 阶段 350：写入本地文本文件（**写操作，需确认**）。
/// <para>
/// 覆盖时旧内容自动备份为 <c>.bak</c>，写入走“临时文件 + 原子替换”；
/// 只允许设置里配置的根目录，可执行/数据库/压缩包等扩展名一律拒写。
/// </para>
/// </summary>
public sealed class FileWriteTool : ITool, IPendingSummary
{
    public string Name => "file_write";

    public string DisplayNameKey => "AssistantTool_file_write";

    public string Description =>
        "写入本地文本文件（需用户确认，仅限允许的根目录）。默认覆盖（旧内容备份为 .bak），" +
        "append=true 追加。单次上限约 200K 字符；目录不存在会自动创建。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["path"] = new JsonObject { ["type"] = "string", ["description"] = "文件路径（绝对路径或相对根目录）" },
            ["content"] = new JsonObject { ["type"] = "string", ["description"] = "要写入的文本内容" },
            ["append"] = new JsonObject { ["type"] = "boolean", ["description"] = "true = 追加到末尾；缺省覆盖" },
            ["reason"] = new JsonObject { ["type"] = "string", ["description"] = "为什么写（显示在确认卡片上）" },
        },
        ["required"] = new JsonArray { "path", "content" },
    };

    public bool RequiresConfirmation => true;

    public bool IsAction => false;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        if (!AiAssistantSettingService.FilesEnabled)
        {
            return Task.FromResult(new ToolResult("本地文件读写已在设置里关闭（Assistant.Files.Enabled=false）。"));
        }

        var (text, summary) = AssistantFileService.Write(
            ToolArgs.ReadString(args, "path"),
            ToolArgs.ReadString(args, "content"),
            ToolArgs.ReadBool(args, "append", false));
        return Task.FromResult(new ToolResult(text, ParamSummary: summary));
    }

    /// <summary>确认卡片上的人话摘要：写哪个文件、覆盖还是追加、多少字符、为什么。</summary>
    public string DescribePending(JsonElement args)
    {
        var path = ToolArgs.ReadString(args, "path") ?? string.Empty;
        var content = ToolArgs.ReadString(args, "content") ?? string.Empty;
        var append = ToolArgs.ReadBool(args, "append", false);
        var reason = ToolArgs.ReadString(args, "reason");

        var action = append ? "追加" : "写入";
        var suffix = string.IsNullOrWhiteSpace(reason) ? string.Empty : $"（原因：{reason.Trim()}）";
        return $"{action}文件 {path}：{content.Length} 字符{suffix}";
    }
}
