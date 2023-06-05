using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public class MapRegionService
    {
        public static async Task<MapRegion> QueryAsync(int id)
        {
            var type = await DBService.MainDb.Queryable<MapRegion>().FirstAsync(p => p.RegionID == id);
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranMapRegionAsync(type);
            }
            return type;
        }
        public static MapRegion Query(int id)
        {
            var type = DBService.MainDb.Queryable<MapRegion>().First(p => p.RegionID == id);
            if (DBService.NeedLocalization)
            {
                LocalDbService.TranMapRegion(type);
            }
            return type;
        }

        public static async Task<List<MapRegion>> QueryAsync(List<int> ids)
        {
            var types = DBService.MainDb.Queryable<MapRegion>().Where(p => ids.Contains(p.RegionID)).ToList();
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranMapRegionsAsync(types);
            }
            return types;
        }
        public static async Task<List<MapRegion>> QueryAllAsync()
        {
            var datas = await DBService.MainDb.Queryable<MapRegion>().ToListAsync();
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranMapRegionsAsync(datas);
            }
            return datas;
        }
    }
}
