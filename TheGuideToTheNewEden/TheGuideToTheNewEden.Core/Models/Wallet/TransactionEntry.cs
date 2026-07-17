using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Wallet
{
    public class TransactionEntry
    {
        public TransactionEntry(EVEStandard.Models.WalletTransaction transaction)
        {
            Transaction = transaction;
        }
        public EVEStandard.Models.WalletTransaction Transaction { get; set; }
        public double TotalPrice
        {
            get => Transaction.Quantity * Transaction.UnitPrice;
        }
        public DBModels.InvType InvType { get; set; }
        public string ClientName { get; set; }
        public string LocationName { get; set; }
    }
}
