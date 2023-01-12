using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("industryActivityProducts")]
    public class IndustryActivityProduct
    {
        public int TypeID { get; set; }
        public int ActivityID { get; set; }
        public int ProductTypeID { get; set; }
        public int Quantity { get; set; }
    }
}
