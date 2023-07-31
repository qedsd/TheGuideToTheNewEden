using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WhomholeCrawler
{
    internal class CaveModel
    {
        public string Destination { get; set; }
        public string AppearsIn { get; set; }
        public float Lifetime { get; set; }
        public long MaxMassPerJump { get; set; }
        public string MaxMassPerJumpNote { get; set; }
        public long TotalJumpMass { get; set; }
        public string TotalJumpMassNote { get; set; }
        public string Respawn { get; set; }
        public long MassRegen { get; set;}
    }
}
