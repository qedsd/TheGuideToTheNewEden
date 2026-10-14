using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Assistant;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>打开某个角色的工作区（动作）。</summary>
public sealed class CharacterNavigateTool : ITool
{
    public string Name => "character_navigate";

    public string DisplayNameKey => "AssistantTool_character_navigate";

    public string Description =>
        "打开软件的角色页并进入指定角色的工作区（配合「看看 X 的角色页」类问题使用）。参数 characterName 用角色全名；省略用默认角色。" +
        "找不到角色时返回原因、不跳转。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["characterName"] = new JsonObject { ["type"] = "string", ["description"] = "角色全名（省略 = 默认角色）" },
        },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => true;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var character = ToolNameResolver.ResolveCharacter(ToolArgs.ReadString(args, "characterName"));
        if (character is null)
        {
            return Task.FromResult(new ToolResult("未登录任何角色，请先在「角色」页添加角色（需要 EVE 授权）。"));
        }

        var id = character.CharacterID;
        return Task.FromResult(new ToolResult(
            $"已打开 {character.CharacterName} 的角色工作区。",
            NavigateAction: () => Navigation.NavigateToCharacter(id),
            ParamSummary: character.CharacterName));
    }
}
