using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    /// <summary>
    /// types 表的轻量映射，专取舰船质量（过洞计算用）。
    /// <see cref="InvType"/> 未映射 Mass 列；单独建模型避免给全量查询加列。
    /// </summary>
    [SugarTable("types")]
    public class InvTypeMass
    {
        [SugarColumn(ColumnName = "Id")]
        public int TypeID { get; set; }

        [SugarColumn(IsNullable = true, ColumnName = "Name")]
        public string TypeName { get; set; }

        public double Mass { get; set; }

        public int GroupID { get; set; }

        /// <summary>市场分组：非空 = 玩家可在市场上买到的船（用于排除 NPC 专属船）。</summary>
        [SugarColumn(IsNullable = true, ColumnName = "MarketGroupID")]
        public int? MarketGroupID { get; set; }
    }
}
