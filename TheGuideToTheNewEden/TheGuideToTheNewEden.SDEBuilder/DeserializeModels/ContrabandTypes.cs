using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class ContrabandTypes : BaseModel
    {
        [JsonProperty("factions")]
        public List<Faction> Factions { get; set; }
    }

    public class Faction
    {
        [JsonProperty("_key")]
        public int Key { get; set; }

        [JsonProperty("attackMinSec")]
        public double AttackMinSec { get; set; }

        [JsonProperty("confiscateMinSec")]
        public double ConfiscateMinSec { get; set; }

        [JsonProperty("fineByValue")]
        public double FineByValue { get; set; }

        [JsonProperty("standingLoss")]
        public double StandingLoss { get; set; }
    }
}
