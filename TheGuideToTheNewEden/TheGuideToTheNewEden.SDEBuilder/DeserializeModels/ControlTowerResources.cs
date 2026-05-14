using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class ControlTowerResources : BaseModel
    {
        [JsonProperty("resources")]
        public List<ResourceItem> Resources { get; set; }
    }

    public class ResourceItem
    {
        [JsonProperty("purpose")]
        public int Purpose { get; set; }

        [JsonProperty("quantity")]
        public int Quantity { get; set; }

        [JsonProperty("resourceTypeID")]
        public int ResourceTypeID { get; set; }

        [JsonProperty("factionID")]
        public int? FactionID { get; set; }

        [JsonProperty("minSecurityLevel")]
        public double? MinSecurityLevel { get; set; }
    }
}
