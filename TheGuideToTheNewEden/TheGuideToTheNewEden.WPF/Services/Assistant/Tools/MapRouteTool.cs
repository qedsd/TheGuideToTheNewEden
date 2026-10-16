using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.Core.EVEHelpers;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Map;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>查某几个星系间的最短路径并显示在星图上（星门 + 跳桥；唯一命中才画）。</summary>
public sealed class MapRouteTool : ITool
{
    public string Name => "map_route";

    public string DisplayNameKey => "AssistantTool_map_route";

    public string Description =>
        "计算两星系间最短路径并画到星图上：可走常规星门或旗舰跳跃，途经玩家跳桥，支持途经点与避让星系。" +
        "适合「怎么去某地」「两地多远」「规划路线」。返回跳数并切换星图视图；星系名唯一命中才画图，否则只说明原因。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["from"] = new JsonObject { ["type"] = "string", ["description"] = "起点星系名" },
            ["to"] = new JsonObject { ["type"] = "string", ["description"] = "终点星系名" },
            ["waypoints"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" }, ["description"] = "途经星系名（可选）" },
            ["navType"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "stargate", "capital" }, ["description"] = "路线类型（默认 stargate）" },
            ["avoid"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" }, ["description"] = "避开的星系名（可选）" },
            ["maxJumpLy"] = new JsonObject { ["type"] = "number", ["description"] = "旗舰最大跳距光年（capital 时默认 8）" },
        },
        ["required"] = new JsonArray { "from", "to" },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => true;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var from = ResolveRequired(args, "from");
        if (from.Error is not null)
        {
            return Task.FromResult(from.Error);
        }

        var to = ResolveRequired(args, "to");
        if (to.Error is not null)
        {
            return Task.FromResult(to.Error);
        }
        var waypointIds = ResolveOptionalArray(args, "waypoints", out var waypointError);
        if (waypointError is not null)
        {
            return Task.FromResult(waypointError);
        }

        var avoidIds = ResolveOptionalArray(args, "avoid", out _);
        var navType = ToolArgs.ReadString(args, "navType") ?? "stargate";
        var maxJumpLy = Math.Max(1, ToolArgs.ReadInt(args, "maxJumpLy", 8));

        var path = new List<int>();
        var waypointIndices = new List<int>();
        var points = new List<int> { from.Id };
        points.AddRange(waypointIds);
        points.Add(to.Id);

        for (var i = 0; i < points.Count - 1; i++)
        {
            var segment = navType == "capital"
                ? ShortestPathHelper.CalCapitalJumpPath(points[i], points[i + 1], maxJumpLy, true, avoidIds, 0)
                : ShortestPathHelper.CalStargatePath(points[i], points[i + 1], avoidIds, JumpBridgeSettingService.GetBridgesDict());

            if (segment is not { Count: > 1 })
            {
                var name = ToolNameResolver.ResolveSystemById(points[i]) ?? points[i].ToString();
                return Task.FromResult(new ToolResult(
                    $"无法从 {name} 计算到下一段的路径（{navType} 路线不可达或避让阻断）。未画图。"));
            }

            if (path.Count == 0)
            {
                path.AddRange(segment);
            }
            else
            {
                path.AddRange(segment.Skip(1));   // 去掉与前一段重合的衔接点
            }

            // 除最后一段外，每段终点是途经点
            if (i < points.Count - 2)
            {
                waypointIndices.Add(path.Count - 1);
            }
        }

        // 拼接路径上的星系名（沿 path 查名）
        var names = string.Join(" → ", path.Select(id => ToolNameResolver.ResolveSystemById(id) ?? id.ToString()));
        var bridgeHops = CountBridgeHops(path);
        var text = $"路径：{names}\n跳数 {path.Count - 1} · 途经跳桥 {bridgeHops} 处 · 已在地图上标出";

        return Task.FromResult(new ToolResult(
            text,
            NavigateAction: () => Services.Navigation.NavigateToMap(new Services.Navigation.MapViewCommand
            {
                Route = path,
                Waypoints = waypointIndices,
                CenterSystemId = from.Id,
            }),
            ParamSummary: $"{from.Name} → {to.Name}"));
    }

    /// <summary>解析必填星系名参数：返回 (Id, Name, Error)，Error 非空表示失败（缺参或未找到）。</summary>
    private static (int Id, string Name, ToolResult? Error) ResolveRequired(JsonElement args, string name)
    {
        var value = ToolArgs.ReadString(args, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return (0, string.Empty, new ToolResult($"参数 {name} 不能为空。"));
        }

        var system = ToolNameResolver.ResolveSystem(value);
        return system is null
            ? (0, string.Empty, new ToolResult($"未找到星系「{value}」；为避免画错路径，未画图。"))
            : (system.SolarSystemID, system.SolarSystemName, null);
    }

    private static List<int> ResolveOptionalArray(JsonElement args, string name, out ToolResult? error)
    {
        error = null;
        var list = new List<int>();
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var system = ToolNameResolver.ResolveSystem(item.GetString());
            if (system is null)
            {
                error = new ToolResult($"未找到星系「{item.GetString()}」；为避免画错路径，未画图。");
                return list;
            }

            list.Add(system.SolarSystemID);
        }

        return list;
    }

    /// <summary>统计路径中"跳过普通边"的跳桥段数：path[i]→path[i+1] 若在跳桥字典里则算一跳桥。</summary>
    private static int CountBridgeHops(IReadOnlyList<int> path)
    {
        var bridges = JumpBridgeSettingService.GetBridgesDict();
        var count = 0;
        for (var i = 0; i + 1 < path.Count; i++)
        {
            if (bridges.TryGetValue(path[i], out var next) && next == path[i + 1])
            {
                count++;
            }
        }

        return count;
    }
}
