using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public class MapRegionService : DBService
    {
        public static async Task<MapRegion> QueryAsync(int id)
        {
            SqlSugarClient db = new SqlSugarClient(DBModels.Config.MainDBConnectionConfig);
            var type = await db.Queryable<MapRegion>().FirstAsync(p => p.RegionID == id);
            if (DBLanguage == Enums.Language.Chinese)
            {
                await ZHDBService.TranMapRegionAsync(type);
            }
            return type;
        }

        public static async Task<List<MapRegion>> QueryAsync(List<int> ids)
        {
            return await Task.Run(async() =>
            {
                SqlSugarClient db = new SqlSugarClient(DBModels.Config.MainDBConnectionConfig);
                var types = db.Queryable<MapRegion>().Where(p => ids.Contains(p.RegionID)).ToList();
                if (DBLanguage == Enums.Language.Chinese)
                {
                    await ZHDBService.TranMapRegionsAsync(types);
                }
                return types;
            });
        }
    }
}
