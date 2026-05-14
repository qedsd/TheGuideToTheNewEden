using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public abstract class BaseModel
    {
        [JsonProperty("_key")]
        public int Id { get; set; }

        [JsonProperty("Name")]
        public Languages Names { get; set; }

        [JsonProperty("Description")]
        public Languages Descriptions { get; set; }
    }
}
