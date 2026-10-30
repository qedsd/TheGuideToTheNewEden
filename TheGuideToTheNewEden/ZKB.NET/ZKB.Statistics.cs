using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZKB.NET.Models.Statistics;

namespace ZKB.NET
{
    public static partial class ZKB
    {
        public static string GetFormatUrl(EntityType entityType, int id)
        {
            string type = entityType.ToString();
            return $"{Config.ApiUrl.TrimEnd('/')}/stats/{ToStartLower(type)}/{id}/";
        }
        public static Task<EntityStatistic> GetStatisticAsync(EntityType entityType, int id)
            => GetStatisticAsync(entityType, id, default);

        public static async Task<EntityStatistic> GetStatisticAsync(EntityType entityType, int id, System.Threading.CancellationToken cancellationToken)
        {
            string json = await HttpHelper.GetZKBAsync(GetFormatUrl(entityType, id), cancellationToken);
            return JsonConvert.DeserializeObject<EntityStatistic>(json);
        }
    }
    public enum EntityType
    {
        CharacterID,
        CorporationID,
        AllianceID,
        FactionID,
        ShipTypeID,
        GroupID,
        SolarSystemID,
        RegionID
    }
}
