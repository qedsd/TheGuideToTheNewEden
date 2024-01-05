using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZKB.NET.Models.Statistics
{
    /// <summary>
    /// 某个类型下的击杀个数
    /// </summary>
    public class KillData
    {
        /// <summary>
        /// 角色、军团、联盟、星系、船类型、势力等ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 击杀个数
        /// </summary>
        public int Kills { get; set; }

        [JsonProperty("characterID")]
        private int CharacterID { set { Id = value; } }
        [JsonProperty("corporationID")]
        private int CorporationID { set { Id = value; } }
        [JsonProperty("allianceID")]
        private int AllianceID { set { Id = value; } }
        [JsonProperty("factionID")]
        private int FactionID { set { Id = value; } }
        [JsonProperty("shipTypeID")]
        private int ShipTypeID { set { Id = value; } }
        [JsonProperty("solarSystemID")]
        private int SolarSystemID { set { Id = value; } }
    }
}
