using SqlSugar;

namespace TheGuideToTheNewEden.Core.DBModels
{
    /// <summary>SDE sovereigntyUpgrades：设施升级的产能 / 人力分配与燃料维护。</summary>
    [SugarTable("sovereigntyUpgrades")]
    public class SovereigntyUpgrade
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int TypeID { get; set; }

        public int? PowerAllocation { get; set; }
        public int? WorkforceAllocation { get; set; }
        public int? PowerProduction { get; set; }
        public int? WorkforceProduction { get; set; }

        public int? FuelTypeID { get; set; }
        public int? FuelHourlyUpkeep { get; set; }
        public int? FuelStartupCost { get; set; }

        [SugarColumn(IsNullable = true)]
        public string? MutuallyExclusiveGroup { get; set; }
    }
}
