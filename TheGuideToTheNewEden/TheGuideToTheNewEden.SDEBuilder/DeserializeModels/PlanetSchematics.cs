using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class PlanetSchematicsType
    {
        [JsonProperty("_key")]
        public int TypeID { get; set; }

        [JsonProperty("isInput")]
        public bool IsInput { get; set; }

        [JsonProperty("quantity")]
        public int Quantity { get; set; }
    }

    public class PlanetSchematics : BaseModel
    {
        [JsonProperty("cycleTime")]
        public int CycleTime { get; set; }

        [JsonProperty("name")]
        public Languages Name { get; set; }

        [JsonProperty("pins")]
        public List<int> Pins { get; set; }

        [JsonProperty("types")]
        public List<PlanetSchematicsType> Types { get; set; }
    }
}
