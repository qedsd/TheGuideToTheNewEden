using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Models.Market
{
    public class Statistic: EVEStandard.Models.MarketRegionHistory
    {
        public Statistic() { }
        public Statistic(EVEStandard.Models.MarketRegionHistory statistic) 
        { 
            this.CopyFrom(statistic);
        }
        public Statistic(EVEStandard.Models.MarketRegionHistory statistic, int invTypeId)
        {
            this.CopyFrom(statistic);
            InvTypeId = invTypeId;
        }
        public int InvTypeId { get; set; }
    }
}
