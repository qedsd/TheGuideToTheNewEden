using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public class StaStationService : DBService
    {
        public static async Task<StaStation> QueryAsync(int id)
        {
            SqlSugarClient db = new SqlSugarClient(DBModels.Config.MainDBConnectionConfig);
            var type = await db.Queryable<StaStation>().FirstAsync(p => p.StationID == id);
            if (DBLanguage == Enums.Language.Chinese)
            {
                await ZHDBService.TranStaStationAsync(type);
            }
            return type;
        }

        public static async Task<List<StaStation>> QueryAsync(List<int> ids)
        {
            return await Task.Run(async() =>
            {
                SqlSugarClient db = new SqlSugarClient(DBModels.Config.MainDBConnectionConfig);
                var types = db.Queryable<StaStation>().Where(p => ids.Contains(p.StationID)).ToList();
                if (DBLanguage == Enums.Language.Chinese)
                {
                    await ZHDBService.TranStaStationsAsync(types);
                }
                return types;
            });
        }
    }
}
