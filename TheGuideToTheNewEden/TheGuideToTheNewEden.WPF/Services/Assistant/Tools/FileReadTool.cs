using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Assistant.Files;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 阶段 350：读取本地文本文件（**只读**）。
/// <para>
/// 超过 256KB 或二进制文件直接拒绝并给出替代做法（分段读 / file_list），避免把整份大文件塞进上下文。
/// </para>
/// </summary>
public sealed class FileReadTool : ITool
{
    public string Name => "file_read";

    public string DisplayNameKey => "AssistantTool_file_read";

    public string Description =>
        "读取本地文本文件（只读，仅限允许的根目录）。默认读前 200 行；大文件用 offset/limit 分段读。" +
        "二进制与超过 256KB 的文件会被拒绝。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["path"] = new JsonObject { ["type"] = "string", ["description"] = "文件路径（绝对路径或相对根目录）" },
            ["offset"] = new JsonObject { ["type"] = "integer", ["description"] = "从第几行开始（1 起，缺省 1）" },
            ["limit"] = new JsonObject { ["type"] = "integer", ["description"] = "最多读多少行（缺省 200，上限 2000）" },
            ["max_chars"] = new JsonObject { ["type"] = "integer", ["description"] = "返回内容的最大字符数（缺省 12000）" },
        },
        ["required"] = new JsonArray { "path" },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        if (!AiAssistantSettingService.FilesEnabled)
        {
            return Task.FromResult(new ToolResult("本地文件读写已在设置里关闭（Assistant.Files.Enabled=false）。"));
        }

        var (text, summary) = AssistantFileService.Read(
            ToolArgs.ReadString(args, "path"),
            ToolArgs.ReadInt(args, "offset", 1),
            ToolArgs.ReadInt(args, "limit", 200),
            ToolArgs.ReadInt(args, "max_chars", 12000));
        return Task.FromResult(new ToolResult(text, ParamSummary: summary));
    }
}
