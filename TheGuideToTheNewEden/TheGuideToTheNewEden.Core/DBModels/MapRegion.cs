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
        public double XMin { get; set; }
        public double XMax { get; set; }
        public double YMin { get; set; }
        public double YMax { get; set; }
        public double ZMin { get; set; }
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
    }
}
