using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("categories")]
    public class InvCategory : InvCategoryBase
    {
        public bool Published { get; set; }
    }

    [SugarTable("categories")]
    public class InvCategoryBase
    {
        [SugarColumn(ColumnName = "Id")]
        public int CategoryID { get; set; }

        [SugarColumn(IsNullable = true, ColumnName = "Name")]
        public string CategoryName { get; set; }
    }
}
