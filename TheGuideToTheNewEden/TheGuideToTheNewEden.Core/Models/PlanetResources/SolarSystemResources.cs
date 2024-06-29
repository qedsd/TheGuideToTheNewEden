using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Models.PlanetResources
{
    public class SolarSystemResources
    {
        public MapSolarSystem MapSolarSystem { get; set; }
        public long Power { get; set; }

        public long Workforce { get; set; }
        public long SuperionicIce { get; set; }
        public long MagmaticGas { get; set; }
    }
}
