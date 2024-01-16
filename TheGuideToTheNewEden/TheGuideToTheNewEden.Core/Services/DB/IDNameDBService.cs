using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public static class IDNameDBService
    {
        public static async Task<List<IdName>> QueryAsync(List<int> ids)
        {
            return await DBService.CacheDb.Queryable<IdName>().Where(p => ids.Contains(p.Id)).ToListAsync();
        }
        public static async Task<IdName> QueryAsync(int id)
        {
            return await DBService.CacheDb.Queryable<IdName>().Where(p =>p.Id == id).FirstAsync();
        }
        public static List<IdName> Query(List<int> ids)
        {
            return  DBService.CacheDb.Queryable<IdName>().Where(p => ids.Contains(p.Id)).ToList();
        }
        public static IdName Query(int id)
        {
            return DBService.CacheDb.Queryable<IdName>().Where(p => p.Id == id).First();
        }

        public static async Task<List<IdName>> QueryAsync(List<string> names)
        {
            return await DBService.CacheDb.Queryable<IdName>().Where(p => names.Contains(p.Name)).ToListAsync();
        }
        public static async Task<IdName> QueryAsync(string name)
        {
            return await DBService.CacheDb.Queryable<IdName>().Where(p => p.Name == name).FirstAsync();
        }
        public static List<IdName> Query(List<string> names)
        {
            return DBService.CacheDb.Queryable<IdName>().Where(p => names.Contains(p.Name)).ToList();
        }
        public static IdName Query(string name)
        {
            return DBService.CacheDb.Queryable<IdName>().Where(p => p.Name == name).First();
        }

        public static async Task<int> InsertAsync(List<IdName> idNames)
        {
            return await DBService.CacheDb.Insertable(idNames).ExecuteCommandAsync();
        }
        public static int Insert(List<IdName> idNames)
        {
            return DBService.CacheDb.Insertable(idNames).ExecuteCommand();
        }
    }
}
