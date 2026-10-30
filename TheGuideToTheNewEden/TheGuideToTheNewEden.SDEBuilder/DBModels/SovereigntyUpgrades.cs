using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    /// <summary>SDE sovereigntyUpgrades：设施升级的产能 / 人力分配与燃料维护。</summary>
    [SugarTable("sovereigntyUpgrades")]
    [SugarIndex("index_sovereigntyUpgrades_typeID", nameof(TypeID), OrderByType.Asc)]
    public class SovereigntyUpgrades
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
        public string MutuallyExclusiveGroup { get; set; }

        public SovereigntyUpgrades() { }

        public SovereigntyUpgrades(DeserializeModels.BaseModel model)
        {
            if (!(model is DeserializeModels.SovereigntyUpgrades data))
            {
                return;
            }

            TypeID = data.Id;
            PowerAllocation = data.PowerAllocation;
            WorkforceAllocation = data.WorkforceAllocation;
            PowerProduction = data.PowerProduction;
            WorkforceProduction = data.WorkforceProduction;
            MutuallyExclusiveGroup = data.MutuallyExclusiveGroup;

            if (data.Fuel != null)
            {
                FuelTypeID = data.Fuel.TypeId;
                FuelHourlyUpkeep = data.Fuel.HourlyUpkeep;
                FuelStartupCost = data.Fuel.StartupCost;
            }
        }
    }
}
