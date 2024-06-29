using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("mapRegions")]
    public class MapRegion : MapRegionBase
    {
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
        public int FactionID { get; set; }
        //public int Nebula { get; set; }
        //public double Radius { get; set; }
    }

    [SugarTable("mapRegions")]
    public class MapRegionBase
    {
        [Display(Order = 1)]
        public int RegionID { get; set; }

        [Display(Order = 2)]
        [SugarColumn(IsNullable = true)]
        public string RegionName { get; set; }

        public override string ToString()
        {
            return RegionName;
        }
        /// <summary>
        /// A-R00001之类的特殊星域
        /// </summary>
        /// <returns></returns>
        public bool IsSpecial()
        {
            return RegionID >= 11000000;
        }
    }
}
