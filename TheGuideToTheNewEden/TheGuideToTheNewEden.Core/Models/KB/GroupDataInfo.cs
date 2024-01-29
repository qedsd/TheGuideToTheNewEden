using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;
using ZKB.NET.Models.Statistics;

namespace TheGuideToTheNewEden.Core.Models.KB
{
    public class GroupDataInfo: GroupData
    {
        public int No { get; set; }
        public string Name { get; set; }
        public GroupDataInfo(GroupData groupData)
        {
            this.CopyFrom(groupData);
        }
    }
}
