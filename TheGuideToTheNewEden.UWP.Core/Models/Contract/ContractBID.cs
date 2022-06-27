using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Contract
{
    /// <summary>
    /// 拍卖竞价
    /// </summary>
    public class ContractBID
    {
        public float Amount { get; set; }
        public int Bid_Id { get; set; }
        public int Bider_Id { get; set; }
        public DateTime Date_Bid { get; set; }
    }
}
