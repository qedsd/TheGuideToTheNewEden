using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Links;
using TheGuideToTheNewEden.WPF.ViewModels.Links;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 快速链接检索（动作）：在本地链接库里按关键词搜索，并把结果**同时**跳转展示到「快速链接」页
/// （页面带搜索词打开，用户可直接点开）。
/// <para>
/// 匹配口径直接复用页面用的 <see cref="LinkItemViewModel.SearchText"/>（名称 / 简述 / 描述 / 地址 / 分类 / 平台 / 语言），
/// 不另写一套——否则会出现"助手说有一条、页面里搜不到"这种分叉。
/// <b>不代替用户打开浏览器</b>：开浏览器是副作用，帮助里给的是"跳到页面 + 把链接贴给你"。
/// </para>
/// </summary>
public sealed class LinksSearchTool : ITool
{
    public string Name => "links_search";

    public string DisplayNameKey => "AssistantTool_links_search";

    public string Description =>
        "在软件内置的「快速链接」库里检索 EVE 相关网站/工具（按名称、简介、描述、网址、分类、平台匹配），" +
        "并把结果跳转展示到「快速链接」页（搜索框已填好关键词）。参数 keyword 是搜索词（中英文均可，如「估价」「kb」「wormhole」）；" +
        "onlyFavorites=true 只搜收藏过的链接；top 控制返回条数（默认 10，上限 30）。" +
        "适合「有没有查价格的网站」「给我几个虫洞工具」；返回里会带上网址，需要实际打开时告诉用户去页面点开（助手不代开浏览器）。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["keyword"] = new JsonObject { ["type"] = "string", ["description"] = "搜索词（名称/简介/网址/分类等任意片段）" },
            ["onlyFavorites"] = new JsonObject { ["type"] = "boolean", ["description"] = "true = 只搜收藏过的（默认 false）" },
            ["top"] = new JsonObject { ["type"] = "integer", ["description"] = "返回条数（默认 10，上限 30）" },
        },
        ["required"] = new JsonArray { "keyword" },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => true;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var keyword = ToolArgs.ReadString(args, "keyword")?.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return Task.FromResult(new ToolResult("参数 keyword（搜索词）不能为空。"));
        }

        var onlyFavorites = ToolArgs.ReadBool(args, "onlyFavorites", false);
        var top = Math.Clamp(ToolArgs.ReadInt(args, "top", 10), 1, 30);

        var all = LinkStore.Load();
        if (all.Count == 0)
        {
            return Task.FromResult(new ToolResult("快速链接库为空（Configs/Links.json 不存在或读不出来），可在「快速链接」页恢复默认链接。"));
        }

        var favorites = onlyFavorites ? LinkStore.LoadFavorites() : null;
        var needle = keyword.ToLowerInvariant();

        var matched = new List<LinkItemViewModel>();
        foreach (var model in all)
        {
            var item = new LinkItemViewModel(model, favorites?.Contains(model.Url ?? string.Empty) == true);
            if (favorites is not null && !item.IsFavorite)
            {
                continue;
            }

            if (item.SearchText.Contains(needle))
            {
                matched.Add(item);
            }
        }

        if (matched.Count == 0)
        {
            return Task.FromResult(new ToolResult(
                $"链接库里没有匹配「{keyword}」的条目（共 {all.Count} 条）{(onlyFavorites ? "（当前只在收藏里找）" : string.Empty)}。" +
                "可以换个关键词（如「市场」「kb」「工业」），或提示用户用「快速链接」页的「恢复默认链接」。",
                NavigateAction: () => Navigation.NavigateToLinks(keyword),
                ParamSummary: keyword));
        }

        var builder = new StringBuilder();
        builder.AppendLine($"链接库命中 {matched.Count} 条（共 {all.Count} 条），显示前 {Math.Min(top, matched.Count)} 条（已打开快速链接页并填入该关键词）：");
        foreach (var item in matched.Take(top))
        {
            var summary = !string.IsNullOrWhiteSpace(item.ShortDescription)
                ? item.ShortDescription
                : ToolArgs.Truncate(item.Description, 60);
            builder.AppendLine($"- {item.Name}（{item.Model.GetCategories('/')}）：{summary}");
            builder.AppendLine($"  {item.Url}");
        }

        return Task.FromResult(new ToolResult(
            builder.ToString().TrimEnd(),
            NavigateAction: () => Navigation.NavigateToLinks(keyword),
            ParamSummary: $"{keyword} · {matched.Count} 条"));
    }
}
