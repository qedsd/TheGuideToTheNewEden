using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class PlanetResources : BaseModel
    {
        public int Power { get; set; }
        public int Workforce { get; set; }
        public Reagent Reagent { get; set; }
    }

    public class Reagent
    {
        [JsonProperty("amount_per_cycle")]
        public int AmountPerCycle { get; set; }

        [JsonProperty("cycle_period")]
        public int CyclePeriod { get; set; }

        [JsonProperty("secured_capacity")]
        public long SecuredCapacity { get; set; }

        [JsonProperty("type_id")]
        public int TypeId { get; set; }

        [JsonProperty("unsecured_capacity")]
        public long UnsecuredCapacity { get; set; }
    }
}
