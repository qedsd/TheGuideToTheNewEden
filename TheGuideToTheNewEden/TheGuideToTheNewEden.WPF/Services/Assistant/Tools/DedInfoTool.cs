using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 战斗站点资料：按关键词 / 势力 / 分类 / 难度查本站的死亡远征库（<c>CombatSite</c> 表，379 条）。
/// <para>
/// 相对旧版（只会按势力 + 等级查 <c>DED</c> 表）多了 <b>分类</b>：战斗异常、化学实验室、战斗遗迹、三神裔、特殊站点
/// 现在都能查了。等级标签也改成了三态——评级复合体给 <c>6/10</c>、战斗异常给 <c>T8</c>、舰队集结点按玩家习惯给 <c>9/10</c>
/// （CONCORD 其实没有 9/10 这一档）。
/// </para>
/// </summary>
public sealed class DedInfoTool : ITool
{
    /// <summary>势力别名 → 索引（与页面下拉顺序一致）。</summary>
    private static readonly (string[] Aliases, int Index, string Label)[] Factions =
    [
        (["无人机", "drone", "rogue"], 0, "无人机"),
        (["天蛇", "serpentis"], 1, "天蛇"),
        (["血袭者", "blood"], 2, "血袭者"),
        (["萨沙", "sansha"], 3, "萨沙"),
        (["天使", "angel"], 4, "天使"),
        (["古斯塔斯", "guristas", "gurista"], 5, "古斯塔斯"),
    ];

    /// <summary>分类别名 → <see cref="CombatSiteCategory"/> 整数值。</summary>
    private static readonly (string[] Aliases, int Index, string Label)[] Categories =
    [
        (["评级", "ded", "rated"], (int)CombatSiteCategory.DedRated, "DED 评级复合体"),
        (["无评级", "unrated"], (int)CombatSiteCategory.Unrated, "无评级复合体"),
        (["远征", "expedition", "escalation"], (int)CombatSiteCategory.Expedition, "远征"),
        (["异常", "anomaly", "anomalies"], (int)CombatSiteCategory.Anomaly, "战斗异常"),
        (["化学", "chemical", "lab"], (int)CombatSiteCategory.ChemicalLab, "化学实验室"),
        (["遗迹", "relic", "ruins"], (int)CombatSiteCategory.RelicSite, "战斗遗迹"),
        (["三神裔", "triglavian", "pochven"], (int)CombatSiteCategory.Triglavian, "三神裔站点"),
        (["特殊", "special"], (int)CombatSiteCategory.Special, "特殊站点"),
        (["旗舰", "capital"], (int)CombatSiteCategory.CapitalExpedition, "旗舰远征"),
    ];

    public string Name => "ded_info";

    public string DisplayNameKey => "AssistantTool_ded_info";

    public string Description =>
        "查询本站的战斗站点（死亡远征）资料库：DED 评级复合体、无评级复合体、远征、旗舰远征、战斗异常、化学实验室、遗迹、三神裔。"
        + "可按关键词（中英文站名）、势力、分类、评级/难度档查询，返回标题、类别、难度标签与正文要点。"
        + "适合「DED 6/10 怎么打」「天使有哪些异常」「古斯塔斯 10/10 的要点」「天蛇集结点怎么走」「旗舰远征有哪些」。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["keyword"] = new JsonObject { ["type"] = "string", ["description"] = "站名关键词（中英文均可）" },
            ["faction"] = new JsonObject { ["type"] = "string", ["description"] = "势力：无人机/天蛇/血袭者/萨沙/天使/古斯塔斯" },
            ["category"] = new JsonObject { ["type"] = "string", ["description"] = "分类：评级/无评级/远征/异常/化学/遗迹/三神裔/特殊" },
            ["level"] = new JsonObject { ["type"] = "integer", ["description"] = "DED 评级 1-10（如 6 即 6/10）" },
            ["tier"] = new JsonObject { ["type"] = "integer", ["description"] = "战斗异常难度档 1-10" },
            ["top"] = new JsonObject { ["type"] = "integer", ["description"] = "条数（默认 15，上限 40）" },
        },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var keyword = ToolArgs.ReadString(args, "keyword")?.Trim();
        var factionText = ToolArgs.ReadString(args, "faction")?.Trim();
        var categoryText = ToolArgs.ReadString(args, "category")?.Trim();
        var level = ToolArgs.ReadInt(args, "level", 0);
        var tier = ToolArgs.ReadInt(args, "tier", 0);
        var top = Math.Clamp(ToolArgs.ReadInt(args, "top", 15), 1, 40);

        var faction = string.IsNullOrWhiteSpace(factionText) ? CombatSiteService.AnyFaction : Resolve(factionText, Factions);
        if (!string.IsNullOrWhiteSpace(factionText) && faction < 0)
        {
            return new ToolResult($"不认识的势力「{factionText}」。支持：{string.Join("、", Factions.Select(f => f.Label))}。");
        }

        var category = string.IsNullOrWhiteSpace(categoryText) ? CombatSiteService.AnyCategory : Resolve(categoryText, Categories);
        if (!string.IsNullOrWhiteSpace(categoryText) && category < 0)
        {
            return new ToolResult($"不认识的分类「{categoryText}」。支持：{string.Join("、", Categories.Select(f => f.Label))}。");
        }

        var rating = level > 0 ? level : -1;
        if (string.IsNullOrWhiteSpace(keyword) && faction < 0 && category < 0 && rating < 0 && tier <= 0)
        {
            return new ToolResult("请至少给一个条件：keyword（站名）/ faction（势力）/ category（分类）/ level（评级）/ tier（异常档位）。");
        }

        var list = await CombatSiteService.QueryAsync(category, faction, rating, tier, keyword);
        if (list.Count == 0)
        {
            return new ToolResult("没有找到匹配的战斗站点（本地库为空或条件太窄）。");
        }

        var scope = BuildScope(keyword, faction, category, level, tier);
        var builder = new StringBuilder();
        builder.AppendLine($"{scope} 命中 {list.Count} 条，显示前 {Math.Min(top, list.Count)} 条：");
        foreach (var site in list.Take(top))
        {
            builder.AppendLine($"- 【{TagOf(site)}】{site.TitleCN}（{site.TitleEN}，Id {site.Id}）");

            var meta = new List<string> { CategoryName(site.Category) };
            if (faction >= 0 && site.Faction >= 0)
            {
                meta.Add(Factions.First(f => f.Index == site.Faction).Label);
            }

            if (!string.IsNullOrWhiteSpace(site.ShipLimit))
            {
                meta.Add("最大船型 " + site.ShipLimit);
            }

            if (!string.IsNullOrWhiteSpace(site.Location))
            {
                meta.Add(site.Location);
            }

            builder.AppendLine("  " + string.Join(" · ", meta));

            if (!string.IsNullOrWhiteSpace(site.Escalation))
            {
                builder.AppendLine("  可升级为：" + site.Escalation);
            }

            if (!string.IsNullOrWhiteSpace(site.Source))
            {
                builder.AppendLine("  触发来源：" + site.Source);
            }

            var body = !string.IsNullOrWhiteSpace(site.CommunityContent) ? site.CommunityContent : site.WikiContent;
            if (!string.IsNullOrWhiteSpace(body))
            {
                builder.AppendLine("  " + ToolArgs.Truncate(body.Replace("\r", " ").Replace("\n", " "), 220));
            }
        }

        return new ToolResult(builder.ToString().TrimEnd(), ParamSummary: $"{scope} {list.Count} 条");
    }

    private static string BuildScope(string? keyword, int faction, int category, int level, int tier)
    {
        var bits = new List<string>();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            bits.Add($"关键词「{keyword}」");
        }

        if (category >= 0)
        {
            bits.Add(Categories.First(c => c.Index == category).Label);
        }

        if (faction >= 0)
        {
            bits.Add(Factions.First(f => f.Index == faction).Label + " 势力");
        }

        if (level > 0)
        {
            bits.Add($"{level}/10");
        }

        if (tier > 0)
        {
            bits.Add($"T{tier}");
        }

        return bits.Count > 0 ? string.Join(" ", bits) : "全部站点";
    }

    /// <summary>难度标签三态：评级 <c>6/10</c>、异常 <c>T8</c>、其它给分类名。</summary>
    private static string TagOf(CombatSite site)
    {
        return (CombatSiteCategory)site.Category switch
        {
            CombatSiteCategory.DedRated => $"{site.Rating}/10",
            // 舰队集结点在库里有意记 Rating = 9：玩家习惯按 9/10 叫，不是数据错误。
            CombatSiteCategory.Unrated => site.Rating > 0 ? $"{site.Rating}/10" : "未定级",
            CombatSiteCategory.Anomaly => $"T{site.Tier}" + (site.Level > 0 ? $"·{site.Level}" : string.Empty),
            CombatSiteCategory.CapitalExpedition => "旗舰",
            _ => CategoryName(site.Category),
        };
    }

    private static string CategoryName(int category)
    {
        foreach (var (_, index, label) in Categories)
        {
            if (index == category)
            {
                return label;
            }
        }

        return category.ToString();
    }

    private static int Resolve(string text, (string[] Aliases, int Index, string Label)[] table)
    {
        var lower = text.Trim().ToLowerInvariant();
        foreach (var (aliases, index, _) in table)
        {
            if (aliases.Any(a => lower.Contains(a, StringComparison.OrdinalIgnoreCase)))
            {
                return index;
            }
        }

        return -1;
    }
}
