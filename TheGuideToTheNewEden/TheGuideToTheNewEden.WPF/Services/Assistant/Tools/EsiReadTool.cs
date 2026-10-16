using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Assistant.Esi;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// ESI 通用网关的只读调用入口：直接执行任意"只读"ESI 操作（含 8 个查询型 POST，
/// 如 <c>/universe/names</c>），返回整形后的结果。写操作会被拒绝并让模型改用 <c>esi_write</c>。
/// </summary>
public sealed class EsiReadTool : ITool
{
    public string Name => "esi_read";

    public string DisplayNameKey => "AssistantTool_esi_read";

    public string Description =>
        "执行任意**只读** ESI 操作并返回整形结果；写操作会被拒绝并提示改用 esi_write（需用户确认）。" +
        "ID 类参数省略 = 默认角色；调用前建议先用 esi_describe 确认参数，返回里提示还有更多页时再继续取。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["operation"] = new JsonObject { ["type"] = "string", ["description"] = "esi_search 得到的 operationId" },
            ["path"] = new JsonObject { ["type"] = "object", ["description"] = "路径参数对象，ID 省略 = 默认角色" },
            ["query"] = new JsonObject { ["type"] = "object", ["description"] = "查询参数对象（如 page、type_id）" },
            ["page"] = new JsonObject { ["type"] = "integer", ["description"] = "页码（默认 1；也可放进 query）" },
            ["allPages"] = new JsonObject { ["type"] = "boolean", ["description"] = "true = 连续取多页（默认 1 页，上限见设置）" },
            ["characterName"] = new JsonObject { ["type"] = "string", ["description"] = "角色全名（省略 = 默认角色）" },
        },
        ["required"] = new JsonArray { "operation" },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var (operation, error) = EsiToolHelper.Resolve(args, requireWrite: false);
        if (error is not null)
        {
            return new ToolResult(error);
        }

        var text = await EsiGateway.InvokeAsync(operation!, ctx, args, allowWrite: false, ct);
        return new ToolResult(text, ParamSummary: operation!.Signature);
    }
}
