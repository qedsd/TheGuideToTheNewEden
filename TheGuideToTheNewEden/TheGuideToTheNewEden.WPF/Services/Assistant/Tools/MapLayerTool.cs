using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Views.UserControls.Map;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>星图图层控制（动作）：配色模式 + 跳桥层显示（未指定项保持当前值）。</summary>
public sealed class MapLayerTool : ITool
{
    public string Name => "map_layer";

    public string DisplayNameKey => "AssistantTool_map_layer";

    public string Description =>
        "切换星图的显示：配色（安等 / 主权 / 行星资源 / 击杀 / 跳数）与跳桥层开关；未指定的项保持当前值。" +
        "适合「星图切成主权视图」这类指令。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["colorMode"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray { "security", "sovereignty", "resource", "kills", "jumps" },
                ["description"] = "配色模式（可选）",
            },
            ["showBridges"] = new JsonObject { ["type"] = "boolean", ["description"] = "是否显示跳桥层（可选）" },
        },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => true;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var modeText = ToolArgs.ReadString(args, "colorMode");
        var showBridges = args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty("showBridges", out var bridgeNode)
            && bridgeNode.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? bridgeNode.GetBoolean()
            : (bool?)null;

        var colorMode = modeText switch
        {
            "sovereignty" => MapColorMode.Sovereignty,
            "resource" => MapColorMode.PlanetResource,
            "kills" => MapColorMode.Kills,
            "jumps" => MapColorMode.Jumps,
            "security" or "" => MapColorMode.Security,
            _ => (MapColorMode?)null,
        };
        if (modeText is not null && colorMode is null)
        {
            return Task.FromResult(new ToolResult($"未知配色模式「{modeText}」（可选 security/sovereignty/resource/kills/jumps）。"));
        }

        var builder = new StringBuilder();
        builder.Append("已切换星图：");
        if (colorMode is { } mode)
        {
            builder.Append($"配色={ModeName(mode)}");
        }

        if (showBridges is { } bridges)
        {
            if (colorMode is not null)
            {
                builder.Append(" · ");
            }

            builder.Append($"跳桥层={(bridges ? "开" : "关")}");
        }

        return Task.FromResult(new ToolResult(
            builder.ToString(),
            NavigateAction: () => Services.Navigation.NavigateToMap(new Services.Navigation.MapViewCommand
            {
                ColorMode = colorMode,
                ShowBridges = showBridges,
            }),
            ParamSummary: builder.ToString().Replace("已切换星图：", string.Empty)));
    }

    private static string ModeName(MapColorMode mode) => mode switch
    {
        MapColorMode.Sovereignty => "主权",
        MapColorMode.PlanetResource => "行星资源",
        MapColorMode.Kills => "击杀",
        MapColorMode.Jumps => "跳数",
        _ => "安等",
    };
}
