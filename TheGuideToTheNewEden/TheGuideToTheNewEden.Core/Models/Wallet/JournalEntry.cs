using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Models.Wallet
{
    public class JournalEntry: ESI.NET.Models.Wallet.JournalEntry
    {
        public JournalEntry(ESI.NET.Models.Wallet.JournalEntry journalEntry) 
        {
            this.CopyFrom(journalEntry);
        }

        public string AmountStr
        {
            get
            {
                var str = Amount.ToString("N2");
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
