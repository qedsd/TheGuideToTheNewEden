using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public class MapSolarSystemService : DBService
    {
        public static async Task<MapSolarSystem> QueryAsync(int id)
        {
            SqlSugarClient db = new SqlSugarClient(DBModels.Config.MainDBConnectionConfig);
            var type = await db.Queryable<MapSolarSystem>().FirstAsync(p => p.SolarSystemID == id);
            if (DBLanguage == Enums.Language.Chinese)
            {
                await ZHDBService.TranMapSolarSystemAsync(type);
            }
            return type;
        }

        public static async Task<List<MapSolarSystem>> QueryAsync(List<int> ids)
        {
            return await Task.Run(async() =>
            {
                SqlSugarClient db = new SqlSugarClient(DBModels.Config.MainDBConnectionConfig);
                var types = db.Queryable<MapSolarSystem>().Where(p => ids.Contains(p.SolarSystemID)).ToList();
                if (DBLanguage == Enums.Language.Chinese)
                {
                    await ZHDBService.TranMapSolarSystemsAsync(types);
                }
                return types;
            });
        }
    }
}
