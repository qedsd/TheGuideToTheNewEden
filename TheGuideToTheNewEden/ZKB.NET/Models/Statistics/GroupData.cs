using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZKB.NET.Models.Statistics
{
    /// <summary>
    /// 分类击杀损失数据
    /// </summary>
    public class GroupData: KillAndLoseDataBase
    {
        /// <summary>
        /// 物品组类别--数据库invGroups
        /// </summary>
        public int GroupID { get; set; }
    }
}
