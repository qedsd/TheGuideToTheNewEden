﻿using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Models.Map
{
    public class MapNavigationPoint: MapSystemInfo
    {
        public int Id { get; set; }
        /// <summary>
        /// 从上一个点旗舰跳到此距离
        /// </summary>
        public double Distance {  get; set; }
        /// <summary>
        /// 从上一个点旗舰跳到此消耗燃料
        /// </summary>
        public double Fuel { get; set; }
        /// <summary>
        /// 0 起始点
        /// 1 星门
        /// 2 诱导
        /// 3 跳桥
        /// </summary>
        public int NavType {  get; set; }

        public string SystemName
        {
            get => $"{System.Security.ToString("N2")} {System.SolarSystemName}";
        }
        public string Sec { get => System.Security.ToString("N2"); }
    }
}
