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
        public static async Task<MapSolarSystem> QueryAsync(string name)
        {
            var type = await DBService.MainDb.Queryable<MapSolarSystem>().FirstAsync(p => p.SolarSystemName == name);
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranMapSolarSystemAsync(type);
            }
            return type;
        }
        public static MapSolarSystem Query(string name)
        {
            var type = DBService.MainDb.Queryable<MapSolarSystem>().First(p => p.SolarSystemName == name);
            if (DBService.NeedLocalization)
            {
                LocalDbService.TranMapSolarSystem(type);
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
        public static List<MapSolarSystem> Query(List<int> ids)
        {
            var types = DBService.MainDb.Queryable<MapSolarSystem>().Where(p => ids.Contains(p.SolarSystemID)).ToList();
            if (DBService.NeedLocalization)
            {
                LocalDbService.TranMapSolarSystems(types);
            }
            return types;
        }
        public static MapSolarSystem Query(int id, bool local = true)
        {
            var system = DBService.MainDb.Queryable<MapSolarSystem>().First(p => id == p.SolarSystemID);
            if (local && DBService.NeedLocalization)
            {
                LocalDbService.TranMapSolarSystem(system);
            }
            return system;
        }

        public static async Task<List<MapSolarSystem>> QueryAllAsync()
        {
            var list = await DBService.MainDb.Queryable<MapSolarSystem>().ToListAsync();
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranMapSolarSystemsAsync(list);
            }
            return list;
        }
        public static List<MapSolarSystem> QueryAll()
        {
            var list = DBService.MainDb.Queryable<MapSolarSystem>().ToList();
            if (DBService.NeedLocalization)
            {
                LocalDbService.TranMapSolarSystems(list);
            }
            return list;
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
                var systems = DBService.MainDb.Queryable<MapSolarSystem>().Where(p => p.SolarSystemName.Contains(partName)).ToList();
                if (systems.NotNullOrEmpty())
                {
                    systems.ForEach(p => searchs.Add(new TranslationSearchItem(p)));
                }
                var locals = LocalDbService.SearchMapSolarSystem(partName);
                if (locals.NotNullOrEmpty())
                {
                    locals.ForEach(p => searchs.Add(new TranslationSearchItem(p)));
                }
                return searchs;
            });
        }

        public static async Task<List<MapSolarSystem>> QueryWormholesAsync()
        {
            return await DBService.MainDb.Queryable<MapSolarSystem>().Where(p=>p.SolarSystemID > 31000000 && p.SolarSystemName.StartsWith("J")).ToListAsync();
        }
    }
}
