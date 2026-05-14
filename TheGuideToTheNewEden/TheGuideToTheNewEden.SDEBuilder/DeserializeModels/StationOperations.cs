using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class StationOperations : BaseModel
    {
        public int ActivityID { get; set; }
        public double Border { get; set; }
        public double Corridor { get; set; }
        public double Fringe { get; set; }
        public double Hub { get; set; }
        public double ManufacturingFactor { get; set; }
        public Languages OperationName { get; set; }
        public double Ratio { get; set; }
        public double ResearchFactor { get; set; }
        public List<int> Services { get; set; }
        public List<StationTypeItem> StationTypes { get; set; }
    }

    public class StationTypeItem
    {
        public long ValueKey { get; set; }
        public int Value { get; set; }
    }
}
