using SqlSugar;

namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("industryActivities")]
    public class IndustryActivityDefinition
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public string Name { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Description { get; set; }
    }

    [SugarTable("industryAssemblyLines")]
    public class IndustryAssemblyLine
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public int ActivityID { get; set; }
        public string Name { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Description { get; set; }
        public double BaseCostMultiplier { get; set; }
        public double BaseMaterialMultiplier { get; set; }
        public double BaseTimeMultiplier { get; set; }
    }

    [SugarTable("industryAssemblyLineDetails")]
    public class IndustryAssemblyLineDetail
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
        public int AssemblyLineID { get; set; }
        public bool IsCategory { get; set; }
        public int TargetID { get; set; }
        public double CostMultiplier { get; set; }
        public double MaterialMultiplier { get; set; }
        public double TimeMultiplier { get; set; }
    }

    [SugarTable("industryInstallationTypes")]
    public class IndustryInstallationType
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int InstallationTypeID { get; set; }
        public string AssemblyLineIDs { get; set; }
    }

    [SugarTable("industryModifierSources")]
    public class IndustryModifierSource
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
        public int SourceTypeID { get; set; }
        public int ActivityID { get; set; }
        public int ModifierType { get; set; }
        public int DogmaAttributeID { get; set; }
        /// <summary>目标过滤器 ID；0 = 无过滤器（结构本体）。</summary>
        public int FilterID { get; set; }
    }

    [SugarTable("industryTargetFilters")]
    public class IndustryTargetFilterDefinition
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public string Name { get; set; }
    }

    [SugarTable("industryTargetFilterCategories")]
    public class IndustryTargetFilterCategory
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
        public int FilterID { get; set; }
        public int CategoryID { get; set; }
    }

    [SugarTable("industryTargetFilterGroups")]
    public class IndustryTargetFilterGroup
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
        public int FilterID { get; set; }
        public int GroupID { get; set; }
    }
}
