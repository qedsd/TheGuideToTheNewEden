using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.PlanetResources;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public class SolarSystemResourcesService
    {
        public static SolarSystemResources QueryBySolarSystemID(int id)
        {
            SolarSystemResources solarSystemResources = new SolarSystemResources()
            {
                MapSolarSystem = MapSolarSystemService.Query(id),
                PlanetResources = GetPlanetResourcesDetailsBySolarSystemID(id)
            };
            return solarSystemResources;
        }
        private static List<PlanetResourcesDetail> GetPlanetResourcesDetailsBySolarSystemID(int id)
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
                        ItemType = InvTypeService.QueryType(mapDenormalize.TypeID),
                        //ItemName = InvNameService.Query(mapDenormalize.ItemID),
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
            List<SolarSystemResources> list = null;
            if(systems.NotNullOrEmpty())
            {
                foreach(var system in systems)
                {
                    list.Add(new SolarSystemResources()
                    {
                        MapSolarSystem = system,
                        PlanetResources = GetPlanetResourcesDetailsBySolarSystemID(id)
                    });
                }
            }
            return list;
        }
        public static async Task<List<SolarSystemResources>> QueryByRegionIDAsync(int id)
        {
            return await Task.Run(()=> QueryByRegionID(id));
        }
    }
}
