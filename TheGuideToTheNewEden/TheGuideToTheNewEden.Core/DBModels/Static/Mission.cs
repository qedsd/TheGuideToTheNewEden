using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    public class Mission
    {
        [SugarColumn(IsPrimaryKey = true, ColumnDescription = "索引，主键")]
        public int Id { get; set; }
        public int Level { get; set; }
        public string Title_Zh { get; set; }
        public string Title_En { get; set; }
    }
}
