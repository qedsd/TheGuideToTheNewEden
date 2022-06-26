using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.UWP.Core.Models.Contract
{
    /// <summary>
    /// 合同包含物品
    /// </summary>
    public class ContractItem
    {
        public bool Is_Included { get; set; }
        public bool Is_Singleton { get; set; }
        public int Quantity { get; set; }
        /// <summary>
        /// 当不是蓝图时，-1表明该物品不可重叠，为蓝图时，-1表示原图，-2表示拷贝图
        /// </summary>
        public int Raw_Quantity { get; set; }
        public long Record_Id { get; set; }
        public int Type_Id { get; set; }
        public string Type_Name { get; set; }
    }
}
