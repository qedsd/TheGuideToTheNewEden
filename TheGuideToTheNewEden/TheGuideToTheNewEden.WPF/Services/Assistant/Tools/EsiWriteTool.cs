using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Assistant.Esi;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// ESI 通用网关的写操作入口：<see cref="RequiresConfirmation"/> = true，引擎会先挂起，
/// 由界面弹出确认卡片（卡片显示 <see cref="DescribePending"/> 给出的人话摘要），用户同意后才真正执行。
/// <para>
/// 只有索引里标为 <c>write</c> 的操作（非 GET，且不在"查询型 POST"白名单里）才允许走这里；
/// 26 个写操作覆盖邮件 / 联系人 / 装配 / 舰队 / 日历 / UI 窗口等。
/// </para>
/// </summary>
public sealed class EsiWriteTool : ITool, IPendingSummary
{
    public string Name => "esi_write";

    public string DisplayNameKey => "AssistantTool_esi_write";

    public string Description =>
        "执行一个写操作 ESI 端点（POST/PUT/DELETE）：发邮件、增删联系人、创建或删除装配、舰队动作、" +
        "回日历邀请、在游戏里开窗口。需要角色授权；会先弹确认卡片，用户拒绝则不执行。" +
        "只读端点请用 esi_read；字段拿不准先用 esi_describe 查。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["operation"] = new JsonObject { ["type"] = "string", ["description"] = "必须是写操作的 operationId" },
            ["path"] = new JsonObject { ["type"] = "object", ["description"] = "路径参数；ID 省略=当前角色/军团" },
            ["query"] = new JsonObject { ["type"] = "object", ["description"] = "查询参数；有些端点必填" },
            ["body"] = new JsonObject { ["type"] = "object", ["description"] = "请求体；数组型直接给数组" },
            ["characterName"] = new JsonObject { ["type"] = "string", ["description"] = "角色全名（省略 = 默认角色）" },
        },
        ["required"] = new JsonArray { "operation" },
    };

    public bool RequiresConfirmation => true;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var (operation, error) = EsiToolHelper.Resolve(args, requireWrite: true);
        if (error is not null)
        {
            return new ToolResult(error);
        }

        var text = await EsiGateway.InvokeAsync(operation!, ctx, args, allowWrite: true, ct);
        return new ToolResult(text, ParamSummary: operation!.Signature);
    }

    /// <summary>确认卡片上的人话摘要：<c>POST /path：字段摘要</c>。</summary>
    public string DescribePending(JsonElement args)
    {
        var operation = EsiOperationIndex.Find(ToolArgs.ReadString(args, "operation"));
        var builder = new StringBuilder(operation?.Signature ?? "未知 ESI 操作");
        var details = new List<string>();

        foreach (var key in new[] { "path", "query" })
        {
            if (args.ValueKind == JsonValueKind.Object
                && args.TryGetProperty(key, out var element)
                && element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    details.Add($"{property.Name}={Preview(property.Value)}");
                }
            }
        }

        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty("body", out var body)
            && body.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
        {
            details.Add($"body={EsiJsonShaper.Shape(body.GetRawText(), 200).Replace("\n", " ")}");
        }

        if (details.Count > 0)
        {
            builder.Append("：").Append(string.Join("；", details));
        }

        if (operation?.Summary is { Length: > 0 } summary)
        {
            builder.Append("（").Append(summary).Append('）');
        }

        return builder.ToString();
    }

    private static string Preview(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Array => $"[{string.Join(",", value.EnumerateArray().Take(8).Select(v => v.ToString()))}]",
        _ => value.ToString(),
    };
}
