using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class SovereigntyUpgrades : BaseModel
    {
        public Fuel Fuel { get; set; }
        [JsonProperty("mutually_exclusive_group")]
        public string MutuallyExclusiveGroup { get; set; }
        [JsonProperty("power_allocation")]
        public int? PowerAllocation { get; set; }
        [JsonProperty("workforce_allocation")]
        public int? WorkforceAllocation { get; set; }
        [JsonProperty("power_production")]
        public int? PowerProduction { get; set; }
        [JsonProperty("workforce_production")]
        public int? WorkforceProduction { get; set; }
    }

    public class Fuel
    {
        [JsonProperty("hourly_upkeep")]
        public int HourlyUpkeep { get; set; }
        [JsonProperty("startup_cost")]
        public int StartupCost { get; set; }
        [JsonProperty("type_id")]
        public int TypeId { get; set; }
    }
}
