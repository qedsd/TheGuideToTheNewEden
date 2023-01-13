using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public class MapSolarSystemService
    {
        public static async Task<MapSolarSystem> QueryAsync(int id)
        {
            var type = await DBService.MainDb.Queryable<MapSolarSystem>().FirstAsync(p => p.SolarSystemID == id);
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranMapSolarSystemAsync(type);
            }
            return type;
        }

        public static async Task<List<MapSolarSystem>> QueryAsync(List<int> ids)
        {
            var types = await DBService.MainDb.Queryable<MapSolarSystem>().Where(p => ids.Contains(p.SolarSystemID)).ToListAsync();
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranMapSolarSystemsAsync(types);
            }
            return types;
        }
    }
}
