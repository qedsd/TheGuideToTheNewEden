using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>
/// 物品/实体图片地址（与 WinUI 版 <c>GameImageConverter</c> 规则一致）：
/// 国际服走 images.evetech.net（舰船与无人机用 render、其余用 icon），国服走 image.evepc.163.com。
/// </summary>
public static class GameImageHelper
{
    /// <summary>
    /// 物品图片地址缓存：图标地址只由 (服务器, 类型ID, 尺寸) 决定，永不变化。
    /// 不缓存的话，击杀列表/星图舰船图标等高频路径每次都要查一次本地库（物品 → 市场分组），
    /// 在后台线程与 UI 线程并发时会撞出 Microsoft.Data.Sqlite 的 Close() NRE（阶段 63 实机）。
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(int Server, long TypeId, int Size), string?> TypeUrlCache = new();

    /// <summary>物品图片地址；ID 无效返回 null。</summary>
    public static string? BuildTypeImageUrl(long typeId, int size = 64)
    {
        if (typeId <= 0)
        {
            return null;
        }

        var server = (int)GameServerSelectorService.Value;
        return TypeUrlCache.GetOrAdd((server, typeId, size), static key => BuildTypeImageUrlCore(key.TypeId, key.Size, (GameServerType)key.Server));
    }

    private static string? BuildTypeImageUrlCore(long typeId, int size, GameServerType server)
    {
        if (server == GameServerType.Serenity)
        {
            return $"https://image.evepc.163.com/types/{typeId}_{size}.png";
        }

        try
        {
            // 舰船（4）与无人机（157）用 render 更好看，其余用 icon（与 WinUI 一致）
            var rootGroup = Core.Services.DB.InvMarketGroupService.QueryRootGroupOfType(typeId);
            if (rootGroup is 4 or 157)
            {
                return $"https://images.evetech.net/types/{typeId}/render?size={size}";
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        return $"https://images.evetech.net/types/{typeId}/icon?size={size}";
    }

    /// <summary>角色头像地址（国服走 evepc 的 characters，其余走 evetech 的 portrait）。</summary>
    public static string? BuildCharacterPortraitUrl(long characterId, int size = 64)
    {
        if (characterId <= 0)
        {
            return null;
        }

        return GameServerSelectorService.Value == GameServerType.Serenity
            ? $"https://image.evepc.163.com/characters/{characterId}_{size}.jpg"
            : $"https://images.evetech.net/characters/{characterId}/portrait?size={size}";
    }

    /// <summary>军团徽标地址。</summary>
    public static string? BuildCorporationLogoUrl(long corporationId, int size = 64)
    {
        if (corporationId <= 0)
        {
            return null;
        }

        return GameServerSelectorService.Value == GameServerType.Serenity
            ? $"https://image.evepc.163.com/corporations/{corporationId}_{size}.png"
            : $"https://images.evetech.net/corporations/{corporationId}/logo?size={size}";
    }

    /// <summary>联盟徽标地址。</summary>
    public static string? BuildAllianceLogoUrl(long allianceId, int size = 64)
    {
        if (allianceId <= 0)
        {
            return null;
        }

        return GameServerSelectorService.Value == GameServerType.Serenity
            ? $"https://image.evepc.163.com/alliances/{allianceId}_{size}.png"
            : $"https://images.evetech.net/alliances/{allianceId}/logo?size={size}";
    }

    /// <summary>
    /// 按实体类别分派的统一入口：角色→头像，军团/联盟→徽标，物品→物品图标；
    /// 其余类别（星系/星座/星域/结构等）没有公开图片，返回 null。
    /// </summary>
    public static string? BuildEntityImageUrl(IdName.CategoryEnum category, long id, int size = 64) => category switch
    {
        IdName.CategoryEnum.Character => BuildCharacterPortraitUrl(id, size),
        IdName.CategoryEnum.Corporation => BuildCorporationLogoUrl(id, size),
        IdName.CategoryEnum.Alliance => BuildAllianceLogoUrl(id, size),
        IdName.CategoryEnum.InventoryType => BuildTypeImageUrl(id, size),
        _ => null,
    };
}

