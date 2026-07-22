using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("typeMaterials")]
    [SugarIndex("index_typeMaterials_typeID", nameof(TypeID), OrderByType.Asc)]
    public class TypeMaterials
    {
        public int TypeID { get; set; }
        public int MaterialTypeID { get; set; }
        public int Quantity { get; set; }
        public TypeMaterials() { }
    }
}
