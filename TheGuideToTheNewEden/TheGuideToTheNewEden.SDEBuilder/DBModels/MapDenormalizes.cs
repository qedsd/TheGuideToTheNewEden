using System;
using System.Collections.Generic;
using System.Text;
using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    /// <summary>
    /// 星系太空物品
    /// 如主权设施、恒星、行星、月球
    /// </summary>
    [SugarTable("mapDenormalizes")]
    [SugarIndex("index_mapdenormalize_solarsystemid", nameof(SolarSystemID), OrderByType.Asc)]
    public class MapDenormalizes
    {
        /// <summary>
        /// ItemID
        /// </summary>
        [SugarColumn(IsPrimaryKey = true)]
        public int ID { get; set; }
        public int TypeID { get; set; }
        public int GroupID { get; set; }
        public int SolarSystemID { get; set; }

        /// <summary>
        /// 天体半径（米，与 CCP SDE / ESI 一致）；星门、空间站等非天体为 0。
        /// 行星半径即 PI 殖民地 CPU/PG 容量估算的输入。
        /// </summary>
        public double Radius { get; set; }

        [SqlSugar.SugarColumn(IsNullable = true)]
        public string ItemName { get; set; }
    }
}
