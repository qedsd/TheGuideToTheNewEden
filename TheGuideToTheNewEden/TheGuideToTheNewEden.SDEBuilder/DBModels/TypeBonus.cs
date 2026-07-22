using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("typeBonus")]
    [SugarIndex("index_typeBonus_typeID", nameof(TypeID), OrderByType.Asc)]
    public class TypeBonus
    {
        public int TypeID { get; set; }
        public int SkillTypeID { get; set; }
        public double Bonus { get; set; }
        public int Importance { get; set; }
        public int UnitID { get; set; }
        [SugarColumn(IsNullable = true)]
        public string BonusText { get; set; }
        [SugarColumn(IsNullable = true)]
        public string BonusTextEn { get; set; }
        public TypeBonus() { }
    }
}
