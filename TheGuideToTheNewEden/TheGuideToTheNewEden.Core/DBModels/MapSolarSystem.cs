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
        public double X2 { get; set; }
        public double Y2 { get; set; }
        [SugarColumn(ColumnName = "SecurityStatus")]
        public double Security { get; set; }
        public int StarID { get; set; }
    }

    [SugarTable("mapSolarSystems")]
    public class MapSolarSystemBase
    {
        [Display(Order = 1)]
        [SugarColumn(ColumnName = "Id")]
        public int SolarSystemID { get; set; }

        [Display(Order = 2)]
        [SugarColumn(IsNullable = true, ColumnName = "Name")]
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
