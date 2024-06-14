using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("invNames")]
    public class InvName
    {
        [SugarColumn(ColumnName = "itemID")]
        public long ID { get; set; }
        [SugarColumn(ColumnName = "itemName")]
        public string Name { get; set; }
    }
}
