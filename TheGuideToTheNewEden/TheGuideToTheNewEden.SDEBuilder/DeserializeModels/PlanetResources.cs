using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class PlanetResources : BaseModel
    {
        public int? Power { get; set; }
        public int? Workforce { get; set; }
        public Reagent Reagent { get; set; }
    }

    public class Reagent
    {
        public int AmountPerCycle { get; set; }
        public int CyclePeriod { get; set; }
        public long SecuredCapacity { get; set; }
        public int TypeId { get; set; }
        public long UnsecuredCapacity { get; set; }
    }
}
