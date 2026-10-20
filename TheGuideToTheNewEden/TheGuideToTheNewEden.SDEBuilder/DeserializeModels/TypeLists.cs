using System.Collections.Generic;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class TypeLists : BaseModel
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        public new string Names { get; set; }
        public List<int> IncludedTypeIDs { get; set; }
    }
}
