using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.DevTools.Map
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
