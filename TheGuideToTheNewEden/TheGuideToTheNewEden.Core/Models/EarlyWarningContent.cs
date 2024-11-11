using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Enums;

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
        public string Listener { get; set; }
        public DateTime Time { get; set; }
        public int SolarSystemId { get; set; }
        public string SolarSystemName { get; set; }
        public string Content { get; set; }
        /// <summary>
        /// 预警等级
        /// 0最小，表示无危险
        /// </summary>
        public int Level { get; set; }
        public IntelChatType IntelType { get; set; }

        public Map.IntelSolarSystemMap IntelMap { get; set; }

        public int Jumps { get; set; }
    }
}
