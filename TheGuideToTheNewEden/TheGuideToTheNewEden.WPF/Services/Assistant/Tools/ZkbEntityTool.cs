using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.WPF.Services.KB;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>ZKB 实体统计：危险值、抱团比例、击杀/损失、ISK 击毁与损失、单挑战绩。</summary>
public sealed class ZkbEntityTool : ITool
{
    public string Name => "zkb_entity";

    public string DisplayNameKey => "AssistantTool_zkb_entity";

    public string Description =>
        "查询某个角色/军团/联盟在 ZKB（zkillboard）上的战斗统计：危险值（0–100）、群体活动比例（越高越爱抱团）、" +
        "击杀与损失数量、ISK 击毁与损失、单挑击杀/被单杀数。参数 entityName 用实体全名（同名多个时会列出候选让你确认）；" +
        "forceRefresh=true 跳过缓存重新拉取。适合「XX 是谁、危险吗」「这个团能打吗」「我最近战绩如何」。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["entityName"] = new JsonObject { ["type"] = "string", ["description"] = "实体全名（角色/军团/联盟）" },
            ["forceRefresh"] = new JsonObject { ["type"] = "boolean", ["description"] = "true = 跳过缓存重新拉取（默认 false）" },
        },
        ["required"] = new JsonArray { "entityName" },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var keyword = ToolArgs.ReadString(args, "entityName")?.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return new ToolResult("参数 entityName（实体名）不能为空。");
        }

        var candidates = await ZkbQueryService.SearchEntitiesAsync(keyword, ct);
        if (candidates is null || candidates.Count == 0)
        {
            return new ToolResult($"ZKB 上没有找到名为「{keyword}」的角色/军团/联盟。");
        }

        // 优先精确匹配；只有一个候选就用它；否则列出候选请模型转述给用户确认
        var match = candidates.FirstOrDefault(c => string.Equals(c.Name, keyword, StringComparison.OrdinalIgnoreCase))
                    ?? (candidates.Count == 1 ? candidates[0] : null);
        if (match is null)
        {
            var names = string.Join("、", candidates.Take(8).Select(c => $"{c.Name}（Id {c.Id}）"));
            return new ToolResult($"「{keyword}」匹配到 {candidates.Count} 个实体：{names}。请用完整名称再查一次。");
        }

        if (!ZkbMapping.TryToEntityType(match.GetCategory(), out var entityType))
        {
            return new ToolResult($"「{match.Name}」的类别（{match.GetCategory()}）没有 ZKB 统计。只支持角色/军团/联盟。");
        }

        var forceRefresh = ToolArgs.ReadBool(args, "forceRefresh", false);
        var statistic = await ZkbQueryService.GetStatisticAsync(entityType, match.Id, forceRefresh, ct);
        if (statistic is null)
        {
            return new ToolResult($"ZKB 查询「{match.Name}」失败（网络不可用或被限流）。");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"【{match.Name}】（{DescribeType(statistic.Type)}，Id {statistic.Id}）");
        builder.AppendLine($"- 危险值 {statistic.DangerRatio}/100｜群体活动比例 {statistic.GangRatio}%（越高越爱抱团）");
        builder.AppendLine($"- 击杀 {statistic.ItemDestroyed} 次（ISK {ToolArgs.FormatIsk(statistic.ISKDestroyed)}）｜损失 {statistic.ItemLost} 次（ISK {ToolArgs.FormatIsk(statistic.ISKLost)}）");
        builder.AppendLine($"- 单挑：击杀 {statistic.SoloKills}｜被单杀 {statistic.SoloLosses}");

        var ratio = statistic.ItemLost > 0
            ? (double)statistic.ItemDestroyed / statistic.ItemLost
            : (statistic.ItemDestroyed > 0 ? double.PositiveInfinity : 0);
        builder.Append(double.IsPositiveInfinity(ratio)
            ? "- 击杀/损失比：无损失记录"
            : $"- 击杀/损失比：{ratio:F2}");

        var efficiency = statistic.ISKDestroyed + statistic.ISKLost > 0
            ? (double)statistic.ISKDestroyed / (statistic.ISKDestroyed + statistic.ISKLost) * 100
            : 0;
        builder.AppendLine();
        builder.Append($"- ISK 效率：{efficiency:F1}%（击毁 ISK 占击毁+损失的比例）");

        return new ToolResult(builder.ToString(), ParamSummary: $"{match.Name} 危险值 {statistic.DangerRatio}");
    }

    private static string DescribeType(string? type) => type switch
    {
        "characterID" => "角色",
        "corporationID" => "军团",
        "allianceID" => "联盟",
        "factionID" => "势力",
        "shipTypeID" => "舰船",
        "groupID" => "舰船组",
        "solarSystemID" => "星系",
        "regionID" => "星域",
        _ => type ?? "实体",
    };
}
