using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZKB.NET.Models.Statistics
{
    public class Activepvp
    {
        public ActivepvpData Characters { get; set; }
        public ActivepvpData Corporations { get; set; }
        public ActivepvpData Ships { get; set; }
        public ActivepvpData Systems { get; set; }
        public ActivepvpData Regions { get; set; }
        public ActivepvpData Kills { get; set; }
    }
    public class ActivepvpData
    {
        /// <summary>
        /// Characters/Corporations/Ships/Systems/Regions/Total Kills
        /// </summary>
        public string Type { get; set; }
        public int Count { get; set; }
    }
}
