using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>角色 LP（**只读**）：各 NPC 军团的忠诚点余额。</summary>
public sealed class CharacterLoyaltyTool : ITool
{
    public string Name => "character_loyalty";

    public string DisplayNameKey => "AssistantTool_character_loyalty";

    public string Description =>
        "查询某个角色的忠诚点（LP）：各 NPC 军团的 LP 余额，按余额降序。" +
        "参数 characterName 用角色全名（省略 = 默认角色）；keyword 按军团名过滤；top 控制条数（默认 15，上限 40）。" +
        "适合「我有多少 LP」「我在哪个团 LP 最多」。注意：本工具只给 LP 余额，" +
        "不查询 LP 商店可兑换物品（那需要额外的商店接口与价格比对）。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["characterName"] = new JsonObject { ["type"] = "string", ["description"] = "角色全名（省略 = 默认角色）" },
            ["keyword"] = new JsonObject { ["type"] = "string", ["description"] = "按军团名过滤" },
            ["top"] = new JsonObject { ["type"] = "integer", ["description"] = "条数（默认 15，上限 40）" },
        },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var (context, error) = await CharacterToolHelper.ResolveAsync(ctx, ToolArgs.ReadString(args, "characterName"), ct);
        if (context is null)
        {
            return new ToolResult(error!);
        }

        var keyword = ToolArgs.ReadString(args, "keyword")?.Trim();
        var top = Math.Clamp(ToolArgs.ReadInt(args, "top", 15), 1, 40);

        var loyalty = await CharacterEsiExtraService.GetLoyaltyAsync(context);
        if (loyalty is null)
        {
            return new ToolResult($"读取 {context.Name} 的 LP 失败（令牌失效或 ESI 不可用）。需要 esi-characters.read_loyalty.v1 权限。");
        }

        if (loyalty.Count == 0)
        {
            return new ToolResult($"{context.Name} 没有任何 LP（或该角色的 LP 权限未授权）。");
        }

        var visible = loyalty
            .Where(p => string.IsNullOrWhiteSpace(keyword)
                || (p.CorporationName ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.Points)
            .ToList();

        if (visible.Count == 0)
        {
            return new ToolResult($"{context.Name} 在 {loyalty.Count} 个军团有 LP，但没有军团名含「{keyword}」的。");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"{context.Name} 的 LP：{loyalty.Count} 个军团 · 总计 {loyalty.Sum(p => p.Points):N0} LP");

        foreach (var row in visible.Take(top))
        {
            builder.AppendLine($"- {row.CorporationName}：{row.Points:N0} LP");
        }

        if (visible.Count > top)
        {
            builder.AppendLine($"（另有 {visible.Count - top} 个军团未列出）");
        }

        return new ToolResult(builder.ToString().TrimEnd(),
            ParamSummary: $"{context.Name} · {visible.Count} 个军团有 LP");
    }
}
