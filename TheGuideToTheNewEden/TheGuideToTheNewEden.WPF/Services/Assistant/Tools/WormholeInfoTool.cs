using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Models.Wormhole;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>虫洞资料：洞系等级/现象/固定与漫游洞口，或洞口（通往何处、寿命、质量上限）。</summary>
public sealed class WormholeInfoTool : ITool
{
    public string Name => "wormhole_info";

    public string DisplayNameKey => "AssistantTool_wormhole_info";

    public string Description =>
        "查本地虫洞库：洞系（等级、现象、固定与漫游洞口）或洞口（通往何处、寿命、质量上限与再生、重生时间）。" +
        "适合「J244 是什么洞」「哪个洞能通高安」「这洞过得去战列吗」。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["keyword"] = new JsonObject { ["type"] = "string", ["description"] = "洞名或洞口名关键词" },
            ["withStellar"] = new JsonObject { ["type"] = "boolean", ["description"] = "唯一命中洞系时带天体" },
            ["top"] = new JsonObject { ["type"] = "integer", ["description"] = "条数（默认 10，上限 30）" },
        },
        ["required"] = new JsonArray { "keyword" },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var keyword = ToolArgs.ReadString(args, "keyword")?.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return new ToolResult("参数 keyword（洞名或洞口名）不能为空。");
        }

        var top = Math.Clamp(ToolArgs.ReadInt(args, "top", 10), 1, 30);
        var withStellar = ToolArgs.ReadBool(args, "withStellar", false);

        var wormholes = await WormholeService.QueryWormholeAsync(keyword);
        var portals = await WormholeService.QueryPortalAsync(keyword);
        if ((wormholes is null || wormholes.Count == 0) && (portals is null || portals.Count == 0))
        {
            return new ToolResult($"虫洞库里没有匹配「{keyword}」的洞系或洞口。");
        }

        var builder = new StringBuilder();

        if (wormholes is { Count: > 0 })
        {
            builder.AppendLine($"洞系 {wormholes.Count} 条：");
            foreach (var wormhole in wormholes.Take(top))
            {
                builder.AppendLine(
                    $"- {wormhole.Name}｜等级 C{wormhole.Class}｜固定洞口 {WormholeFormat.DestinationCodesToText(wormhole.Statics)}｜" +
                    $"漫游洞口 {WormholeFormat.DestinationCodesToText(wormhole.Wanderings)}");
            }

            if (withStellar && wormholes.Count == 1)
            {
                var stellar = WormholeService.QueryWormholeStellar(wormholes[0].Id);
                if (stellar is { Count: > 0 })
                {
                    builder.AppendLine($"天体（{stellar.Count}）：{string.Join("、", stellar.Select(s => s.Name).Take(24))}");
                }
            }
        }

        if (portals is { Count: > 0 })
        {
            builder.AppendLine($"洞口 {portals.Count} 条：");
            foreach (var portal in portals.Take(top))
            {
                builder.AppendLine(
                    $"- {portal.Name} → {portal.DestinationName}（{portal.Destination}，Id {portal.Id}）｜出现于 {portal.AppearsIn}｜" +
                    $"寿命 {portal.Lifetime:0.#} 小时｜单次最大质量 {WormholeFormat.MassToText(portal.MaxMassPerJump)}｜" +
                    $"总质量 {WormholeFormat.MassToText(portal.TotalJumpMass)}｜质量再生 {WormholeFormat.MassToText(portal.MassRegen)}｜重生 {portal.Respawn}");
            }
        }

        return new ToolResult(builder.ToString().TrimEnd(), ParamSummary: $"洞系 {(wormholes?.Count ?? 0)} / 洞口 {(portals?.Count ?? 0)}");
    }
}
