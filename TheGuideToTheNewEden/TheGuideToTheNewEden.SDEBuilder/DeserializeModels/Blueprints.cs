using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class MaterialsItem
    {
        public int Quantity { get; set; }
        public int TypeID { get; set; }
    }

    public class SkillsItem
    {
        public int Level { get; set; }
        public int TypeID { get; set; }
    }

    public class ProductsItem
    {
        public int Quantity { get; set; }
        public int TypeID { get; set; }
        /// <summary>
        /// 成功概率
        /// </summary>
        public double Probability { get; set; } = 1;
    }

    public class ActivityRequire
    {
        public List<MaterialsItem> Materials { get; set; }
        public List<ProductsItem> Products { get; set; }
        public List<SkillsItem> Skills { get; set; }
        public int Time { get; set; }
    }

    public class Activities
    {
        public ActivityRequire Copying { get; set; }
        public ActivityRequire Manufacturing { get; set; }
        [JsonProperty("research_material")]
        public ActivityRequire ResearchMaterial { get; set; }
        [JsonProperty("research_time")]
        public ActivityRequire ResearchTime { get; set; }
        public ActivityRequire Invention { get; set; }
        public ActivityRequire Reaction { get; set; }
    }

    public class Blueprints: BaseModel
    {
        public Activities Activities { get; set; }

        public int BlueprintTypeID { get; set; }

        public int MaxProductionLimit { get; set; }
    }
}
