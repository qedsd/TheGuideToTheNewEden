using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>
/// 物品/实体图片地址（与 WinUI 版 <c>GameImageConverter</c> 规则一致）：
/// 国际服走 images.evetech.net（舰船与无人机用 render、其余用 icon），国服走 image.evepc.163.com。
/// </summary>
public static class GameImageHelper
{
    /// <summary>物品图片地址；ID 无效返回 null。</summary>
    public static string? BuildTypeImageUrl(long typeId, int size = 64)
    {
        if (typeId <= 0)
        {
            return null;
        }

        if (GameServerSelectorService.Value == GameServerType.Serenity)
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
}
