using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Map
{
    /// <summary>
    /// 星系位置关系
    /// </summary>
    public class SolarSystemPosition
    {
        public int SolarSystemID { get; set; }
        [JsonIgnore]
        public string SolarSystemName { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        /// <summary>
        /// 星门连接可跳跃到的星系
        /// </summary>
        public List<int> JumpTo { get; set; }
    }
    public class SolarSystemPosition2: SolarSystemPosition
    {
        public double Sec { get; set; }
    }
}
