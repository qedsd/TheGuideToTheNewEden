using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Map;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>角色当前所在星系（ESI 实时位置，一次性查询，不启动轮询）。</summary>
public sealed class CharacterLocationTool : ITool
{
    public string Name => "character_location";

    public string DisplayNameKey => "AssistantTool_character_location";

    public string Description =>
        "查询角色当前所在的星系（ESI 实时位置）。角色全名（省略 = 返回所有已授权角色的位置）。" +
        "结果含星系名、安全等级与星系 Id。适合「我在哪」「我的小号都在哪」。注意：位置取自 ESI，不是游戏日志，" +
        "角色刚跳跃时可能有一小段延迟。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["characterName"] = new JsonObject { ["type"] = "string", ["description"] = "角色全名（省略 = 所有已授权角色）" },
        },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var requestedName = ToolArgs.ReadString(args, "characterName");
        var locations = await CharacterLocationService.Current.QueryOnceAsync(ct);
        if (locations.Count == 0)
        {
            return new ToolResult("没有取到任何角色位置（没有已授权角色、令牌失效或 ESI 不可用/限流）。");
        }

        var matched = string.IsNullOrWhiteSpace(requestedName)
            ? locations
            : locations
                .Where(l => string.Equals(l.Character.CharacterName, requestedName.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (matched.Count == 0)
        {
            var known = string.Join("、", locations.Select(l => l.Character.CharacterName).Take(8));
            return new ToolResult($"没有名为「{requestedName}」的已授权角色。当前可查的角色：{known}。");
        }

        var builder = new StringBuilder();
        foreach (var location in matched)
        {
            var systemName = ToolNameResolver.ResolveSystemById(location.SystemId) ?? $"星系 {location.SystemId}";
            builder.AppendLine($"- {location.Character.CharacterName}：{systemName}（星系 Id {location.SystemId}）");
        }

        return new ToolResult(builder.ToString().TrimEnd(), ParamSummary: $"{matched.Count} 个角色位置");
    }
}
