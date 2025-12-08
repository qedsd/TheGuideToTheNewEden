using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class MapRegions : BaseModel
    {
        public int NebulaID { get; set; }

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public int WormholeClassID {  get; set; }

        public List<int> ConstellationIDs { get; set; }

        public Position Position { get; set; }
    }
}
