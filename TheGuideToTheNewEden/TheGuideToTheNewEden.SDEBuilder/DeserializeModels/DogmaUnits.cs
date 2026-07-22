using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class DogmaUnits : BaseModel
    {
        [JsonProperty("description")]
        public Languages Description { get; set; }

        [JsonProperty("displayName")]
        public Languages DisplayName { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }
}
