using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Models.Map
{
    public class MapSystemInfo: MapSystemStatistics
    {
        public MapSolarSystem System { get; set; }
        public MapRegion Region { get; set; }
    }
}
