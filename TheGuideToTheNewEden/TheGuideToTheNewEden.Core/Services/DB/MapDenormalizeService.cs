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

        public static MapDenormalize Query(int id)
        {
            return DBService.MainDb.Queryable<MapDenormalize>().Where(p => p.ItemID == id).First();
        }

        /// <summary>
        /// 某星系的全部行星（groupID = 7），按天体编号排序；<see cref="MapDenormalize.Radius"/> 为真实半径（米）。
        /// </summary>
        public static List<MapDenormalize> QueryPlanetsBySolarSystemID(int solarSystemId)
        {
            if (solarSystemId <= 0)
            {
                return new List<MapDenormalize>();
            }
            return DBService.MainDb.Queryable<MapDenormalize>()
                .Where(p => p.SolarSystemID == solarSystemId && p.GroupID == 7)
                .ToList()
                .OrderBy(p => p.ItemID)
                .ToList();
        }

        /// <summary>按行星 ID 取单个行星（含真实半径）；不是行星时返回 null。</summary>
        public static MapDenormalize QueryPlanet(long planetId)
        {
            if (planetId <= 0 || planetId > int.MaxValue)
            {
                return null;
            }
            return DBService.MainDb.Queryable<MapDenormalize>()
                .Where(p => p.ItemID == (int)planetId && p.GroupID == 7)
                .First();
        }
    }
}
