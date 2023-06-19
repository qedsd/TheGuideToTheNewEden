using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Models.Market
{
    public class ScalperItem
    {
        public InvType InvType { get; set; }
        public List<Core.Models.Market.Order> SourceSellOrders { get; set; }
        public List<Core.Models.Market.Order> SourceBuyOrders { get; set; }
        public List<Core.Models.Market.Statistic> SourceStatistics { get; set; }
        public List<Core.Models.Market.Order> DestinationSellOrders { get; set; }
        public List<Core.Models.Market.Order> DestinationBuyOrders { get; set; }
        public List<Core.Models.Market.Statistic> DestinationStatistics { get; set; }
        public double SellPrice { get; set; }
        public double BuyPrice { get; set;}
        /// <summary>
        /// 源市场销量
        /// </summary>
        public long SourceSales { get; set; }
        /// <summary>
        /// 目的市场销量
        /// </summary>
        public long DestinationSales { get; set; }
    }
}
