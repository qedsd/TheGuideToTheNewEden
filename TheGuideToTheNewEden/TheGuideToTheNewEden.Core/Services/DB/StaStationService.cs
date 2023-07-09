using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public class StaStationService
    {
        public static StaStation Query(int id, bool local = true)
        {
            var type = DBService.MainDb.Queryable<StaStation>().First(p => p.StationID == id);
            if (local && DBService.NeedLocalization)
            {
                LocalDbService.TranStaStation(type);
            }
            return type;
        }
        public static async Task<StaStation> QueryAsync(int id)
        {
            var type = await DBService.MainDb.Queryable<StaStation>().FirstAsync(p => p.StationID == id);
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranStaStationAsync(type);
            }
            return type;
        }
        public static async Task<StaStation> QueryAsync(long id)
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

        public static async Task<List<StaStation>> QueryAsync(List<long> ids)
        {
            var types = await DBService.MainDb.Queryable<StaStation>().Where(p => ids.Contains(p.StationID)).ToListAsync();
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranStaStationsAsync(types);
            }
            return types;
        }

        /// <summary>
        /// 模糊搜索，支持本地化数据库
        /// </summary>
        /// <param name="partName"></param>
        /// <returns></returns>
        public static async Task<List<TranslationSearchItem>> SearchAsync(string partName)
        {
            return await Task.Run(() =>
            {
                List<TranslationSearchItem> searchs = new List<TranslationSearchItem>();
                var stations = DBService.MainDb.Queryable<StaStation>().Where(p => p.StationName.Contains(partName)).ToList();
                if (stations.NotNullOrEmpty())
                {
                    stations.ForEach(p => searchs.Add(new TranslationSearchItem(p)));
                }
                var locals = LocalDbService.SearchStaStations(partName);
                if (locals.NotNullOrEmpty())
                {
                    locals.ForEach(p => searchs.Add(new TranslationSearchItem(p)));
                }
                return searchs;
            });
        }
    }
}
