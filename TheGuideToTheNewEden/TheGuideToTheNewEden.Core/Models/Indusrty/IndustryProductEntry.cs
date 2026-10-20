using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Indusrty
{
    /// <summary>蓝图浏览器的一条：某个产物 + 生产它的蓝图。</summary>
    public class IndustryProductEntry
    {
        public int ProductTypeId { get; set; }
        public string ProductName { get; set; }
        public int BlueprintTypeId { get; set; }
        public string BlueprintName { get; set; }
        /// <summary>生产活动（1 制造 / 11 反应）。</summary>
        public int ActivityId { get; set; }
        public int MaxProductionLimit { get; set; }
        public int GroupId { get; set; }
        public string GroupName { get; set; }
        public int CategoryId { get; set; }
    }
}
