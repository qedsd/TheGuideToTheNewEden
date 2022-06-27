using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models
{
    public class StructureInfo
    {
        public string Name { get; set; }
        public int Owner_id { get; set; }
        public Position Position { get; set; }
        public int Solar_system_id { get; set; }
        public int Type_id { get; set; }
    }
    public class Position
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }
}
