using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("invMarketGroups")]
    public class InvMarketGroup : InvMarketGroupBase
    {
        public int? ParentGroupID { get; set; }
        public int IconID { get; set; }
        public bool HasTypes { get; set; }
    }

    [SugarTable("invMarketGroups")]
    public class InvMarketGroupBase
    {
        public int MarketGroupID { get; set; }
        [SugarColumn(IsNullable = true)]
        public string MarketGroupName { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Description { get; set; }
    }
}
