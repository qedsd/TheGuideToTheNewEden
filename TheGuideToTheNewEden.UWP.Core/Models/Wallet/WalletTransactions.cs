using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.UWP.Core.Models.Wallet
{
    public class WalletTransactions
    {
        public int Client_id { get; set; }
        public string Client_name { get; set; }
        public DateTime Date { get; set; }
        public bool Is_buy { get; set; }
        public bool Is_personal { get; set; }
        public long Journal_ref_id { get; set; }
        public long Location_id { get; set; }
        public int Quantity { get; set; }
        public long Transaction_id { get; set; }
        public int Type_id { get; set; }
        public string Type_name { get; set; }
        public double Unit_price { get; set; }
        public double TotalPrice { get; set; }
    }
}
