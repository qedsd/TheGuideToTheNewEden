using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZKB.NET.Models.Statistics.Top
{
    public class TopSolarSystem:TopBase
    {
        public int SunTypeID { get; set; }
        public float SolarSystemSecurity { get; set; }
        public string SystemColorCode { get; set; }
        public int ConstellationID { get; set; }
        public string ConstellationName { get; set; }
        //public int RegionID { get; set; }
    }
}
