using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.UWP.Core.Models.Wallet
{
    public class Walletjournal
    {
        public double Amount { get; set; }
        public double Balance { get; set; }
        public long Context_id { get; set; }
        public string Context_id_type { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; }
        public int First_party_id { get; set; }
        public long Id { get; set; }
        public string Reason { get; set; }
        public string Ref_type { get; set; }
        public int Second_party_id { get; set; }
        public double Tax { get; set; }
        public int Tax_receiver_id { get; set; }
    }
}
