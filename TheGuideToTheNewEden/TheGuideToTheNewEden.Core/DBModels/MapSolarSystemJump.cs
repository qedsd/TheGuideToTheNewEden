using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("mapSolarSystemJumps")]
    public class MapSolarSystemJump
    {
        public int FromRegionID { get; set; }
        public int FromConstellationID { get; set; }
        public int FromSolarSystemID { get; set; }
        public int ToSolarSystemID { get; set; }
        public int ToConstellationID { get; set; }
        public int ToRegionID { get; set; }
    }
}
