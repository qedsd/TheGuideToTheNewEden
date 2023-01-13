using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public class StaStationService
    {
        public static async Task<StaStation> QueryAsync(int id)
        {
            var type = await DBService.MainDb.Queryable<StaStation>().FirstAsync(p => p.StationID == id);
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranStaStationAsync(type);
            }
            return type;
        }

        public static async Task<List<StaStation>> QueryAsync(List<int> ids)
        {
            var types = await DBService.MainDb.Queryable<StaStation>().Where(p => ids.Contains(p.StationID)).ToListAsync();
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranStaStationsAsync(types);
            }
            return types;
        }
    }
}
