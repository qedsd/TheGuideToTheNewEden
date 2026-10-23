using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
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
        public double BaseCostMultiplier { get; set; } = 1.0;
        public double BaseMaterialMultiplier { get; set; } = 1.0;
        public double BaseTimeMultiplier { get; set; } = 1.0;
    }

    [SugarTable("industryAssemblyLineDetails")]
    [SugarIndex("index_industryAssemblyLineDetails_line", nameof(AssemblyLineID), OrderByType.Asc)]
    public class IndustryAssemblyLineDetail
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
        public int AssemblyLineID { get; set; }
        /// <summary>true = 按分类；false = 按组。</summary>
        public bool IsCategory { get; set; }
        public int TargetID { get; set; }
        public double CostMultiplier { get; set; } = 1.0;
        public double MaterialMultiplier { get; set; } = 1.0;
        public double TimeMultiplier { get; set; } = 1.0;
    }

    [SugarTable("industryInstallationTypes")]
    public class IndustryInstallationType
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int InstallationTypeID { get; set; }
        /// <summary>装配线 ID 列表，逗号分隔；保持源文件一行一条。</summary>
        public string AssemblyLineIDs { get; set; }
    }

    [SugarTable("industryModifierSources")]
    [SugarIndex("index_industryModifierSources_source", nameof(SourceTypeID), OrderByType.Asc)]
    public class IndustryModifierSource
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
        public int SourceTypeID { get; set; }
        /// <summary>官方活动 ID（反应为 9）。</summary>
        public int ActivityID { get; set; }
        /// <summary>1=cost, 2=time, 3=material。</summary>
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
    [SugarIndex("index_industryTargetFilterCategories_filter", nameof(FilterID), OrderByType.Asc)]
    public class IndustryTargetFilterCategory
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
        public int FilterID { get; set; }
        public int CategoryID { get; set; }
    }

    [SugarTable("industryTargetFilterGroups")]
    [SugarIndex("index_industryTargetFilterGroups_filter", nameof(FilterID), OrderByType.Asc)]
    public class IndustryTargetFilterGroup
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
        public int FilterID { get; set; }
        public int GroupID { get; set; }
    }
}
