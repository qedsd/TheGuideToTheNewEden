using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.UWP.Core.Models.Contract
{
    public class Contract
    {
        /// <summary>
        /// 最后谁接收了
        /// </summary>
        public int Acceptor_Id { get; set; }
        /// <summary>
        /// 合同指派给谁（可个人军团）
        /// </summary>
        public int Assignee_Id { get; set; }
        /// <summary>
        /// [ public, personal, corporation, alliance ]
        /// </summary>
        public string Availability { get; set; }
        /// <summary>
        /// 拍卖一口价
        /// </summary>
        public double Buyout { get; set; }
        /// <summary>
        /// 快递合同的押金
        /// </summary>
        public double Collateral { get; set; }
        public int Contract_Id { get; set; }
        public DateTime Date_Completed { get; set; }
        public DateTime Date_Accepted { get; set; }
        public DateTime Date_Expired { get; set; }
        public DateTime Date_Issued { get; set; }
        public int Days_To_Complete { get; set; }
        /// <summary>
        /// 快递目的地
        /// </summary>
        public long End_Location_Id { get; set; }
        public bool For_Crporation { get; set; }
        public int Issuer_Corporation { get; set; }
        public int Issuer_Id { get; set; }
        /// <summary>
        /// 将要支付的合同价格，若为拍卖则为起拍价
        /// </summary>
        public double Price { get; set; }
        /// <summary>
        /// 将获得的合同价格（包括快递合同奖励）
        /// </summary>
        public double Reward { get; set; }
        public long Start_Location_Id { get; set; }
        /// <summary>
        /// [ outstanding, in_progress, finished_issuer, finished_contractor, finished, cancelled, rejected, failed, deleted, reversed ]
        /// </summary>
        public string Status { get; set; }
        public string Title { get; set; }
        /// <summary>
        /// [ unknown, item_exchange, auction, courier, loan ]
        /// </summary>
        public string Type { get; set; }
        public double Volume { get; set; }

        public List<ContractBID> ContractBIDs { get; set; }
        public List<ContractItem> ContractItems { get; set; }
    }
}
