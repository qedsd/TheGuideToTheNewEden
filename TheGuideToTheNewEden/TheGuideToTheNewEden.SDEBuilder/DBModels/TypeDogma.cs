using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("typeDogma")]
    [SugarIndex("index_typeDogma_typeID", nameof(TypeID), OrderByType.Asc)]
    [SugarIndex("index_typeDogma_attributeID", nameof(AttributeID), OrderByType.Asc)]
    public class TypeDogma
    {
        public int TypeID { get; set; }
        public int AttributeID { get; set; }
        public double Value { get; set; }
        public TypeDogma() { }
    }
}
