using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Tools;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>按名称检索 SDE 静态库（物品/星系/星域/空间站等）。</summary>
public sealed class SearchSdeTool : ITool
{
    public string Name => "search_sde";

    public string DisplayNameKey => "AssistantTool_search_sde";

    public string Description =>
        "按名称片段搜索本地游戏数据：物品、星系、星域、空间站，以及角色、军团、联盟；中英文均可，可限定只查一类，" +
        "返回名称、类别与 id，最多 25 条。适合「搜一下叫什么来着」「XX 在哪个星系」。只读、离线，不含市场行情。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["keyword"] = new JsonObject { ["type"] = "string", ["description"] = "名称片段（中英文均可）" },
            ["category"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray { "item", "solar_system", "region", "station" },
                ["description"] = "类别过滤（可选）",
            },
            ["limit"] = new JsonObject { ["type"] = "integer", ["description"] = "条数（默认 10，上限 25）" },
        },
        ["required"] = new JsonArray { "keyword" },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var keyword = ToolArgs.ReadString(args, "keyword");
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return Task.FromResult(new ToolResult("参数 keyword 不能为空。"));
        }

        var category = ToolArgs.ReadString(args, "category");
        var limit = Math.Clamp(ToolArgs.ReadInt(args, "limit", 10), 1, 25);
        var allowed = category switch
        {
            "item" => new HashSet<int> { (int)Core.DBModels.IdName.CategoryEnum.InventoryType },
            "solar_system" => new HashSet<int> { (int)Core.DBModels.IdName.CategoryEnum.SolarSystem },
            "region" => new HashSet<int> { (int)Core.DBModels.IdName.CategoryEnum.Region },
            "station" => new HashSet<int> { (int)Core.DBModels.IdName.CategoryEnum.Station },
            _ => null,
        };

        var matches = GameNameSearchService.Search(keyword, ct)
            .Where(p => allowed is null || allowed.Contains(p.Category))
            .Take(limit)
            .ToList();
        if (matches.Count == 0)
        {
            return Task.FromResult(new ToolResult($"没有找到与「{keyword}」匹配的名称。"));
        }

        var builder = new StringBuilder();
        foreach (var item in matches)
        {
            var categoryName = Enum.IsDefined(typeof(Core.DBModels.IdName.CategoryEnum), item.Category)
                ? ((Core.DBModels.IdName.CategoryEnum)item.Category).ToString()
                : item.Category.ToString();
            builder.AppendLine($"{item.Name}（{categoryName} id={item.Id}）");
        }

        return Task.FromResult(new ToolResult(builder.ToString().TrimEnd(), ParamSummary: $"「{keyword}」{matches.Count} 条"));
    }
}
