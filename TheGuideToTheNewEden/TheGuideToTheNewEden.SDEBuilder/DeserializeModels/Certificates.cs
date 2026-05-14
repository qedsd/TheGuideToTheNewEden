using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class Certificates : BaseModel
    {
        [JsonProperty("groupID")]
        public int GroupID { get; set; }

        [JsonProperty("recommendedFor")]
        public List<int> RecommendedFor { get; set; }

        [JsonProperty("skillTypes")]
        public List<SkillType> SkillTypes { get; set; }
    }

    public class SkillType
    {
        [JsonProperty("advanced")]
        public int Advanced { get; set; }

        [JsonProperty("basic")]
        public int Basic { get; set; }

        [JsonProperty("elite")]
        public int Elite { get; set; }

        [JsonProperty("improved")]
        public int Improved { get; set; }

        [JsonProperty("standard")]
        public int Standard { get; set; }
    }
}
