using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("marketGroups")]
    public class InvMarketGroup : InvMarketGroupBase
    {
        public int? ParentGroupID { get; set; }
        public int IconID { get; set; }
        public bool HasTypes { get; set; }
    }

    [SugarTable("marketGroups")]
    public class InvMarketGroupBase
    {
        [SugarColumn(ColumnName = "Id")]
        public int MarketGroupID { get; set; }

        [SugarColumn(IsNullable = true, ColumnName = "Name")]
        public string MarketGroupName { get; set; }

        [SugarColumn(IsNullable = true, ColumnName = "Description")]
        public string Description { get; set; }
    }
}
