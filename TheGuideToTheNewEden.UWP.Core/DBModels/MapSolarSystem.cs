using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("mapSolarSystems")]
    public class MapSolarSystem : MapSolarSystemBase
    {
        public int RegionID { get; set; }
        public int ConstellationID { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double XMin { get; set; }
        public double XMax { get; set; }
        public double YMin { get; set; }
        public double YMax { get; set; }
        public double ZMin { get; set; }
        public double ZMax { get; set; }
        public double Luminosity { get; set; }
        public bool Border { get; set; }
        public bool Fringe { get; set; }
        public bool Corridor { get; set; }
        public bool Hub { get; set; }
        public bool International { get; set; }
        public bool Regional { get; set; }
        public bool Constellation { get; set; }
        public double Security { get; set; }
        public int FactionID { get; set; }
        public double Radius { get; set; }
        public int SunTypeID { get; set; }
        [SugarColumn(IsNullable = true)]
        public string SecurityClass { get; set; }
    }

    [SugarTable("mapSolarSystems")]
    public class MapSolarSystemBase
    {
        public int SolarSystemID { get; set; }
        [SugarColumn(IsNullable = true)]
        public string SolarSystemName { get; set; }
    }
}
