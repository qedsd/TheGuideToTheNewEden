using SqlSugar;

namespace TheGuideToTheNewEden.Core.DBModels
{
    /// <summary>
    /// 主库 <c>types</c> 的完整列映射（<see cref="InvType"/> 只映射了工业/市场要用到的几列）。
    /// 物品详情窗口需要质量、容量、半径、基础价格、科技等级等展示字段。
    /// </summary>
    [SugarTable("types")]
    public class InvTypeFull
    {
        [SugarColumn(ColumnName = "Id", IsPrimaryKey = true)]
        public int TypeID { get; set; }

        [SugarColumn(ColumnName = "Name", IsNullable = true)]
        public string Name { get; set; }

        [SugarColumn(IsNullable = true)]
        public string Description { get; set; }

        public int GroupID { get; set; }

        [SugarColumn(IsNullable = true)]
        public int? MarketGroupID { get; set; }

        public double Mass { get; set; }
        public double Volume { get; set; }
        public double PackagedVolume { get; set; }
        public int MetaGroupID { get; set; }
        public int PortionSize { get; set; }
        public double BasePrice { get; set; }
        public int IconID { get; set; }
        public bool Published { get; set; }
        public double Capacity { get; set; }
        public int MetaLevel { get; set; }
        public double Radius { get; set; }
    }
}
