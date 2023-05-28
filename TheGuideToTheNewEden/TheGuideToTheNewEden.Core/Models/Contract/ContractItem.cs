using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Models.Contract
{
    /// <summary>
    /// 合同包含物品
    /// Raw_Quantity 当不是蓝图时，-1表明该物品不可重叠，为蓝图时，-1表示原图，-2表示拷贝图
    /// </summary>
    public class ContractItem: ESI.NET.Models.Contracts.ContractItem
    {
        public string TypeName { get; set; }
        public bool IsBlueprint { get; set; }
        public ContractItem(ESI.NET.Models.Contracts.ContractItem contractItem) 
        { 
            this.CopyFrom(contractItem);
        }
    }
}
