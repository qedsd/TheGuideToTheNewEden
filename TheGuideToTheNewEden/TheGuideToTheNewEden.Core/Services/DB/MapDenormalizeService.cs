using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public class MapDenormalizeService
    {
        public static List<MapDenormalize> QueryBySolarSystemID(int id)
        {
            return DBService.MainDb.Queryable<MapDenormalize>().Where(p => p.SolarSystemID == id).ToList();
        }
        public static async Task<List<MapDenormalize>> QueryBySolarSystemIDAsync(int id)
        {
            return await DBService.MainDb.Queryable<MapDenormalize>().Where(p => p.SolarSystemID == id).ToListAsync();
        }
    }
}
