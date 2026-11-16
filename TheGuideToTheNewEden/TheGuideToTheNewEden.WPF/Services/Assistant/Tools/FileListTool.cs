using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Assistant.Files;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 阶段 350：列出本地目录（**只读**）。只允许访问设置里配置的根目录，默认就是应用数据目录。
/// </summary>
public sealed class FileListTool : ITool
{
    public string Name => "file_list";

    public string DisplayNameKey => "AssistantTool_file_list";

    public string Description =>
        "列出本地目录里的文件（只读，仅限允许的根目录）。path 可相对根目录；pattern 支持 *.txt 之类。" +
        "先用它确认文件是否存在、多大，再决定 file_read 的 offset/limit。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["path"] = new JsonObject { ["type"] = "string", ["description"] = "目录路径（绝对路径或相对根目录）" },
            ["pattern"] = new JsonObject { ["type"] = "string", ["description"] = "通配符，缺省 *" },
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

        var (text, summary) = AssistantFileService.List(
            ToolArgs.ReadString(args, "path"),
            ToolArgs.ReadString(args, "pattern"));
        return Task.FromResult(new ToolResult(text, ParamSummary: summary));
    }
}
