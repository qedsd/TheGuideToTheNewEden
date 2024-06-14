using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Models.PlanetResources
{
    public class SolarSystemResources
    {
        public MapSolarSystem MapSolarSystem { get; set; }
        public List<PlanetResourcesDetail> PlanetResources { get; set; }
    }
}
