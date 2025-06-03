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
        public int GroupID { get; set; }
        public int SolarSystemID { get; set; }
        public string ItemName { get; set; }
    }
}
