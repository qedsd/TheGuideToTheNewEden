using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    public class BackstoryContent
    {
        [SugarColumn(IsPrimaryKey = true, ColumnDescription = "索引，主键")]
        public int Id { get; set; }
        public string Content { get; set; }
    }
}
