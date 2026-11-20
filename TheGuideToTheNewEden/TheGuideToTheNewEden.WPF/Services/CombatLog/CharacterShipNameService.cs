using System.Collections.Concurrent;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.CombatLog;

/// <summary>
/// 阶段 359：取角色**当前舰船类型名**（日志里没有自己的船型，只能走 ESI）。
/// <para>
/// 用已授权的角色令牌调 <c>GET /characters/{id}/ship</c>，再经本地 SDE 翻译成当前语言的类型名；
/// 结果按角色缓存 2 分钟（失败也缓存 30 秒，避免窗口刷新时反复请求）。
/// 未授权 / 令牌失效 / 无权限时返回 null，调用方隐藏标题里的舰船即可。
/// </para>
/// </summary>
public static class CharacterShipNameService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan FailTtl = TimeSpan.FromSeconds(30);
    private static readonly ConcurrentDictionary<string, (string? Name, DateTime At, bool Ok)> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static async Task<string?> GetAsync(string? characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName))
        {
            return null;
        }

        if (Cache.TryGetValue(characterName, out var cached))
        {
            var age = DateTime.UtcNow - cached.At;
            if (age < (cached.Ok ? Ttl : FailTtl))
            {
                return cached.Name;
            }
        }

        try
        {
            var character = CharacterStore.Characters.FirstOrDefault(p =>
                string.Equals(p.CharacterName, characterName, StringComparison.OrdinalIgnoreCase));
            if (character is null)
            {
                Cache[characterName] = (null, DateTime.UtcNow, false);
                return null;
            }

            var context = new CharacterContext(character);
            if (!await context.EnsureTokenValidAsync())
            {
                Cache[characterName] = (null, DateTime.UtcNow, false);
                return null;
            }

            var ship = await context.Api.Location.GetCurrentShipAsync(context.Auth);
            var type = ship?.Model is { } model ? InvTypeService.QueryType(model.ShipTypeId)?.TypeName : null;
            Cache[characterName] = (type, DateTime.UtcNow, !string.IsNullOrWhiteSpace(type));
            return type;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            Cache[characterName] = (null, DateTime.UtcNow, false);
            return null;
        }
    }
}
