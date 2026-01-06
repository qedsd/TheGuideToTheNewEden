using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("planetResources")]
    public class PlanetResources
    {
        [SugarColumn(IsPrimaryKey = true, ColumnName = "Id")]
        public int StarID { get; set; }

        public int Power { get; set; }

        public int Workforce { get; set; }

        public int AmountPerCycle { get; set; }

        public int TypeId { get; set; }
    }
}
