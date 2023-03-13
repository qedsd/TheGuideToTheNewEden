using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models
{
    /// <summary>
    /// 预警记录
    /// </summary>
    public class EarlyWarningContent
    {
        public EarlyWarningContent() 
        {
            //Time = DateTime.Now;
        }
        public DateTime Time { get; set; }
        public int SolarSystemId { get; set; }
        public string SolarSystemName { get; set; }
        public string Content { get; set; }
        /// <summary>
        /// 预警等级
        /// 0最小，表示无危险
        /// </summary>
        public int Level { get; set; }
    }
}
