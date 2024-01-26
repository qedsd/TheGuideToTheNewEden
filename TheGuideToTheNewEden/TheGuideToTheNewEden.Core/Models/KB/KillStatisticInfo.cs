using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.KB
{
    public class KillStatisticInfo
    {
        public string Type { get; set; }
        public List<KillDataInfo> KillDataInfos { get; set; }
    }
}
