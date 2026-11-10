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
        /// 目标销量
        /// </summary>
        public long TargetSales { get; set; }
        /// <summary>
        /// 推荐度
        /// </summary>
        public double Suggestion { get; set; }
        /// <summary>
        /// 回报率
        /// </summary>
        public double ROI { get; set; }
        /// <summary>
        /// 单位净利润
        /// </summary>
        public double NetProfit { get; set; }
        /// <summary>
        /// 目标销量净利润
        /// </summary>
        public double TargetNetProfit { get; set; }
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
        /// <summary>
        /// 热力值
        /// </summary>
        public int HeatValue { get; set; }
        /// <summary>加热度占比（0~100%）：按阈值模式判定“热天”，强度加权后归一。</summary>
        public double HeatRatio { get; set; }

        #region 阶段 299：趋势 / 质量置信 / 可解释分

        /// <summary>目的市场历史均价的线性回归斜率（每日 %，带符号；正=涨势）。</summary>
        public double PriceTrend { get; set; }

        /// <summary>当前卖价相对历史均价的安全垫（带符号 %；正=价高于历史均价）。</summary>
        public double PriceCushion { get; set; }

        /// <summary>目的市场日销量变异系数（std/mean，越低需求越稳定）。</summary>
        public double SalesCV { get; set; }

        /// <summary>数据置信度（0~1）：目的市场历史天数 / 统计窗口。</summary>
        public double Confidence { get; set; }

        /// <summary>可解释推荐分（0~100 绝对分，跨批可比）。</summary>
        public double Score { get; set; }

        /// <summary>分数构成摘要（悬浮提示）。</summary>
        public string ScoreDetail { get; set; } = string.Empty;

        #endregion

        /// <summary>
        /// 目标销量体积
        /// </summary>
        public double TargetVolume { get => InvType.PackagedVolume * TargetSales; }

        /// <summary>
        /// ISK/跳
        /// </summary>
        public double IskPerJump { get; set; }

        /// <summary>
        /// ISK/单位体积
        /// </summary>
        public double IskPerVolume { get; set; }

        /// <summary>
        /// 源市场与目的市场距离跳数
        /// </summary>
        public int Jumps { get; set; }

    #region 阶段 296：费用 / 成交 / 时间 / 竞争 / 风险指标

    /// <summary>按目标数量走订单簿后的买入均价（含税/费前）。</summary>
    public double BuyAvgPrice { get; set; }

    /// <summary>按目标数量走订单簿后的卖出均价（含税/费前）。</summary>
    public double SellAvgPrice { get; set; }

    /// <summary>成交覆盖率：订单簿能支撑的目标量占比（0~1；历史口径按 1 计）。</summary>
    public double FillRatio { get; set; }

    /// <summary>竞争份额：目标量 /（目标量 + 同价带竞争挂单量），0~1。</summary>
    public double CompetitionShare { get; set; }

    /// <summary>按日销量与竞争份额估算的卖完天数（买入视为即时完成，不计赶路）。</summary>
    public double DaysToSell { get; set; }

    /// <summary>单位税后净利（已扣销售税与经纪费）。</summary>
    public double NetProfitAfterFees { get; set; }

    /// <summary>目标量税后净利（已扣销售税与经纪费）。</summary>
    public double TargetNetProfitAfterFees { get; set; }

    /// <summary>税后回报率（%）。</summary>
    public double ROIAfterFees { get; set; }

    /// <summary>利润/小时（税后）：按"卖完天数"折算，不计赶路。</summary>
    public double ProfitPerHour { get; set; }

    /// <summary>风险调整利润（税后利润 − 风险厌恶 × 历史波动率 × 买入成本）。</summary>
    public double RiskAdjustedProfit { get; set; }

    /// <summary>单位体积税后利润。</summary>
    public double ProfitPerVolumeAfterFees { get; set; }

    #endregion
    }
}
