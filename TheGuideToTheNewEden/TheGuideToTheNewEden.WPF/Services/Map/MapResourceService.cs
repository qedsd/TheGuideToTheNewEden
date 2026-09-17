using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.PlanetResources;

namespace TheGuideToTheNewEden.WPF.Services.Map;

/// <summary>行星资源种类（星图着色与清单页共用）。</summary>
public enum ResourceKind
{
    Power,
    Workforce,
    MagmaticGas,
    SuperionicIce,
}

/// <summary>
/// 星图行星资源数据：本地库 <c>planetResources</c> 的按星系/按星域聚合 + 设施升级表（UpgradeResources.csv）。
/// <para>
/// 只统计 <c>Security &lt;= 0</c> 的星系（00 地区才有行星资源，与 WinUI 口径一致）。
/// 首次访问时懒加载并缓存；<see cref="IsLoaded"/> 可用于决定要不要先 <see cref="LoadAsync"/>。
/// </para>
/// </summary>
public static class MapResourceService
{
    /// <summary>岩浆气资源类型 ID（本地库约定）。</summary>
    public const int MagmaticGasTypeId = 81143;

    /// <summary>超离子冰资源类型 ID（本地库约定）。</summary>
    public const int SuperionicIceTypeId = 81144;

    private static readonly object Locker = new();
    private static Dictionary<int, SolarSystemResources> _systemResources = [];
    private static Dictionary<int, RegionResources> _regionResources = [];
    private static List<Upgrade> _upgrades = [];

    public static bool IsLoaded { get; private set; }

    /// <summary>清掉聚合缓存（"重新载入"用；下次 <see cref="LoadAsync"/> 会重新查库）。</summary>
    public static void ClearCache()
    {
        _systemResources = [];
        _regionResources = [];
        _upgrades = [];
        lock (Locker)
        {
            IsLoaded = false;
        }
    }

    public static Dictionary<int, SolarSystemResources> SystemResources => _systemResources;

    public static Dictionary<int, RegionResources> RegionResources => _regionResources;

    /// <summary>设施升级表（本地 csv，失败返回空表）。</summary>
    public static List<Upgrade> GetUpgrades() => _upgrades ?? [];

    /// <summary>按资源种类取星系资源值。</summary>
    public static long GetValue(SolarSystemResources? resources, ResourceKind kind) => resources is null
        ? 0
        : kind switch
        {
            ResourceKind.Power => resources.Power,
            ResourceKind.Workforce => resources.Workforce,
            ResourceKind.MagmaticGas => resources.MagmaticGas,
            _ => resources.SuperionicIce,
        };

    /// <summary>按资源种类取星域资源值。</summary>
    public static long GetValue(RegionResources? resources, ResourceKind kind) => resources is null
        ? 0
        : kind switch
        {
            ResourceKind.Power => resources.Power,
            ResourceKind.Workforce => resources.Workforce,
            ResourceKind.MagmaticGas => resources.MagmaticGas,
            _ => resources.SuperionicIce,
        };

    /// <summary>取某星系的行星资源明细（行星名 / 产能 / 资源量），供星系详情页与清单页使用。</summary>
    public static List<PlanetResourcesDetail> GetSystemDetails(int systemId)
    {
        try
        {
            return Core.Services.DB.SolarSystemResourcesService.GetPlanetResourcesDetailsBySolarSystemID(systemId) ?? [];
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return [];
        }
    }

    /// <summary>取某星系的天体（恒星 / 行星 / 月球 / 主权设施），含类型名。</summary>
    public static List<MapDenormalizeDetail> GetCelestials(int systemId)
    {
        try
        {
            return Core.Services.DB.MapDenormalizeService.QueryBySolarSystemID(systemId, detail: true) ?? [];
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return [];
        }
    }

    /// <summary>装载（幂等）：聚合全部 00 星系的行星资源 + 星域汇总 + 设施升级表。</summary>
    public static async Task LoadAsync(IReadOnlyList<MapSolarSystem> allSystems)
    {
        lock (Locker)
        {
            if (IsLoaded)
            {
                return;
            }
        }

        await Task.Run(() =>
        {
            var systemResources = new Dictionary<int, SolarSystemResources>();
            try
            {
                var nullSecs = allSystems.Where(p => p.Security <= 0).ToList();
                var ids = nullSecs.Select(p => p.SolarSystemID).ToList();
                var dict = ids.Count > 0
                    ? Core.Services.DB.SolarSystemResourcesService.GetPlanetResourcesDetailsBySolarSystemID(ids)
                    : [];
                foreach (var system in nullSecs)
                {
                    if (!dict.TryGetValue(system.SolarSystemID, out var details) || details is null || details.Count == 0)
                    {
                        continue;
                    }

                    systemResources[system.SolarSystemID] = new SolarSystemResources
                    {
                        MapSolarSystem = system,
                        Power = details.Sum(p => (long)(p.PlanetResources?.Power ?? 0)),
                        Workforce = details.Sum(p => (long)(p.PlanetResources?.Workforce ?? 0)),
                        MagmaticGas = details.Where(p => p.PlanetResources?.TypeId == MagmaticGasTypeId).Sum(p => (long)(p.PlanetResources?.AmountPerCycle ?? 0)),
                        SuperionicIce = details.Where(p => p.PlanetResources?.TypeId == SuperionicIceTypeId).Sum(p => (long)(p.PlanetResources?.AmountPerCycle ?? 0)),
                    };
                }

                // 星域汇总（产能与人力都为 0 的星域不入表）
                var regions = Core.Services.DB.MapRegionService.QueryAll().ToDictionary(p => p.RegionID);
                var regionResources = new Dictionary<int, RegionResources>();
                foreach (var group in systemResources.Values.GroupBy(p => p.MapSolarSystem.RegionID))
                {
                    var power = group.Sum(p => p.Power);
                    var workforce = group.Sum(p => p.Workforce);
                    if (power + workforce == 0)
                    {
                        continue;
                    }

                    regions.TryGetValue(group.Key, out var region);
                    regionResources[group.Key] = new RegionResources
                    {
                        Region = region,
                        Power = power,
                        Workforce = workforce,
                        MagmaticGas = group.Sum(p => p.MagmaticGas),
                        SuperionicIce = group.Sum(p => p.SuperionicIce),
                    };
                }

                _systemResources = systemResources;
                _regionResources = regionResources;
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                _systemResources = systemResources;
                _regionResources = [];
            }

            try
            {
                _upgrades = Core.Services.UpgradeService.Current.GetUpgrades() ?? [];
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                _upgrades = [];
            }

            lock (Locker)
            {
                IsLoaded = true;
            }
        });
    }
}
