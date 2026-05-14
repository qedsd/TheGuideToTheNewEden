using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class SovereigntyUpgrades : BaseModel
    {
        public Fuel Fuel { get; set; }
        public string MutuallyExclusiveGroup { get; set; }
        public int? PowerAllocation { get; set; }
        public int? WorkforceAllocation { get; set; }
        public int? PowerProduction { get; set; }
        public int? WorkforceProduction { get; set; }
    }

    public class Fuel
    {
        public int HourlyUpkeep { get; set; }
        public int StartupCost { get; set; }
        public int TypeId { get; set; }
    }
}
