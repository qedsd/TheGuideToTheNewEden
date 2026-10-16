using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Assistant.Esi;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>ESI 通用网关的说明书入口：给出某个操作的 path / 参数 / 请求体 / 权限 / 调用示例。</summary>
public sealed class EsiDescribeTool : ITool
{
    public string Name => "esi_describe";

    public string DisplayNameKey => "AssistantTool_esi_describe";

    public string Description =>
        "查看一个 ESI 操作的全部信息：路径与查询参数、请求体字段与枚举、所需权限、是否分页、调用示例。" +
        "本工具只读、不写盘。调用 esi_read / esi_write 前先看这里（字段或枚举写错会被拒 422），可先用 esi_search 检索。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["operation"] = new JsonObject { ["type"] = "string", ["description"] = "operationId 或 `GET /路径`，区分大小写" },
        },
        ["required"] = new JsonArray { "operation" },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        if (!EsiOperationIndex.IsReady)
        {
            return Task.FromResult(new ToolResult(
                $"ESI 操作索引不可用（{EsiOperationIndex.IndexPath} 缺失或损坏）。请运行 tools/esi/gen_esi_index.py 重新生成。"));
        }

        var key = ToolArgs.ReadString(args, "operation")?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult(new ToolResult("缺少参数 operation（operationId）。可以先用 esi_search 检索。"));
        }

        var operation = EsiOperationIndex.Find(key);
        if (operation is null)
        {
            return Task.FromResult(new ToolResult(
                $"没有找到操作「{key}」。请用 esi_search 检索（例如 keyword=\"wallet\"），"
                + "再用返回的 operationId 精确查询（operationId 区分大小写、必须与检索结果完全一致）。",
                ParamSummary: $"未知操作 {key}"));
        }

        return Task.FromResult(new ToolResult(EsiGateway.Describe(operation),
            ParamSummary: operation.Signature));
    }
}
