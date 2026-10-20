using System.Collections.Generic;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class CloneGrades : BaseModel
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        public new string Names { get; set; }
        public List<CloneGradeSkill> Skills { get; set; }
    }

    public class CloneGradeSkill
    {
        public int Level { get; set; }
        public int TypeID { get; set; }
    }
}
