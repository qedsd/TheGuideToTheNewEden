using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.ServerLogger.Models
{
    internal class ServerStatus
    {
        [SugarColumn(IsIdentity = true,IsPrimaryKey = true)]
        public int Id { get; set; }
        public DateTime CreatedTime { get; set; }
        public int Players { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Server_Version { get; set; }
        public DateTime Start_Time { get; set; }
        public bool Vip { get; set; }
    }
}
