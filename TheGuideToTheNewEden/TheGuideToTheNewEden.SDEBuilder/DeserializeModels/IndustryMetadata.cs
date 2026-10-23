using Newtonsoft.Json;
using System.Collections.Generic;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class IndustryActivities : BaseModel
    {
    }

    public class IndustryAssemblyLineDetail
    {
        [JsonProperty("categoryID")]
        public int CategoryID { get; set; }
        [JsonProperty("groupID")]
        public int GroupID { get; set; }
        [JsonProperty("costMultiplier")]
        public double CostMultiplier { get; set; } = 1.0;
        [JsonProperty("materialMultiplier")]
        public double MaterialMultiplier { get; set; } = 1.0;
        [JsonProperty("timeMultiplier")]
        public double TimeMultiplier { get; set; } = 1.0;
    }

    public class IndustryAssemblyLines : BaseModel
    {
        [JsonProperty("activityID")]
        public int ActivityID { get; set; }
        [JsonProperty("baseCostMultiplier")]
        public double BaseCostMultiplier { get; set; } = 1.0;
        [JsonProperty("baseMaterialMultiplier")]
        public double BaseMaterialMultiplier { get; set; } = 1.0;
        [JsonProperty("baseTimeMultiplier")]
        public double BaseTimeMultiplier { get; set; } = 1.0;
        [JsonProperty("detailsPerCategory")]
        public List<IndustryAssemblyLineDetail> DetailsPerCategory { get; set; }
        [JsonProperty("detailsPerGroup")]
        public List<IndustryAssemblyLineDetail> DetailsPerGroup { get; set; }
    }

    public class IndustryInstallationAssemblyLine
    {
        [JsonProperty("assemblyLineID")]
        public int AssemblyLineID { get; set; }
    }

    public class IndustryInstallationTypes : BaseModel
    {
        [JsonProperty("assemblyLines")]
        public List<IndustryInstallationAssemblyLine> AssemblyLines { get; set; }
    }

    public class IndustryModifierAttribute
    {
        [JsonProperty("dogmaAttributeID")]
        public int DogmaAttributeID { get; set; }
        /// <summary>目标过滤器 ID；0 = 不带过滤器（结构本体加成）。</summary>
        [JsonProperty("filterID")]
        public int FilterID { get; set; }
    }

    public class IndustryModifierActivity
    {
        [JsonProperty("cost")]
        public List<IndustryModifierAttribute> Cost { get; set; }
        [JsonProperty("time")]
        public List<IndustryModifierAttribute> Time { get; set; }
        [JsonProperty("material")]
        public List<IndustryModifierAttribute> Material { get; set; }
    }

    public class IndustryModifierSources : BaseModel
    {
        [JsonProperty("copying")]
        public IndustryModifierActivity Copying { get; set; }
        [JsonProperty("invention")]
        public IndustryModifierActivity Invention { get; set; }
        [JsonProperty("manufacturing")]
        public IndustryModifierActivity Manufacturing { get; set; }
        [JsonProperty("researchMaterial")]
        public IndustryModifierActivity ResearchMaterial { get; set; }
        [JsonProperty("researchTime")]
        public IndustryModifierActivity ResearchTime { get; set; }
        [JsonProperty("reaction")]
        public IndustryModifierActivity Reaction { get; set; }
    }

    public class IndustryTargetFilters : BaseModel
    {
        [JsonProperty("categoryIDs")]
        public List<int> CategoryIDs { get; set; }
        [JsonProperty("groupIDs")]
        public List<int> GroupIDs { get; set; }
    }
}
