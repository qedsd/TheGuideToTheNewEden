using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Services.Business;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.Assistant;

/// <summary>
/// 名称 → ID 统一解析（唯一命中语义，防注入；未命中/歧义返回 null，由调用方给出明确原因）。
/// <para>
/// 遵循项目"解析成 ID、拒绝自由文本"的约定：物品走 <see cref="MarketTypeNameResolver"/>（自带唯一命中保护），
/// 星域/星系走本地 SDE 精确匹配。角色空名 = 第一个角色（与 OrderPage 的默认角色语义一致，避免 UI 线程 sync-over-async）。
/// </para>
/// </summary>
public static class ToolNameResolver
{
    /// <summary>物品名 → <see cref="InvType"/>（MarketTypeNameResolver 已做唯一命中保护）。</summary>
    public static InvType? ResolveItem(string? name)
        => string.IsNullOrWhiteSpace(name) ? null : MarketTypeNameResolver.ResolveOne(name.Trim());

    /// <summary>星域名 → <see cref="MapRegion"/>（忽略大小写精确匹配；唯一命中）。</summary>
    public static MapRegion? ResolveRegion(string? name)
    {
        var regionName = name?.Trim() ?? string.Empty;
        if (regionName.Length == 0)
        {
            return null;
        }

        var matches = MapRegionService.QueryAll()
            .Where(r => string.Equals(r.RegionName, regionName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    /// <summary>星系名 → <see cref="MapSolarSystem"/>（本地库精确匹配；唯一命中）。</summary>
    public static MapSolarSystem? ResolveSystem(string? name)
    {
        var systemName = name?.Trim() ?? string.Empty;
        if (systemName.Length == 0)
        {
            return null;
        }

        return MapSolarSystemService.Query(systemName);
    }

    /// <summary>星系 ID → 星系名（找不到返回 null，调用方回退显示 ID）。</summary>
    public static string? ResolveSystemById(int id)
        => MapSolarSystemService.Query(id)?.SolarSystemName;

    /// <summary>角色名 → <see cref="AuthorizedCharacterData"/>（空 = 第一个角色；忽略大小写全名匹配）。</summary>
    public static AuthorizedCharacterData? ResolveCharacter(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CharacterStore.Characters.FirstOrDefault();
        }

        var trimmed = name.Trim();
        return CharacterStore.Characters.FirstOrDefault(c =>
            string.Equals(c.CharacterName, trimmed, StringComparison.OrdinalIgnoreCase));
    }
}
