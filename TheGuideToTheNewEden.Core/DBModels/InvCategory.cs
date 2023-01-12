using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("invCategorys")]
    public class InvCategory : InvCategoryBase
    {
        public int IconID { get; set; }
        public bool Published { get; set; }
    }

    [SugarTable("invCategorys")]
    public class InvCategoryBase
    {
        public int CategoryID { get; set; }
        [SugarColumn(IsNullable = true)]
        public string CategoryName { get; set; }
    }
}
