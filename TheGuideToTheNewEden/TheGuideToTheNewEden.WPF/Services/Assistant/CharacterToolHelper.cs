using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.Assistant;

/// <summary>角色类工具共用：解析角色（空名=默认角色）+ 令牌校验。</summary>
public static class CharacterToolHelper
{
    /// <summary>返回 (上下文, 错误)；错误非空时直接返回给用户。</summary>
    public static async Task<(CharacterContext? Ctx, string? Error)> ResolveAsync(ToolContext ctx, string? name, CancellationToken ct)
    {
        var character = string.IsNullOrWhiteSpace(name)
            ? ctx.Character
            : ToolNameResolver.ResolveCharacter(name);
        if (character is null)
        {
            return (null, string.IsNullOrWhiteSpace(name)
                ? "未登录任何角色，请先在「角色」页添加角色（需要 EVE 授权）。"
                : $"未找到角色「{name}」。");
        }

        var characterCtx = new CharacterContext(character);
        if (!await characterCtx.EnsureTokenValidAsync().ConfigureAwait(false))
        {
            return (null, $"角色「{character.CharacterName}」令牌无效，请先在角色页重新授权。");
        }

        return (characterCtx, null);
    }
}
