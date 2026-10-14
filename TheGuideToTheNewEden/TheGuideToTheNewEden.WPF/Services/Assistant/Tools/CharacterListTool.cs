using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>列出已登录角色（角色名 + 服务器）。</summary>
public sealed class CharacterListTool : ITool
{
    public string Name => "character_list";

    public string DisplayNameKey => "AssistantTool_character_list";

    public string Description => "列出本软件里已登录的 EVE 角色（角色名）。没有角色时说明需要先添加角色。";

    public JsonObject? Parameters => null;

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var characters = CharacterStore.Characters;
        if (characters.Count == 0)
        {
            return Task.FromResult(new ToolResult("还没有登录任何角色，请先在「角色」页添加角色（需要 EVE 授权）。"));
        }

        var builder = new StringBuilder();
        foreach (var character in characters)
        {
            builder.AppendLine(character.CharacterName);
        }

        return Task.FromResult(new ToolResult(builder.ToString().TrimEnd(), ParamSummary: $"{characters.Count} 个角色"));
    }
}
