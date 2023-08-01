using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    public class Wormhole
    {
        [SugarColumn(IsPrimaryKey = true, ColumnDescription = "索引，主键")]
        public int Id { get; set; }
        public string Name { get; set; }
        public int Class { get; set; }
        public int Phenomena { get; set; }
        /// <summary>
        /// 永联洞
        /// 以,分割
        /// </summary>
        public string Statics { get; set; }
        /// <summary>
        /// 随机洞
        /// 以,分割
        /// </summary>
        public string Wanderings { get; set; }
    }
}
