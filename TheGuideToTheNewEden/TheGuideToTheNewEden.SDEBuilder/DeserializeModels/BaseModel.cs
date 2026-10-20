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
        [JsonConverter(typeof(LanguagesConverter))]
        public Languages Names { get; set; }

        [JsonProperty("Description")]
        [JsonConverter(typeof(LanguagesConverter))]
        public Languages Descriptions { get; set; }
    }
}
