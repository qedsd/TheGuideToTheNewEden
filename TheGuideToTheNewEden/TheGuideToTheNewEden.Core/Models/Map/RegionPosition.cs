using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Map
{
    public class RegionPosition : MapPosition
    {
        public int RegionId { get; set; }
        public List<int> JumpTo { get; set; }
    }
}
