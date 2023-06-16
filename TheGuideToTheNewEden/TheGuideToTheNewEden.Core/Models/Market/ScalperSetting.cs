using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using TheGuideToTheNewEden.Core.Interfaces;

namespace TheGuideToTheNewEden.Core.Models.Market
{
    public class ScalperSetting : ObservableObject
    {
        #region 市场位置
        private MarketLocation sourceMarketLocation;
        /// <summary>
        /// 源市场
        /// </summary>
        public MarketLocation SourceMarketLocation
        {
            get => sourceMarketLocation;
            set => SetProperty(ref sourceMarketLocation, value);
        }

        private MarketLocation destinationMarketLocation;
        /// <summary>
        /// 目的地市场
        /// </summary>
        public MarketLocation DestinationMarketLocation
        {
            get => destinationMarketLocation;
            set => SetProperty(ref destinationMarketLocation, value);
        }

        private int marketGroup = 9;
        /// <summary>
        /// 物品类型
        /// </summary>
        public int MarketGroup
        {
            get => marketGroup; set => SetProperty(ref marketGroup, value);
        }
        #endregion

        #region 买入卖出价格
        private PriceType buyPrice = PriceType.SellTop5;
        /// <summary>
        /// 买入价格方式
        /// </summary>
        public PriceType BuyPrice
        {
            get => buyPrice;
            set
            {
                SetProperty(ref buyPrice, value);
                IsHistoryBuyPrice = (value == PriceType.HistoryLowest || value == PriceType.HistoryHighest || value == PriceType.HistoryAverage);
            }
        }

        private double buyPriceScale = 1;
        /// <summary>
        /// 买入价格缩放系数
        /// </summary>
        public double BuyPriceScale
        {
            get => buyPriceScale; set => SetProperty(ref buyPriceScale, value);
        }

        private bool isHistoryBuyPrice;
        /// <summary>
        /// 买入价格是否为历史价格
        /// </summary>
        [JsonIgnore]
        public bool IsHistoryBuyPrice
        {
            get => isHistoryBuyPrice; set => SetProperty(ref isHistoryBuyPrice, value);
        }

        private int buyHistoryDay = 7;
        /// <summary>
        /// 买入历史价格天数
        /// </summary>
        public int BuyHistoryDay
        {
            get => buyHistoryDay; set => SetProperty(ref buyHistoryDay, value);
        }

        private PriceType sellPrice = PriceType.SellTop5;
        /// <summary>
        /// 卖出价格方式
        /// </summary>
        public PriceType SellPrice
        {
            get => sellPrice;
            set
            {
                SetProperty(ref sellPrice, value);
                IsHistorySellPrice = (value == PriceType.HistoryLowest || value == PriceType.HistoryHighest || value == PriceType.HistoryAverage);
            }
        }

        private double sellPriceScale = 1;
        /// <summary>
        /// 卖出价格缩放系数
        /// </summary>
        public double SellPriceScale
        {
            get => sellPriceScale; set => SetProperty(ref sellPriceScale, value);
        }

        private bool isHistorySellPrice;
        /// <summary>
        /// 卖出价格是否为历史价格
        /// </summary>
        [JsonIgnore]
        public bool IsHistorySellPrice
        {
            get => isHistorySellPrice; set => SetProperty(ref isHistorySellPrice, value);
        }

        private int sellHistoryDay = 7;
        /// <summary>
        /// 卖出历史价格天数
        /// </summary>
        public int SellHistoryDay
        {
            get => sellHistoryDay; set => SetProperty(ref sellHistoryDay, value);
        }
        #endregion

        #region 销量计算
        private int sourceSalesDay = 7;
        /// <summary>
        /// 源市场销量计算天数
        /// </summary>
        public int SourceSalesDay
        {
            get => sourceSalesDay; set => SetProperty(ref sourceSalesDay, value);
        }

        private bool sourceRemoveExtremum = true;
        /// <summary>
        /// 源市场销量计算去除极值
        /// </summary>
        public bool SourceRemoveExtremum
        {
            get => sourceRemoveExtremum; set => SetProperty(ref sourceRemoveExtremum, value);
        }

        private int destinationSalesDay = 7;
        /// <summary>
        /// 目的市场销量计算天数
        /// </summary>
        public int DestinationSalesDay
        {
            get => destinationSalesDay; set => SetProperty(ref destinationSalesDay, value);
        }

        private bool destinationRemoveExtremum = true;
        /// <summary>
        /// 目的市场销量计算去除极值
        /// </summary>
        public bool DestinationRemoveExtremum
        {
            get => destinationRemoveExtremum; set => SetProperty(ref destinationRemoveExtremum, value);
        }
        #endregion

        #region 目的地历史价格波动
        private int historyPriceFluctuationDay = 7;
        /// <summary>
        /// 历史价格波动计算天数
        /// </summary>
        public int HistoryPriceFluctuationDay
        {
            get => historyPriceFluctuationDay; set => SetProperty(ref historyPriceFluctuationDay, value);
        }
        private double historyPriceFluctuation = 7;
        /// <summary>
        /// 历史价格波动值，越小越好
        /// 标准差/平均值
        /// 参考百分比差异
        /// </summary>
        public double HistoryPriceFluctuation
        {
            get => historyPriceFluctuation; set => SetProperty(ref historyPriceFluctuation, value);
        }
        #endregion

        #region 目的地当前价格波动
        private int nowPriceFluctuationDay = 7;
        /// <summary>
        /// 平均历史价格计算天数
        /// </summary>
        public int NowPriceFluctuationDay
        {
            get => nowPriceFluctuationDay; set => SetProperty(ref nowPriceFluctuationDay, value);
        }
        private double nowPriceFluctuation = 7;
        /// <summary>
        /// 当前价格波动值，越小越好
        /// |(卖出价格 - 历史平均价格)|/历史平均价格
        /// </summary>
        public double NowPriceFluctuation
        {
            get => nowPriceFluctuation; set => SetProperty(ref nowPriceFluctuation, value);
        }
        #endregion

        #region 饱和度
        private double saturationFluctuation = 20;
        /// <summary>
        /// 有效订单价格相对于卖出价格的波动范围
        /// </summary>
        public double SaturationFluctuation
        {
            get => saturationFluctuation; set => SetProperty(ref saturationFluctuation, value);
        }
        #endregion

        #region 推荐度计算
        private double suggestionROI = 40;
        /// <summary>
        /// 回报率
        /// 回报率越高越好
        /// </summary>
        public double SuggestionROI
        {
            get => suggestionROI; set => SetProperty(ref suggestionROI, value);
        }

        private double suggestionNetProfit = 30;
        /// <summary>
        /// 净利润
        /// 净利润越高越好
        /// </summary>
        public double SuggestionNetProfit
        {
            get => suggestionNetProfit; set => SetProperty(ref suggestionNetProfit, value);
        }

        private double suggestionPrincipal = 10;
        /// <summary>
        /// 投入ISK本金
        /// 本金越少越好
        /// </summary>
        public double SuggestionPrincipal
        {
            get => suggestionPrincipal; set => SetProperty(ref suggestionPrincipal, value);
        }

        private double suggestionSales = 5;
        /// <summary>
        /// 销量
        /// 销量一定程度上越多越好
        /// 销量多意味着卖得更快，但往往竞争也激烈
        /// </summary>
        public double SuggestionSales
        {
            get => suggestionSales; set => SetProperty(ref suggestionSales, value);
        }

        private double suggestionHistoryPriceFluctuation = 5;
        /// <summary>
        /// 历史价格波动
        /// 波动越小越好
        /// </summary>
        public double SuggestionHistoryPriceFluctuation
        {
            get => suggestionHistoryPriceFluctuation; set => SetProperty(ref suggestionHistoryPriceFluctuation, value);
        }

        private double suggestionNowPriceFluctuation = 5;
        /// <summary>
        /// 当前价格波动
        /// 波动越小越好
        /// </summary>
        public double SuggestionNowPriceFluctuation
        {
            get => suggestionNowPriceFluctuation; set => SetProperty(ref suggestionNowPriceFluctuation, value);
        }

        private double suggestionSaturation = 5;
        /// <summary>
        /// 订单饱和度
        /// 饱和度越小越好
        /// </summary>
        public double SuggestionSaturation
        {
            get => suggestionSaturation; set => SetProperty(ref suggestionSaturation, value);
        }
        #endregion

        public enum PriceType
        {
            [Description("卖单前5%最低价格订单平均价格")]
            SellTop5,
            [Description("卖单实际数量的相应价格")]
            SellAvailable,
            [Description("卖单最低价格")]
            SellTop,

            [Description("买单前5%最高价格订单平均价格")]
            BuyTop5,
            [Description("买单实际数量的相应价格")]
            BuyAvailable,
            [Description("买单最高价格")]
            BuyTop,

            [Description("历史最高价格")]
            HistoryHighest,
            [Description("历史平均价格")]
            HistoryAverage,
            [Description("历史最低价格")]
            HistoryLowest,
        }
    }
}
