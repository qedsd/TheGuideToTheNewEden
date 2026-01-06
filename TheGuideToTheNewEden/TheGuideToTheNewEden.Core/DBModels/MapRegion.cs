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
    }

    [SugarTable("mapRegions")]
    public class MapRegionBase
    {
        [Display(Order = 1)]
        [SugarColumn(ColumnName = "Id")]
        public int RegionID { get; set; }

        [Display(Order = 2)]
        [SugarColumn(IsNullable = true, ColumnName = "Name")]
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
