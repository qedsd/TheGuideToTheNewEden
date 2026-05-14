using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.Models
{
    public class SDEInfo
    {
        [JsonProperty("_key")]
        public string Id {  get; set; }
        public int BuildNumber {  get; set; }
        public DateTime ReleaseDate {  get; set; }
    }
}
