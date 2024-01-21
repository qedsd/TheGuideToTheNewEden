using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZKB.NET.Models.Statistics
{
    public class EntityInfo
    {
        public int Id { get; set; }

        /// <summary>
        /// characterID/corporationID/allianceID/factionID/shipTypeID/groupID/solarSystemID/regionID
        /// </summary>
        public string Type { get; set; }
        public string Name { get; set; }
        public int CorpCount { get; set; }

        /// <summary>
        /// 执行军团ID
        /// </summary>
        public int ExecutorCorpID { get; set; }
        public int AllianceID { get; set; }
        public int CEOID { get; set; }
        /// <summary>
        /// 势力ID
        /// 仅npc军团有，默认0
        /// </summary>
        public int FactionID { get; set; }

        public int MemberCount { get; set; }

        /// <summary>
        /// 缩写
        /// </summary>
        public string Ticker { get; set; }

        /// <summary>
        /// 联盟创建者军团ID
        /// </summary>
        [JsonProperty("creator_corporation_id")]
        public int CreatorCorporationId { get; set; }

        /// <summary>
        /// 创建者ID
        /// </summary>
        [JsonProperty("creator_id")]
        public int Creatorid { get; set; }

        /// <summary>
        /// 成立时间
        /// </summary>
        [JsonProperty("date_founded")]
        public string DateFounded { get; set; }

        /// <summary>
        /// 联盟执行军团ID
        /// </summary>
        [JsonProperty("executor_corporation_id")]
        public int ExecutorCorporationId { get; set; }

        /// <summary>
        /// 服务器版本号
        /// </summary>
        public int ServerVersion { get; set; }

        /// <summary>
        /// 上一次API更新时间
        /// </summary>
        public LastApiUpdate LastApiUpdate { get; set; }
    }
    public class LastApiUpdate
    {
        /// <summary>
        /// eg:1704357232
        /// </summary>
        public int Sec { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int Usec { get; set; }

    }
}
