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
    }
}
