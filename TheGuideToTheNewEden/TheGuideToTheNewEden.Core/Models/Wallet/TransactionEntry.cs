using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Wallet
{
    public class TransactionEntry
    {
        public TransactionEntry(ESI.NET.Models.Wallet.Transaction transaction)
        {
            Transaction = transaction;
        }
        public ESI.NET.Models.Wallet.Transaction Transaction { get; set; }
        public decimal AutoTotalPrice
        {
            get => Transaction.Quantity * Transaction.UnitPrice;
        }
        public DBModels.InvType InvType { get; set; }
        public string ClientName { get; set; }
        public string LocationName { get; set; }
    }
}
