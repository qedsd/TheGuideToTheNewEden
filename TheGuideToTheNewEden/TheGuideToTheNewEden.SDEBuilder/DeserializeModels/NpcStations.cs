using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class NpcStations : BaseModel
    {
        public int CelestialIndex { get; set; }
        public int OperationID { get; set; }
        public int OrbitID { get; set; }
        public int? OrbitIndex { get; set; }
        public int OwnerID { get; set; }
        public Position Position { get; set; }
        public double ReprocessingEfficiency { get; set; }
        public int ReprocessingHangarFlag { get; set; }
        public double ReprocessingStationsTake { get; set; }
        public int SolarSystemID { get; set; }
        public int TypeID { get; set; }
        public bool UseOperationName { get; set; }
    }
}
