using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("industryActivityMaterials")]
    public class IndustryActivityMaterial
    {
        public int TypeID { get; set; }
        public int ActivityID { get; set; }
        public int MaterialTypeID { get; set; }
        public int Quantity { get; set; }
    }
}
