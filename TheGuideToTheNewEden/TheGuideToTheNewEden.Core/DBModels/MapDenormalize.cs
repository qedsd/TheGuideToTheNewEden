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
    [SugarTable("mapDenormalizes")]
    public class MapDenormalize
    {
        [SugarColumn(ColumnName = "Id")]
        public int ItemID { get; set; }
        public int TypeID { get; set; }
        public int GroupID { get; set; }
        public int SolarSystemID { get; set; }

        /// <summary>
        /// 天体半径（米，与 CCP SDE / ESI 一致）；星门、空间站等非天体为 0。
        /// 行星半径是行星开发 CPU/PG 容量估算的输入，可用 <see cref="RadiusKm"/> 取公里值。
        /// </summary>
        public double Radius { get; set; }

        /// <summary>半径的公里值（界面与行星容量计算使用）。</summary>
        [SugarColumn(IsIgnore = true)]
        public double RadiusKm => Radius / 1000.0;

        public string ItemName { get; set; }
    }
}
