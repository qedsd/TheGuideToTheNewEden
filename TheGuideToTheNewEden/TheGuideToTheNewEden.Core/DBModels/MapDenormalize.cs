using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    /// <summary>
    /// 星系太空物品
    /// 如主权设施、恒星、行星、月球
    /// </summary>
    [SugarTable("mapDenormalize")]
    public class MapDenormalize
    {
        public int ItemID { get; set; }
        public int TypeID { get; set; }
        public int SolarSystemID { get; set; }
        //public int ConstellationID { get; set; }
        //public int RegionID { get; set; }
        //public int OrbitID { get; set; }
        //public double X { get; set; }
        //public double Y { get; set; }
        //public double Z { get; set; }
        //public double Radius { get; set; }
        //public int NameID { get; set; }
        //public double Security { get; set; }
        //public int CelestialIndex { get; set; }
        //public int OrbitIndex { get; set; }
    }
}
