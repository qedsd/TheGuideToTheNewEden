using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class MapStargates : BaseModel
    {
        public Destination Destination { get; set; }
        public Position Position { get; set; }
        public int SolarSystemID { get; set; }
        public int TypeID { get; set; }
    }

    public class Destination
    {
        public int SolarSystemID { get; set; }
        public int StargateID { get; set; }
    }
}
