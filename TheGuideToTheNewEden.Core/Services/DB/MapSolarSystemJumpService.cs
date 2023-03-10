using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public static class MapSolarSystemJumpService
    {
        /// <summary>
        /// 获取所有的MapSolarSystemJump
        /// </summary>
        /// <returns></returns>
        public static async Task<List<MapSolarSystemJump>> QueryAllAsync()
        {
            return await DBService.MainDb.Queryable<MapSolarSystemJump>().ToListAsync();
        }
        /// <summary>
        /// 获取所有的MapSolarSystemJump
        /// </summary>
        /// <returns></returns>
        public static List<MapSolarSystemJump> QueryAll()
        {
            return DBService.MainDb.Queryable<MapSolarSystemJump>().ToList();
        }
    }
}
