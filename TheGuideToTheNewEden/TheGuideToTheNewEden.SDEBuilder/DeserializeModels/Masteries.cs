using System.Collections.Generic;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class Masteries : BaseModel
    {
        [JsonProperty("_value")]
        public List<MasteryLevel> Values { get; set; }
    }

    public class MasteryLevel
    {
        [JsonProperty("_key")]
        public int Level { get; set; }
        [JsonProperty("_value")]
        public List<int> SkillTypeIDs { get; set; }
    }
}
