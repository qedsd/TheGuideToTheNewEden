using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class MapConstellations : BaseModel
    {
        public int? FactionID { get; set; }
        public Position Position { get; set; }
        public int RegionID { get; set; }
        public List<int> SolarSystemIDs { get; set; }
        public int? WormholeClassID { get; set; }
    }
}
