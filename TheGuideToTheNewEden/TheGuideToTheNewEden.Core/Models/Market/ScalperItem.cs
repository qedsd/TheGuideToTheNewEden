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
