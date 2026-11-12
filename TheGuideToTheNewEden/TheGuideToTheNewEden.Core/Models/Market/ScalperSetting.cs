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

        private int[] marketGroups;
        /// <summary>
        /// 物品类型
        /// </summary>
        public int[] MarketGroups
        {
            get => marketGroups; set => SetProperty(ref marketGroups, value);
        }

        public int[] MarketTypes { get; set; }
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
                IsHistoryBuyPrice = (value == PriceType.HistoryLowest || value == PriceType.HistoryHighest || value == PriceType.HistoryAverage || value == PriceType.HistoryMedian);
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

        private bool buyPirceRemoveExtremum = true;
        /// <summary>
        /// 买价去除极值
        /// </summary>
        public bool BuyPirceRemoveExtremum
        {
            get => buyPirceRemoveExtremum; set => SetProperty(ref buyPirceRemoveExtremum, value);
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
                IsHistorySellPrice = (value == PriceType.HistoryLowest || value == PriceType.HistoryHighest || value == PriceType.HistoryAverage || value == PriceType.HistoryMedian);
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

        private bool sellPirceRemoveExtremum = true;
        /// <summary>
        /// 卖价去除极值
        /// </summary>
        public bool SellPirceRemoveExtremum
        {
            get => sellPirceRemoveExtremum; set => SetProperty(ref sellPirceRemoveExtremum, value);
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

        private SalesType sourceSalesType = SalesType.HistoryLowest;
        /// <summary>
        /// 原市场销量计算方式
        /// </summary>
        public SalesType SourceSalesType
        {
            get => sourceSalesType; set => SetProperty(ref sourceSalesType, value);
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

        private SalesType destinationSalesType = SalesType.HistoryLowest;
        /// <summary>
        /// 目的市场销量计算方式
        /// </summary>
        public SalesType DestinationSalesType
        {
            get => destinationSalesType; set => SetProperty(ref destinationSalesType, value);
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
        private double saturationFluctuation = 0.2;
        /// <summary>
        /// 有效订单价格相对于卖出价格的波动范围
        /// </summary>
        public double SaturationFluctuation
        {
            get => saturationFluctuation; set => SetProperty(ref saturationFluctuation, value);
        }
        #endregion

        #region 拟卖出销量
        private double salesPercent = 20;
        /// <summary>
        /// 拟卖出销量百分比
        /// </summary>
        public double SalesPercent
        {
            get => salesPercent; set => SetProperty(ref salesPercent, value);
        }
        #endregion

        #region 热力值
        private int heatValueDay = 30;
        /// <summary>
        /// 热力值计算天数
        /// </summary>
        public int HeatValueDay
        {
            get => heatValueDay; set => SetProperty(ref heatValueDay, value);
        }
        private int heatValueThreshold = 1;
        /// <summary>
        /// 热力值销量判断界限
        /// </summary>
        public int HeatValueThreshold
        {
            get => heatValueThreshold; set => SetProperty(ref heatValueThreshold, value);
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

        private double suggestionHeatValue = 30;
        /// <summary>
        /// 热力值
        /// </summary>
        public double SuggestionHeatValue
        {
            get => suggestionHeatValue; set => SetProperty(ref suggestionHeatValue, value);
        }

        private double suggestionIskPerVolume = 10;
        /// <summary>
        /// ISK/单位体积
        /// </summary>
        public double SuggestionIskPerVolume
        {
            get => suggestionIskPerVolume; set => SetProperty(ref suggestionIskPerVolume, value);
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
            [Description("历史中位数价格")]
            HistoryMedian,
        }

        public enum SalesType
        {
            [Description("历史最低")]
            HistoryLowest,
            [Description("历史最高")]
            HistoryHighest,
            [Description("历史平均")]
            HistoryAverage,
        }

        /// <summary>热力值阈值的判定方式。</summary>
        public enum HeatThresholdKind
        {
            /// <summary>绝对值：日销量 ≥ 阈值（兼容旧版）。</summary>
            [Description("绝对销量")]
            Absolute,
            /// <summary>相对：日销量 ≥ 物品自身日均销量 × 倍数。</summary>
            [Description("自身日均的倍数")]
            RelativeToAverage,
        }

        #region 费用与新增推荐指标（阶段 296）

        private double salesTaxRate = 3.6;
        /// <summary>销售税（%，默认 3.6）。</summary>
        public double SalesTaxRate { get => salesTaxRate; set => SetProperty(ref salesTaxRate, value); }

        private double brokerFeeRate = 1.0;
        /// <summary>经纪人费（%，买入/卖出各按一次计，默认 1）。</summary>
        public double BrokerFeeRate { get => brokerFeeRate; set => SetProperty(ref brokerFeeRate, value); }

        private double competitionBandPercent = 5.0;
        /// <summary>竞争统计价格带（%）：统计"与你的卖价相差不超过该比例"的竞争挂单量。</summary>
        public double CompetitionBandPercent { get => competitionBandPercent; set => SetProperty(ref competitionBandPercent, value); }

        private double riskAversion = 1.0;
        /// <summary>风险厌恶系数：风险调整利润 = 税后利润 − 系数 × 历史波动率 × 买入成本。</summary>
        public double RiskAversion { get => riskAversion; set => SetProperty(ref riskAversion, value); }

        private HeatThresholdKind heatThresholdMode = HeatThresholdKind.RelativeToAverage;
        /// <summary>热力值阈值模式：绝对值（兼容旧版）/ 相对物品自身日均销量的倍数。</summary>
        public HeatThresholdKind HeatThresholdMode { get => heatThresholdMode; set => SetProperty(ref heatThresholdMode, value); }

        private double staleOrderDays = 2;
        /// <summary>订单新鲜度过滤：剩余时间不足该天数（天）的挂单不参与定价与深度统计（0 = 不过滤）。</summary>
        public double StaleOrderDays { get => staleOrderDays; set => SetProperty(ref staleOrderDays, value); }

        private double maxSellDays = 30;
        /// <summary>挂卖单策略的可接受卖完上限（天）：超过时按该天数截断可成交量。</summary>
        public double MaxSellDays { get => maxSellDays; set => SetProperty(ref maxSellDays, value); }

        private double suggestionPriceTrend = 15;
        public double SuggestionPriceTrend { get => suggestionPriceTrend; set => SetProperty(ref suggestionPriceTrend, value); }

        private double suggestionPriceCushion = 10;
        public double SuggestionPriceCushion { get => suggestionPriceCushion; set => SetProperty(ref suggestionPriceCushion, value); }

        private double suggestionSalesStability = 15;
        public double SuggestionSalesStability { get => suggestionSalesStability; set => SetProperty(ref suggestionSalesStability, value); }

        private double suggestionConfidence = 15;
        public double SuggestionConfidence { get => suggestionConfidence; set => SetProperty(ref suggestionConfidence, value); }

        private double heatRelativeFactor = 0.5;
        /// <summary>相对模式下的系数：销量 ≥ 自身日均 × 该倍数 记为“热天”（默认 0.5）。</summary>
        public double HeatRelativeFactor { get => heatRelativeFactor; set => SetProperty(ref heatRelativeFactor, value); }

        // ---- 新增推荐度权重（与旧权重一样，可玩家自行调整；默认先给一组相对合理的比例）----

        private double suggestionNetProfitAfterFees = 30;
        public double SuggestionNetProfitAfterFees { get => suggestionNetProfitAfterFees; set => SetProperty(ref suggestionNetProfitAfterFees, value); }

        private double suggestionROIAfterFees = 30;
        public double SuggestionROIAfterFees { get => suggestionROIAfterFees; set => SetProperty(ref suggestionROIAfterFees, value); }

        private double suggestionFillRatio = 15;
        public double SuggestionFillRatio { get => suggestionFillRatio; set => SetProperty(ref suggestionFillRatio, value); }

        private double suggestionCompetitionShare = 15;
        public double SuggestionCompetitionShare { get => suggestionCompetitionShare; set => SetProperty(ref suggestionCompetitionShare, value); }

        private double suggestionDaysToSell = 20;
        public double SuggestionDaysToSell { get => suggestionDaysToSell; set => SetProperty(ref suggestionDaysToSell, value); }

        private double suggestionProfitPerHour = 25;
        public double SuggestionProfitPerHour { get => suggestionProfitPerHour; set => SetProperty(ref suggestionProfitPerHour, value); }

        private double suggestionRiskAdjustedProfit = 10;
        public double SuggestionRiskAdjustedProfit { get => suggestionRiskAdjustedProfit; set => SetProperty(ref suggestionRiskAdjustedProfit, value); }

        private double suggestionProfitPerVolumeAfterFees = 10;
        public double SuggestionProfitPerVolumeAfterFees { get => suggestionProfitPerVolumeAfterFees; set => SetProperty(ref suggestionProfitPerVolumeAfterFees, value); }

        #endregion
    }
}
