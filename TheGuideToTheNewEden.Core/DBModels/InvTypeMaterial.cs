using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("invTypeMaterials")]
    public class InvTypeMaterial
    {
        public int TypeID { get; set; }
        public int MaterialTypeID { get; set; }
        public int Quantity { get; set; }
    }
}
