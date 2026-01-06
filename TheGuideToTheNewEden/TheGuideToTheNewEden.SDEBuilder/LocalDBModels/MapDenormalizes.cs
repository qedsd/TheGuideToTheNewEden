using System;
using System.Collections.Generic;
using System.Text;
using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.LocalDBModels
{
    /// <summary>
    /// 星系太空物品
    /// 如主权设施、恒星、行星、月球
    /// </summary>
    [SugarTable("mapDenormalize")]
    [SugarIndex("index_mapdenormalize_solarsystemid", nameof(ID), OrderByType.Asc)]
    public class MapDenormalizes
    {
        /// <summary>
        /// ItemID
        /// </summary>
        [SugarColumn(IsPrimaryKey = true)]
        public int ID { get; set; }

        [SqlSugar.SugarColumn(IsNullable = true)]
        public string ItemName { get; set; }

        public MapDenormalizes() { }
        public MapDenormalizes(DBModels.MapDenormalizes model)
        {
            ID = model.ID;
            ItemName = model.ItemName;
        }
    }
}
