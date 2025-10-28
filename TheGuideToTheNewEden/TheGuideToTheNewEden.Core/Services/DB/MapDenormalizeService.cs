using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public class MapDenormalizeService
    {
        public static List<MapDenormalizeDetail> QueryBySolarSystemID(int id, bool detail = false)
        {
            var items = DBService.MainDb.Queryable<MapDenormalize>().Where(p => p.SolarSystemID == id).ToList();
            if (items.NotNullOrEmpty())
            {
                List<MapDenormalizeDetail> mapDenormalizeDetails = items.Select(p=>new MapDenormalizeDetail(p)).ToList();
                if (detail)
                {
                    var types = InvTypeService.QueryTypes(items.Select(p => p.TypeID).ToList()).ToDictionary(p => p.TypeID);
                    foreach (var item in mapDenormalizeDetails)
                    {
                        item.Type = types[item.TypeID];
                    }
                }
                return mapDenormalizeDetails;
            }
            else
            {
                return null;
            }
        }
        public static async Task<List<MapDenormalizeDetail>> QueryBySolarSystemIDAsync(int id)
        {
            return await Task.Run(()=> QueryBySolarSystemID(id));
        }

        public static List<MapDenormalize> QueryBySolarSystemID(List<int> ids)
        {
            return DBService.MainDb.Queryable<MapDenormalize>().Where(p => ids.Contains(p.SolarSystemID)).ToList();
        }
    }
}
