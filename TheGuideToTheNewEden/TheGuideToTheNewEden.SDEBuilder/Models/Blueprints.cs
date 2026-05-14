using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.Models
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

    public class Blueprints
    {
        [JsonProperty("_key")]
        public int Id { get; set; }

        public Activities Activities { get; set; }

        [SqlSugar.SugarColumn(IsPrimaryKey = true)]
        public int BlueprintTypeID { get; set; }

        public int MaxProductionLimit { get; set; }
    }

    #region db model
    [SqlSugar.SugarTable(TableName = "industryBlueprints")]
    public class BlueprintsDb
    {
        [SqlSugar.SugarColumn(IsPrimaryKey = true)]
        public int BlueprintTypeID { get; set; }

        public int MaxProductionLimit { get; set; }
    }
    [SqlSugar.SugarTable(TableName = "industryActivitySkills")]
    public class BlueprintSkillDb
    {
        public int BlueprintTypeID { get; set; }
        public short ActivitieID { get; set; }
        public int SkillID { get; set; }
        public int Level { get; set; }
    }
    [SqlSugar.SugarTable(TableName = "industryActivityProducts")]
    public class BlueprintProductDb
    {
        public int BlueprintTypeID { get; set; }
        public short ActivitieID { get; set; }
        public int ProductTypeID { get; set; }
        public int Quantity { get; set; }
        /// <summary>
        /// 成功概率
        /// </summary>
        public double Probability { get; set; }
    }

    [SqlSugar.SugarTable(TableName = "industryActivityMaterials")]
    public class BlueprintMaterialDb
    {
        public int BlueprintTypeID { get; set; }
        public short ActivitieID { get; set; }
        public int MaterialTypeID { get; set; }
        public int Quantity { get; set; }
    }

    [SqlSugar.SugarTable(TableName = "industryActivities")]
    public class BlueprintActivitieDb
    {
        public int BlueprintTypeID { get; set; }
        /// <summary>
        /// https://github.com/esi/esi-issues/issues/894
        /// 0 None
        /// 1 Manufacturing
        /// 3 Researching Time Efficiency
        /// 4 Researching Material Efficiency
        /// 5 Copying
        /// 7 Reverse Engineering(None)
        /// 8 Invention
        /// 9 Reactions(None)
        /// 11 Reactions
        /// </summary>
        public short ActivitieID { get; set; }
        public int Time { get; set; }
    }
    #endregion
}
