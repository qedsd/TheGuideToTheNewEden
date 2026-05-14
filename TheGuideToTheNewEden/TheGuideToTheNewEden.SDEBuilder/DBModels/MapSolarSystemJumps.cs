using System;
using System.Collections.Generic;
using System.Text;
using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("mapSolarSystemJumps")]
    [SugarIndex("index_mapSolarSystemJumps_from_solarsystem_id", nameof(FromSolarSystemID), OrderByType.Asc)]
    public class MapSolarSystemJumps
    {
        public int FromSolarSystemID { get; set; }
        public int FromConstellationID { get; set; }
        public int FromRegionID { get; set; }
        public int ToSolarSystemID { get; set; }
        public int ToConstellationID { get; set; }
        public int ToRegionID { get; set; }
    }
}
