using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.ServerLogger.Models
{
    internal class TiebaInfo
    {
        [SugarColumn(IsIdentity = true, IsPrimaryKey = true)]
        public int Id { get; set; }
        public DateTime CreatedTime { get; set; }
        public int Themes { get; set; }
        public int Replys { get; set; }
        public int Members { get; set; }
    }
}
