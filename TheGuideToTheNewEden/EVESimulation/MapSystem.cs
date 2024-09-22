using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVESimulation
{
    internal class MapSystem
    {
        public int Id {  get; set; }
        public string Name { get; set; }
        public int RegionId { get; set; }
    }

    internal class MapRegion
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
