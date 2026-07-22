using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("blueprints")]
    [SugarIndex("index_blueprints_id", nameof(Id), OrderByType.Asc)]
    public class Blueprints
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public int BlueprintTypeID { get; set; }
        public int MaxProductionLimit { get; set; }
        public Blueprints() { }
    }

    [SugarTable("blueprintActivities")]
    public class BlueprintActivity
    {
        public int BlueprintTypeID { get; set; }
        public int ActivityID { get; set; }
        public int Time { get; set; }
        public BlueprintActivity() { }
    }

    [SugarTable("blueprintMaterials")]
    public class BlueprintMaterial
    {
        public int BlueprintTypeID { get; set; }
        public int ActivityID { get; set; }
        public int MaterialTypeID { get; set; }
        public int Quantity { get; set; }
        public BlueprintMaterial() { }
    }

    [SugarTable("blueprintProducts")]
    public class BlueprintProduct
    {
        public int BlueprintTypeID { get; set; }
        public int ActivityID { get; set; }
        public int ProductTypeID { get; set; }
        public int Quantity { get; set; }
        [SugarColumn(IsNullable = true)]
        public double? Probability { get; set; }
        public BlueprintProduct() { }
    }

    [SugarTable("blueprintSkills")]
    public class BlueprintSkill
    {
        public int BlueprintTypeID { get; set; }
        public int ActivityID { get; set; }
        public int SkillTypeID { get; set; }
        public int Level { get; set; }
        public BlueprintSkill() { }
    }
}
