using System;
using System.Collections.Generic;
using System.Text;
using ZKB.NET.Models.Statistics;

namespace TheGuideToTheNewEden.Core.Models.KB
{
    public class MonthGroupData
    {
        public int Year { get; set; }
        public List<MonthData> MonthDatas { get; set; }
    }
}
