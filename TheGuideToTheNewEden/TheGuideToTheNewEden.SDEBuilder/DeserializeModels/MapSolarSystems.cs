using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    internal class MapSolarSystems : BaseModel
    {
        public int RegionID { get; set; }
        public int ConstellationID { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Luminosity { get; set; }
        public bool Border { get; set; }
        public bool Corridor { get; set; }
        public double SecurityStatus { get; set; }
        public string SecurityClass { get; set; }
        public double Radius { get; set; }
        public int StarID { get; set; }
        public int WormholeClassID { get; set; }

        public List<int> PlanetIDs { get; set; }

        public Position Position { get; set; }

        public Position Position2D { get; set; }

        public List<int> StargateIDs { get; set; }
    }
}
