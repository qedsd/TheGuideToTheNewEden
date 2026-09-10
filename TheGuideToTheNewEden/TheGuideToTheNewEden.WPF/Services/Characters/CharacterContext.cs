using EVEStandard;
using EVEStandard.Models.API;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.Core.Services;

namespace TheGuideToTheNewEden.WPF.Services.Characters;

/// <summary>
/// 单个角色的调用上下文：令牌 + ESI 客户端。
/// 取代 WinUI 版用 <c>object[] { api, characterData }</c> 传参的做法。
/// </summary>
public sealed class CharacterContext
{
    public CharacterContext(AuthorizedCharacterData character, EVEStandardAPI? api = null)
    {
        Character = character;
        Api = api ?? ESIService.GetDefaultESI();
    }

    public AuthorizedCharacterData Character { get; }

    public EVEStandardAPI Api { get; }

    public long CharacterId => Character.CharacterID;

    public AuthDTO Auth => Character.Auth;

    public string Name => Character.CharacterName;

    /// <summary>确保令牌可用；失败时返回 false（调用方据此展示"需要重新登录"）。</summary>
    public Task<bool> EnsureTokenValidAsync() => CharacterStore.EnsureTokenValidAsync(Character);
}