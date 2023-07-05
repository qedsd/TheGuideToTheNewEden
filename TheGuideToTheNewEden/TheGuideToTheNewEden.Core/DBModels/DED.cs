using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("DED")]
    public class DED
    {
        public int Id { get; set; }
        /// <summary>
        /// 0无人机 1天蛇 2血袭者 3萨沙 4天使 5古斯塔斯
        /// </summary>
        public int Type { get; set; }
        /// <summary>
        /// 1-10表示x/10，0表示未定级
        /// </summary>
        public int Level { get; set; }
        public string TitleCN { get; set; }
        public string TitleEN { get; set; }
        public string Content { get; set; }
    }
}
