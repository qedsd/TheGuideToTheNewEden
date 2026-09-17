using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.PlanetResources;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public class SolarSystemResourcesService
    {
        private const int SuperionicIceID = 81144;
        private const int MagmaticGasID = 81143;
        public static SolarSystemResources QueryBySolarSystemID(int id)
        {
            SolarSystemResources solarSystemResources = new SolarSystemResources()
            {
                MapSolarSystem = MapSolarSystemService.Query(id),
            };
            var planetResources = GetPlanetResourcesDetailsBySolarSystemID(id);
            if(planetResources.NotNullOrEmpty())
            {
                // 注意：PlanetResourcesDetail.PlanetResources 可能为 null（该天体在 planetResources 表里没有行），
                // 直接点 p.PlanetResources.X 会 NRE（阶段 63 实机踩到）。两种用量都走 null 守卫。
                solarSystemResources.Power = planetResources.Sum(p => (long)(p.PlanetResources?.Power ?? 0));
                solarSystemResources.Workforce = planetResources.Sum(p => (long)(p.PlanetResources?.Workforce ?? 0));
                solarSystemResources.SuperionicIce = planetResources.Sum(p => p.SuperionicIce);
                solarSystemResources.MagmaticGas = planetResources.Sum(p => p.MagmaticGas);
            }
            return solarSystemResources;
        }
        public static List<PlanetResourcesDetail> GetPlanetResourcesDetailsBySolarSystemID(int id)
        {
            var mapDenormalizes = MapDenormalizeService.QueryBySolarSystemID(id);
            List<PlanetResourcesDetail> list = null;
            if (mapDenormalizes.NotNullOrEmpty())
            {
                list = new List<PlanetResourcesDetail>();
                foreach (var mapDenormalize in mapDenormalizes)
                {
                    list.Add(new PlanetResourcesDetail()
                    {
                        MapDenormalize = mapDenormalize,
                        PlanetResources = PlanetResourcesService.QueryByStarID(mapDenormalize.ItemID)
                    });
                }
            }
            return list;
        }
        public static async Task<SolarSystemResources> QueryBySolarSystemIDAsync(int id)
        {
            return await Task.Run(()=>QueryBySolarSystemID(id));
        }
        public static List<SolarSystemResources> QueryByRegionID(int id)
        {
            var systems = MapSolarSystemService.QueryByRegionID(id);
            // 原来是 list = null，一旦 systems 非空就会在 list.Add 处 NRE；同时循环内把"星域 ID"当"星系 ID"传下去，
            // 每个星系都算成同一个（错误的）星系资源。两处都在阶段 63 一并修掉。
            var list = new List<SolarSystemResources>();
            if(systems.NotNullOrEmpty())
            {
                foreach(var system in systems)
                {
                    SolarSystemResources solarSystemResources = new SolarSystemResources()
                    {
                        MapSolarSystem = system,
                    };
                    var planetResources = GetPlanetResourcesDetailsBySolarSystemID(system.SolarSystemID);
                    if (planetResources.NotNullOrEmpty())
                    {
                        solarSystemResources.Power = planetResources.Sum(p => (long)(p.PlanetResources?.Power ?? 0));
                        solarSystemResources.Workforce = planetResources.Sum(p => (long)(p.PlanetResources?.Workforce ?? 0));
                        solarSystemResources.SuperionicIce = planetResources.Sum(p => p.SuperionicIce);
                        solarSystemResources.MagmaticGas = planetResources.Sum(p => p.MagmaticGas);
                    }
                    list.Add(solarSystemResources);
                }
            }
            return list;
        }
        public static async Task<List<SolarSystemResources>> QueryByRegionIDAsync(int id)
        {
            return await Task.Run(()=> QueryByRegionID(id));
        }

        public static Dictionary<int, List<PlanetResourcesDetail>> GetPlanetResourcesDetailsBySolarSystemID(List<int> ids)
        {
            var allMapDenormalizes = MapDenormalizeService.QueryBySolarSystemID(ids);
            if (allMapDenormalizes.NotNullOrEmpty())
            {
                var results = new Dictionary<int, List<PlanetResourcesDetail>>();
                //StartID == ItemID
                var allPlanetResources = PlanetResourcesService.QueryByStarID(allMapDenormalizes.Select(p => p.ItemID).ToList());
                if(allPlanetResources.NotNullOrEmpty())
                {
                    var allPlanetResourcesDic = allPlanetResources.ToDictionary(p => p.StarID);
                    var allMapDenormalizesGroup = allMapDenormalizes.GroupBy(p=>p.SolarSystemID);
                    foreach(var group in allMapDenormalizesGroup)
                    {
                        if(group.Count() > 0)
                        {
                            List<PlanetResourcesDetail> list = new List<PlanetResourcesDetail>();
                            foreach (var mapDenormalize in group)
                            {
                                if(allPlanetResourcesDic.TryGetValue(mapDenormalize.ItemID, out var planetResources))
                                {
                                    list.Add(new PlanetResourcesDetail()
                                    {
                                        MapDenormalize = mapDenormalize,
                                        PlanetResources = planetResources
                                    });
                                }
                            }
                            if(list.Any())
                            {
                                results.Add(group.Key, list);
                            }
                        }
                    }
                }
                return results;
            }
            return null;
        }
    }
}
