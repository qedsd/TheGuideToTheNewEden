using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Wallet
{
    public class JournalEntry
    {
        public JournalEntry(ESI.NET.Models.Wallet.JournalEntry journalEntry) 
        {
            Journal = journalEntry;
        }
        public ESI.NET.Models.Wallet.JournalEntry Journal { get; set; }

        public string Amount
        {
            get
            {
                var str = Journal?.Amount.ToString("N2");
                if(str != null && str[0] != '-')
                {
                    return '+' + str;
                }
                else
                {
                    return str;
                }
            }
        }
    }
}
