using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Map;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>列出当前配置的玩家跳桥。</summary>
public sealed class MapJumpBridgeListTool : ITool
{
    public string Name => "map_jumpbridge_list";

    public string DisplayNameKey => "AssistantTool_map_jumpbridge_list";

    public string Description => "列出软件里已配置的玩家跳桥（每行 星系A ↔ 星系B）。";

    public JsonObject? Parameters => null;

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var bridges = JumpBridgeSettingService.GetValue();
        if (bridges.Count == 0)
        {
            return Task.FromResult(new ToolResult("还没有配置任何玩家跳桥（可在星图 → 跳桥设置或让 AI 添加）。"));
        }

        var builder = new StringBuilder();
        foreach (var bridge in bridges)
        {
            var a = ToolNameResolver.ResolveSystemById(bridge.System1) ?? bridge.System1.ToString();
            var b = ToolNameResolver.ResolveSystemById(bridge.System2) ?? bridge.System2.ToString();
            builder.AppendLine($"{a} ↔ {b}");
        }

        return Task.FromResult(new ToolResult(builder.ToString().TrimEnd(), ParamSummary: $"{bridges.Count} 组跳桥"));
    }
}
