using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Assistant.Esi;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// ESI 通用网关的检索入口：在 197 个 ESI 操作里找"能做这件事的端点"。
/// <para>
/// 三个元工具的分工：<c>esi_search</c>（找操作）→ <c>esi_describe</c>（看参数）→
/// <c>esi_read</c> / <c>esi_write</c>（调用）。这样把几百个端点的 schema 留在索引里按需取，
/// 而不是全部塞进提示词。
/// </para>
/// </summary>
public sealed class EsiSearchTool : ITool
{
    public string Name => "esi_search";

    public string DisplayNameKey => "AssistantTool_esi_search";

    public string Description =>
        "检索 ESI 官方接口的操作清单——没有对应的专用工具时用它。" +
        "返回 operationId、HTTP 方法与路径，再用 esi_describe 看参数、esi_read / esi_write 调用。" +
        "已按当前角色权限过滤，只读、较慢，缺权限的端点不会返回。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["keyword"] = new JsonObject { ["type"] = "string", ["description"] = "关键词，空格分词且逐词命中；留空 = 全部" },
            ["tag"] = new JsonObject { ["type"] = "string", ["description"] = "分组名精确过滤，如 Market" },
            ["limit"] = new JsonObject { ["type"] = "integer", ["description"] = "最多返回条数，默认 20 上限 60" },
        },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        if (!EsiOperationIndex.IsReady)
        {
            return Task.FromResult(new ToolResult(
                $"ESI 操作索引不可用（{EsiOperationIndex.IndexPath} 缺失或损坏）。"
                + "请在仓库里运行 tools/esi/gen_esi_index.py 重新生成，并重新构建程序。"));
        }

        var keyword = ToolArgs.ReadString(args, "keyword")?.Trim();
        var tag = ToolArgs.ReadString(args, "tag")?.Trim();
        var limit = Math.Clamp(ToolArgs.ReadInt(args, "limit", 20), 1, 60);
        var granted = EsiOperationIndex.GetGrantedScopes(ctx.Character);
        var filterByScope = granted is not null;

        if (string.IsNullOrWhiteSpace(keyword) && string.IsNullOrWhiteSpace(tag))
        {
            var builder = new StringBuilder();
            builder.AppendLine($"ESI 共 {EsiOperationIndex.All.Count} 个操作（索引规格 {EsiOperationIndex.File?.SpecVersion}，"
                               + $"生成于 {EsiOperationIndex.File?.GeneratedAt}）。分组与操作数：");
            foreach (var (name, count) in EsiOperationIndex.Tags())
            {
                builder.AppendLine($"- {name}（{count}）");
            }

            builder.Append("请给出 keyword 或 tag 再检索。");
            return Task.FromResult(new ToolResult(builder.ToString(),
                ParamSummary: "列出所有分组"));
        }

        var results = EsiOperationIndex.Search(keyword, tag, granted, filterByScope, limit);
        if (results.Count == 0)
        {
            var hint = EsiOperationIndex.Tags().Take(8).Select(t => t.Tag);
            return Task.FromResult(new ToolResult(
                $"没有匹配「{keyword ?? tag}」的操作{(filterByScope ? "（已按当前角色权限过滤：缺权限的端点不会出现）" : string.Empty)}。"
                + $"可用的分组例如：{string.Join("、", hint)}。换个关键词（英文名更容易命中）或按 tag 检索。",
                ParamSummary: $"检索「{keyword}/{tag}」：0 条"));
        }

        var text = new StringBuilder();
        text.AppendLine($"匹配 {results.Count} 条{(filterByScope ? "（已按当前角色权限过滤）" : "（未按权限过滤：该角色没有权限记录）")}：");
        foreach (var operation in results)
        {
            text.Append("- ").Append(operation.Id).Append(" · ").Append(operation.Signature);
            if (!string.IsNullOrWhiteSpace(operation.Summary))
            {
                text.Append(" · ").Append(operation.Summary);
            }

            if (operation.Write)
            {
                text.Append("［写操作］");
            }

            if (operation.Corp)
            {
                text.Append("［军团级，需职位］");
            }

            text.AppendLine();
        }

        text.Append("下一步：esi_describe 看参数，esi_read / esi_write 调用。");
        return Task.FromResult(new ToolResult(text.ToString().TrimEnd(),
            ParamSummary: $"检索「{keyword}{tag}」：{results.Count} 条"));
    }
}
