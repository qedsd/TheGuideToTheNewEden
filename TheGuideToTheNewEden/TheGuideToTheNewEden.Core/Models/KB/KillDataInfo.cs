using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;
using ZKB.NET.Models.Statistics;

namespace TheGuideToTheNewEden.Core.Models.KB
{
    public class KillDataInfo: KillData
    {
        public int No { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public string ImgUrl { get; set; }
        public KillDataInfo(KillData killData)
        {
            this.CopyFrom(killData);
        }
    }
}
