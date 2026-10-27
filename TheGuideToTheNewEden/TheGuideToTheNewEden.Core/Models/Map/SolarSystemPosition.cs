using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Map
{
    /// <summary>
    /// 星系位置关系
    /// </summary>
    public class SolarSystemPosition: MapPosition
    {
        public int SolarSystemID { get; set; }
        [JsonIgnore]
        public string SolarSystemName { get; set; }
        /// <summary>
        /// 星门连接可跳跃到的星系
        /// </summary>
        public List<int> JumpTo { get; set; }

        /// <summary>
        /// 2D 坐标落在当前视图的 2%–98% 分位之外（仅用于绘制“几何离群”提示，**不从范围中删除**）。
        /// </summary>
        [JsonIgnore]
        public bool ViewportOutlier { get; set; }
    }
}
