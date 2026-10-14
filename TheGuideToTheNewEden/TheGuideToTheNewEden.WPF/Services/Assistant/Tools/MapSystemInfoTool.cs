using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Services.Map;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>星系详情：安等/星域/跳桥/主权/资源行星。</summary>
public sealed class MapSystemInfoTool : ITool
{
    public string Name => "map_system_info";

    public string DisplayNameKey => "AssistantTool_map_system_info";

    public string Description =>
        "查询某个星系的详情：安全等级、所属星域、是否有玩家跳桥（到哪个星系）、主权归属（军团/联盟）、资源行星数。" +
        "参数 systemName 用星系名（如 吉他 / Jita）。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["systemName"] = new JsonObject { ["type"] = "string", ["description"] = "星系名（如 吉他 / Jita）" },
        },
        ["required"] = new JsonArray { "systemName" },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var systemName = ToolArgs.ReadString(args, "systemName");
        if (string.IsNullOrWhiteSpace(systemName))
        {
            return Task.FromResult(new ToolResult("参数 systemName 不能为空。"));
        }

        var system = ToolNameResolver.ResolveSystem(systemName);
        if (system is null)
        {
            return Task.FromResult(new ToolResult($"未找到星系「{systemName}」。"));
        }

        var builder = new StringBuilder();
        builder.Append($"{system.SolarSystemName} · 安等 {system.Security:F1}");

        var region = MapRegionService.Query(system.RegionID);
        if (region is not null)
        {
            builder.Append($" · 星域 {region.RegionName}");
        }

        builder.AppendLine();

        var bridgeTarget = JumpBridgeSettingService.GetValue(system.SolarSystemID, out var toSystemId)
            ? ToolNameResolver.ResolveSystemById(toSystemId) ?? toSystemId.ToString()
            : null;
        builder.AppendLine(bridgeTarget is null ? "跳桥：无" : $"跳桥：→ {bridgeTarget}");

        var sov = SovService.GetSovInfo(system.SolarSystemID);
        builder.AppendLine(sov is null ? "主权：无" : $"主权：{sov.AllianceName}");

        var resources = MapResourceService.GetSystemDetails(system.SolarSystemID);
        builder.AppendLine($"资源行星：{resources.Count(r => r.ContainResource)} 颗");

        return Task.FromResult(new ToolResult(builder.ToString().TrimEnd(), ParamSummary: system.SolarSystemName));
    }
}
