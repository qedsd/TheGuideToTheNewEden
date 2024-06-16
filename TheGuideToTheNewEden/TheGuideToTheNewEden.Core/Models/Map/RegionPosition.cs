using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Map
{
    public class RegionPosition
    {
        public int RegionId { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public List<int> JumpTo { get; set; }
    }
}
