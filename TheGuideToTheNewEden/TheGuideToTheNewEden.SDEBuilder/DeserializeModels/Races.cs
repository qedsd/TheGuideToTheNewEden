using System.Collections.Generic;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class Races : BaseModel
    {
        public int IconID { get; set; }
        public int ShipTypeID { get; set; }
        public List<RaceSkill> Skills { get; set; }
    }

    public class RaceSkill
    {
        [JsonProperty("_key")]
        public int TypeID { get; set; }
        [JsonProperty("_value")]
        public int Level { get; set; }
    }
}
