using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    /// <summary>
    /// 工业活动 ID（与 SDE <c>blueprintActivities.ActivityID</c> 对应）。
    /// </summary>
    public static class IndustryActivity
    {
        public const int Manufacturing = 1;
        public const int ResearchTechnology = 2;
        public const int ResearchTimeEfficiency = 3;
        public const int ResearchMaterialEfficiency = 4;
        public const int Copying = 5;
        public const int Duplicating = 6;
        public const int ReverseEngineering = 7;
        /// <summary>内部活动：行星开发（行星 schematic，不是工业 job）。</summary>
        public const int Planetary = 100;
        public const int Invention = 8;
        public const int Reaction = 11;
        /// <summary>官方 SDE/ESI 的反应活动 ID（应用内部旧库仍用 11）。</summary>
        public const int OfficialReaction = 9;
    }

    /// <summary>蓝图头（SDE <c>blueprints</c>）。<see cref="BlueprintTypeID"/> 即蓝图物品的类型 ID。</summary>
    [SugarTable("blueprints")]
    public class Blueprint
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public int BlueprintTypeID { get; set; }
        /// <summary>单张 BPC 的最大流程数（0 = BPO / 不限制）。</summary>
        public int MaxProductionLimit { get; set; }
    }

    /// <summary>蓝图各活动的基础耗时（SDE <c>blueprintActivities</c>）。</summary>
    [SugarTable("blueprintActivities")]
    public class BlueprintActivity
    {
        public int BlueprintTypeID { get; set; }
        public int ActivityID { get; set; }
        /// <summary>基础耗时（秒，未套 TE）。</summary>
        public int Time { get; set; }
    }

    /// <summary>蓝图单流程材料（SDE <c>blueprintMaterials</c>）。</summary>
    [SugarTable("blueprintMaterials")]
    public class BlueprintMaterial
    {
        public int BlueprintTypeID { get; set; }
        public int ActivityID { get; set; }
        public int MaterialTypeID { get; set; }
        /// <summary>单流程所需数量（未套 ME）。</summary>
        public int Quantity { get; set; }
    }

    /// <summary>蓝图单流程产物（SDE <c>blueprintProducts</c>）。发明产物带成功率。</summary>
    [SugarTable("blueprintProducts")]
    public class BlueprintProduct
    {
        public int BlueprintTypeID { get; set; }
        public int ActivityID { get; set; }
        public int ProductTypeID { get; set; }
        public int Quantity { get; set; }
        /// <summary>成功率（发明 &lt; 1）；制造/反应为 NULL。</summary>
        [SugarColumn(IsNullable = true)]
        public double? Probability { get; set; }
    }

    /// <summary>蓝图各活动的技能需求（SDE <c>blueprintSkills</c>）。</summary>
    [SugarTable("blueprintSkills")]
    public class BlueprintSkill
    {
        public int BlueprintTypeID { get; set; }
        public int ActivityID { get; set; }
        public int SkillTypeID { get; set; }
        public int Level { get; set; }
    }

    /// <summary>物品回收/精炼产出（SDE <c>typeMaterials</c>）。</summary>
    [SugarTable("typeMaterials")]
    public class TypeMaterial
    {
        public int TypeID { get; set; }
        public int MaterialTypeID { get; set; }
        public int Quantity { get; set; }
    }

    /// <summary>物品 dogma 属性值（SDE <c>typeDogma</c>）。工业侧用于读取蓝图/装备的 ME/TE 相关属性。</summary>
    [SugarTable("typeDogma")]
    public class TypeDogma
    {
        public int TypeID { get; set; }
        public int AttributeID { get; set; }
        public double Value { get; set; }
    }

    /// <summary>可压缩物品 → 压缩后物品（SDE <c>compressibleTypes</c>）。</summary>
    [SugarTable("compressibleTypes")]
    public class CompressibleType
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public int CompressedTypeID { get; set; }
    }
}
