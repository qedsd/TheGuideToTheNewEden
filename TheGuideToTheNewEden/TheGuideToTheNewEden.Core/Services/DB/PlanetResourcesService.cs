using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public class PlanetResourcesService
    {
        public static PlanetResources QueryByStarID(long id)
        {
            return DBService.MainDb.Queryable<PlanetResources>().First(p => p.StarID == id);
        }
        public static List<PlanetResources> QueryByStarID(List<long> ids)
        {
            return DBService.MainDb.Queryable<PlanetResources>().In(ids).ToList();
        }
        public static List<PlanetResources> QueryByStarID(List<int> ids)
        {
            return DBService.MainDb.Queryable<PlanetResources>().In(ids).ToList();
        }
        /// <summary>
        /// 查询全部行星资源，用于本地模拟时展示资源分布
        /// </summary>
        public static List<PlanetResources> GetAll()
        {
            return DBService.MainDb.Queryable<PlanetResources>().ToList();
        }
    }
}
