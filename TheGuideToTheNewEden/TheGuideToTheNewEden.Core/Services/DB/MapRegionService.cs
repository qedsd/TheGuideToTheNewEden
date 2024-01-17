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
        public static MapRegion Query(int id, bool local = true)
        {
            var type = DBService.MainDb.Queryable<MapRegion>().First(p => p.RegionID == id);
            if (local && DBService.NeedLocalization)
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
                var regions = DBService.MainDb.Queryable<MapRegion>().Where(p => p.RegionName.Contains(partName)).ToList();
                if (regions.NotNullOrEmpty())
                {
                    regions.ForEach(p => searchs.Add(new TranslationSearchItem(p)));
                }
                var locals = LocalDbService.SearchMapRegion(partName);
                if (locals.NotNullOrEmpty())
                {
                    locals.ForEach(p => searchs.Add(new TranslationSearchItem(p)));
                }
                return searchs;
            });
        }

        /// <summary>
        /// 模糊搜索，支持本地化数据库
        /// </summary>
        /// <param name="partName"></param>
        /// <returns></returns>
        public static List<TranslationSearchItem> Search(string partName)
        {
            List<TranslationSearchItem> searchs = new List<TranslationSearchItem>();
            var regions = DBService.MainDb.Queryable<MapRegion>().Where(p => p.RegionName.Contains(partName)).ToList();
            if (regions.NotNullOrEmpty())
            {
                regions.ForEach(p => searchs.Add(new TranslationSearchItem(p)));
            }
            var locals = LocalDbService.SearchMapRegion(partName);
            if (locals.NotNullOrEmpty())
            {
                locals.ForEach(p => searchs.Add(new TranslationSearchItem(p)));
            }
            return searchs;
        }
    }
}
