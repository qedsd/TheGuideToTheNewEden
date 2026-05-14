using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Map
{
    public class MapPosition
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        /// <summary>
        /// 2D地图坐标X
        /// </summary>
        public double X2 { get; set; }
        /// <summary>
        /// 2D地图坐标Y
        /// </summary>
        public double Y2 { get; set; }
    }
}
