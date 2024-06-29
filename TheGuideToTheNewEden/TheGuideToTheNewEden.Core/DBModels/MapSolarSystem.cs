using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
        [SugarColumn(ColumnName = "x_Min")]
        public double XMin { get; set; }
        [SugarColumn(ColumnName = "x_Max")]
        public double XMax { get; set; }
        [SugarColumn(ColumnName = "y_Min")]
        public double YMin { get; set; }
        [SugarColumn(ColumnName = "y_Max")]
        public double YMax { get; set; }
        [SugarColumn(ColumnName = "z_Min")]
        public double ZMin { get; set; }
        [SugarColumn(ColumnName = "z_Max")]
        public double ZMax { get; set; }
        public double Luminosity { get; set; }
        public bool Border { get; set; }
        public bool Fringe { get; set; }
        public bool Corridor { get; set; }
        public bool Hub { get; set; }
        public bool International { get; set; }
        public bool Regional { get; set; }
        //public bool Constellation { get; set; }
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
        [Display(Order = 1)]
        public int SolarSystemID { get; set; }

        [Display(Order = 2)]
        [SugarColumn(IsNullable = true)]
        public string SolarSystemName { get; set; }

        public override string ToString()
        {
            return SolarSystemName;
        }
        /// <summary>
        /// 虫洞、希拉之类的特殊星系
        /// </summary>
        /// <returns></returns>
        public bool IsSpecial()
        {
            return SolarSystemID >= 31000000;
        }
    }
}
