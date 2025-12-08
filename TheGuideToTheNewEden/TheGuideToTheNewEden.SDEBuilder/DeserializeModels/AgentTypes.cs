using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using static Azure.Core.HttpHeader;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class AgentTypes : BaseModel
    {
        [JsonProperty("Name")]
        public string Name {  get; set; }

        public new string Names {  get; set; }
    }
}
