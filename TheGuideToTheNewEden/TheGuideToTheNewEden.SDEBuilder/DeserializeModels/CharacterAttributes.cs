using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class CharacterAttributes : BaseModel
    {
        public new string Descriptions {  get; set; }
        public string Description {  get; set; }

        [JsonProperty("iconID")]
        public int IconID { get; set; }

        [JsonProperty("shortDescription")]
        public string ShortDescription { get; set; }

        [JsonProperty("notes")]
        public string Notes { get; set; }
    }
}
