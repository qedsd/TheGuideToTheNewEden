using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Models.Market
{
    public class Statistic: ESI.NET.Models.Market.Statistic
    {
        public Statistic() { }
        public Statistic(ESI.NET.Models.Market.Statistic statistic) 
        { 
            this.CopyFrom(statistic);
        }
        public Statistic(ESI.NET.Models.Market.Statistic statistic, int invTypeId)
        {
            this.CopyFrom(statistic);
            InvTypeId = invTypeId;
        }
        public int InvTypeId { get; set; }
    }
}
