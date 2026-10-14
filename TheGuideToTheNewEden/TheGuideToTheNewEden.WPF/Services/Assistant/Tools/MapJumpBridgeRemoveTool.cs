using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Map;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>删除一组玩家跳桥（写操作，需用户确认后执行）。</summary>
public sealed class MapJumpBridgeRemoveTool : ITool
{
    public string Name => "map_jumpbridge_remove";

    public string DisplayNameKey => "AssistantTool_map_jumpbridge_remove";

    public string Description =>
        "删除一组玩家跳桥。参数 systemA/systemB 用星系名（不区分两端顺序）。该操作会写入跳桥配置并影响寻路，" +
        "需要用户确认后才会执行。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["systemA"] = new JsonObject { ["type"] = "string", ["description"] = "跳桥一端的星系名" },
            ["systemB"] = new JsonObject { ["type"] = "string", ["description"] = "跳桥另一端的星系名" },
        },
        ["required"] = new JsonArray { "systemA", "systemB" },
    };

    public bool RequiresConfirmation => true;

    public bool IsAction => false;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var a = ToolArgs.ReadString(args, "systemA");
        var b = ToolArgs.ReadString(args, "systemB");
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
        {
            return Task.FromResult(new ToolResult("参数 systemA/systemB 不能为空。"));
        }

        var systemA = ToolNameResolver.ResolveSystem(a);
        var systemB = ToolNameResolver.ResolveSystem(b);
        if (systemA is null || systemB is null)
        {
            return Task.FromResult(new ToolResult(
                (systemA is null ? $"未找到星系「{a}」；" : string.Empty) + (systemB is null ? $"未找到星系「{b}」；" : string.Empty) + "未删除。"));
        }

        var removed = JumpBridgeSettingService.Remove(systemA.SolarSystemID, systemB.SolarSystemID);
        return Task.FromResult(removed
            ? new ToolResult($"已删除跳桥 {systemA.SolarSystemName} ↔ {systemB.SolarSystemName}。",
                ParamSummary: $"{systemA.SolarSystemName} ↔ {systemB.SolarSystemName}")
            : new ToolResult($"未找到跳桥 {systemA.SolarSystemName} ↔ {systemB.SolarSystemName}，无需删除。"));
    }
}
