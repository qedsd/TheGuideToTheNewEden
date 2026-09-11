using System.Text.RegularExpressions;
using EVEStandard.Models.API;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WPF.Services.Characters;

/// <summary>总览页需要、但 <see cref="CharacterOverviewService"/> 未覆盖的补充信息。</summary>
public sealed class CharacterOverviewExtra
{
    /// <summary>角色当前舰船的个体名称（可被玩家重命名）。</summary>
    public string? ShipName { get; set; }

    public long SolarSystemId { get; set; }

    public string? SolarSystemName { get; set; }

    /// <summary>星系安全等级（-1.0 ~ 1.0）。</summary>
    public double? SecurityStatus { get; set; }

    /// <summary>空间站 / 建筑 ID（非星系 ID）。</summary>
    public long LocationId { get; set; }

    /// <summary>地点名称：空间站名或建筑名。</summary>
    public string? LocationName { get; set; }

    public DateTime UpdatedUtc { get; set; }
}

/// <summary>
/// 角色总览补充数据：舰船名、所在空间站/建筑、星系名与安全等级。
/// 与总览主服务同样带 TTL 缓存，避免切换标签反复请求 ESI。
/// </summary>
public static class CharacterOverviewExtraService
{
    private const string CacheKey = "overview-extra";

    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public static async Task<CharacterOverviewExtra?> GetAsync(CharacterContext context, bool forceRefresh = false)
    {
        if (!forceRefresh && CharacterCache.TryGet<CharacterOverviewExtra>(context.CharacterId, CacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        if (!await context.EnsureTokenValidAsync())
        {
            return null;
        }

        var auth = context.Auth;
        var extra = new CharacterOverviewExtra();

        // 当前舰船：总览服务只保留了船型，这里取个体舰船名。
        var ship = await Fetch(() => context.Api.Location.GetCurrentShipAsync(auth));
        if (ship is not null)
        {
            extra.ShipName = UnescapeShipName(ship.ShipName);
        }

        // 当前位置：星系 + 空间站/建筑。
        var location = await Fetch(() => context.Api.Location.GetCharacterLocationAsync(auth));
        if (location is not null)
        {
            extra.SolarSystemId = location.SolarSystemId;
            extra.LocationId = location.StationId ?? location.StructureId ?? 0;
        }

        if (extra.SolarSystemId > 0)
        {
            try
            {
                var system = await MapSolarSystemService.QueryAsync(extra.SolarSystemId);
                if (system is not null)
                {
                    extra.SolarSystemName = system.SolarSystemName;
                    extra.SecurityStatus = system.Security;
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        }

        if (extra.LocationId > 0)
        {
            extra.LocationName = await ResolveLocationNameAsync(context, auth, extra.LocationId, location?.StationId is not null);
        }

        extra.UpdatedUtc = DateTime.UtcNow;
        CharacterCache.Set(context.CharacterId, CacheKey, extra, Ttl);
        return extra;
    }

    /// <summary>空间站名走本地数据库；建筑名需要带令牌请求 ESI。</summary>
    private static async Task<string?> ResolveLocationNameAsync(CharacterContext context, AuthDTO auth, long locationId, bool isStation)
    {
        try
        {
            if (isStation)
            {
                var station = await StaStationService.QueryAsync(locationId);
                if (station is not null)
                {
                    return station.StationName;
                }
            }
            else
            {
                var structure = (await context.Api.Universe.GetStructureInfoAsync(auth, locationId)).Model;
                if (structure is not null)
                {
                    return structure.Name;
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        return null;
    }

    /// <summary>ESI 偶发返回 "u'...'" 形式的舰船名，还原成明文（与 WinUI 处理一致）。</summary>
    private static string? UnescapeShipName(string? shipName)
    {
        if (string.IsNullOrEmpty(shipName))
        {
            return shipName;
        }

        if (shipName.StartsWith("u'", StringComparison.Ordinal) && shipName.Length >= 3)
        {
            try
            {
                return Regex.Unescape(shipName.Substring(2, shipName.Length - 3));
            }
            catch
            {
                return shipName;
            }
        }

        return shipName;
    }

    /// <summary>单个接口失败不应让整页报错。</summary>
    private static async Task<TModel?> Fetch<TModel>(Func<Task<EVEStandard.Models.API.ESIModelDTO<TModel>>> call)
        where TModel : class
    {
        try
        {
            return (await call()).Model;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }
}
