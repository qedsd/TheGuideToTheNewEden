using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Models.Market
{
    public class ScalperItem
    {
        public InvType InvType { get; set; }
        public List<Core.Models.Market.Order> SellOrders { get; set; }
        public List<Core.Models.Market.Order> BuyOrders { get; set; }
        public List<Core.Models.Market.Statistic> Statistics { get; set; }
    }
}
