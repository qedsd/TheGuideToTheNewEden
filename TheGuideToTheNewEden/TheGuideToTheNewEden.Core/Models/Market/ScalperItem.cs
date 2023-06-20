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
        /// <summary>
        /// 推荐度
        /// </summary>
        public double Suggestion { get; set; }
        /// <summary>
        /// 回报率
        /// </summary>
        public double ROI { get; set; }
        /// <summary>
        /// 净利润
        /// </summary>
        public double NetProfit { get; set; }
        /// <summary>
        /// 本金
        /// </summary>
        public double Principal { get; set; }
        /// <summary>
        /// 历史价格波动
        /// </summary>
        public double HistoryPriceFluctuation { get; set; }
        /// <summary>
        /// 当前价格波动
        /// </summary>
        public double NowPriceFluctuation { get; set; }
        /// <summary>
        /// 订单饱和度
        /// </summary>
        public double Saturation { get; set; }
    }
}
