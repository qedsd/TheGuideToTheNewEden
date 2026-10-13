using System.ComponentModel;
using System.Runtime.CompilerServices;
using TheGuideToTheNewEden.Core.Models.Market;

namespace TheGuideToTheNewEden.WPF.Services.Business;

/// <summary>清单里一件物品的角色（卖出组 / 买入组）。两组同时参与计算，可互换。</summary>
public enum CompareItemGroup
{
    /// <summary>卖出组：对比各市场"卖在哪里到手最多"。</summary>
    Sell,

    /// <summary>买入组：对比各市场"在哪里买最便宜"。</summary>
    Buy,
}

/// <summary>
/// 成交方式：挂单（自己挂，价格更优但要等成交） / 吃单（直接吃对手价，立即成交但价格更差）。
/// 卖出侧的挂单 = 挂卖单、吃单 = 卖给现有买单；买入侧相反。
/// </summary>
public enum CompareTradeMode
{
    /// <summary>挂单（卖→挂卖单 / 买→挂买单）。</summary>
    MakeOrder,

    /// <summary>吃单（卖→卖给买单 / 买→吃卖单）。</summary>
    TakeOrder,
}

/// <summary>比价输入项（VM 从清单行构造；服务层因此不依赖 ViewModel 层）。</summary>
public sealed class CompareMarketInputItem
{
    public int TypeId { get; init; }

    public string Name { get; init; } = string.Empty;

    public double Amount { get; init; } = 1;

    public CompareItemGroup Group { get; init; }
}

/// <summary>取数进度（服务层只给结构化数据，文案由 VM 拼接）。</summary>
public sealed class CompareProgress
{
    public int MarketIndex { get; init; }

    public int MarketCount { get; init; }

    public string MarketName { get; init; } = string.Empty;

    /// <summary>当前市场已处理的物品数。</summary>
    public int Done { get; init; }

    /// <summary>当前市场需处理的物品总数。</summary>
    public int Total { get; init; }
}

/// <summary>
/// 一个市场对一件物品的行情：两个参考价 + 挂单量统计。
/// 价格为 0 表示该侧无报价（订单接口正常返回但该侧没有订单，或该物品的请求失败）。
/// </summary>
public sealed class CompareMarketCell
{
    /// <summary>卖单侧参考价（该市场现有卖单；口径见设置）。</summary>
    public double SellPrice { get; init; }

    /// <summary>买单侧参考价（该市场现有买单）。</summary>
    public double BuyPrice { get; init; }

    public int SellOrderCount { get; init; }

    public long SellVolumeRemain { get; init; }

    public int BuyOrderCount { get; init; }

    public long BuyVolumeRemain { get; init; }

    /// <summary>该市场整体取数失败（如建筑无权限），区别于"该物品在此市场没有报价"。</summary>
    public bool MarketFailed { get; init; }

    /// <summary>
    /// 该市场对"这一行所属角色"的单价：卖出组 = 按卖出方式的税后到手价、买入组 = 按买入方式的成本价。
    /// 由 <see cref="CompareMarketService"/> 在组装行时回填（矩阵里每个市场只显示这一列）。
    /// </summary>
    public double UnitPrice { get; set; }

    /// <summary>至少一侧有价。</summary>
    public bool HasQuote => SellPrice > 0 || BuyPrice > 0;
}

/// <summary>
/// 结果表的一行（= 清单里一件物品）。数量与分组可改：改动后由 VM 订阅 <see cref="PropertyChanged"/> 即时重算汇总，
/// **不重新取数**（除非口径是"按数量吃单"类，此时 VM 会提示需重新比价）。
/// </summary>
public sealed class CompareMarketRow : INotifyPropertyChanged
{
    public int TypeId { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>单位体积（打包体积，缺省回落普通体积）。</summary>
    public double UnitVolume { get; init; }

    /// <summary>各市场价格，顺序与 <see cref="CompareMarketResult.Markets"/> 一致。</summary>
    public List<CompareMarketCell> Cells { get; init; } = [];

    /// <summary>最优买入市场下标（-1 = 无）。</summary>
    public int BuyMarketIndex { get; init; } = -1;

    /// <summary>最优卖出市场下标（-1 = 无）。</summary>
    public int SellMarketIndex { get; init; } = -1;

    /// <summary>最优买入市场的单价（按买入方式取列）。</summary>
    public double BuyUnitPrice { get; init; }

    /// <summary>最优卖出市场的单价（按卖出方式取列）。</summary>
    public double SellUnitPrice { get; init; }

    /// <summary>最优买入市场名（SDE 名，不随界面语言变化；用哪一侧的价由全局"买入方式"决定，故只在列头说明）。</summary>
    public string BestBuyMarketName { get; init; } = string.Empty;

    /// <summary>最优卖出市场名。</summary>
    public string BestSellMarketName { get; init; } = string.Empty;

    /// <summary>按本行角色选出的单价：卖出组取最优卖出价、买入组取最优买入价。</summary>
    public double EffectiveUnitPrice { get; init; }

    /// <summary>较次优优势百分比（null = 不足两个市场有价，界面显示 —）。</summary>
    public double? AdvantagePercent { get; init; }

    /// <summary>按其角色无法定价（卖出组没有任何市场可卖、买入组没有任何市场可买）——这些行不计入汇总。</summary>
    public bool HasNoQuote { get; init; }

    private CompareItemGroup _group;

    public CompareItemGroup Group
    {
        get => _group;
        set => Set(ref _group, value);
    }

    private double _amount = 1;

    public double Amount
    {
        get => _amount;
        set
        {
            if (Set(ref _amount, value))
            {
                OnPropertyChanged(nameof(Subtotal));
                OnPropertyChanged(nameof(Volume));
            }
        }
    }

    /// <summary>小计 = 单价 × 数量（卖出组为预期收入、买入组为预期支出）。</summary>
    public double Subtotal => EffectiveUnitPrice * Amount;

    public double Volume => UnitVolume * Amount;

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// 集中度分析结果：回答"值不值得把货分散到多个市场"。
/// <para>
/// 口径（同集合自洽，避免把"不能在 J 卖"与"在 J 卖得便宜"混为一谈）：
/// 只用"在候选市场有报价的物品子集"比较——<see cref="BestPerItemTotal"/> 是该子集"各自选最优"的总额，
/// <see cref="SingleMarketTotal"/> 是该子集"全部集中到候选市场"的总额，<see cref="Gain"/> 是两者的差额。
/// </para>
/// </summary>
public sealed class ConcentrationResult
{
    public int MarketIndex { get; init; } = -1;

    public string MarketName { get; init; } = string.Empty;

    /// <summary>各自最优时的总额（收入越高越好 / 支出越低越好）。</summary>
    public double BestPerItemTotal { get; init; }

    /// <summary>全部集中到候选市场时的总额。</summary>
    public double SingleMarketTotal { get; init; }

    /// <summary>差额：卖出侧为"分散可多赚"、买入侧为"集中会多花"。</summary>
    public double Gain { get; init; }

    /// <summary>差额占各自最优总额的百分比。</summary>
    public double GainPercent { get; init; }

    /// <summary>候选市场覆盖到的物品数。</summary>
    public int CoveredCount { get; init; }

    /// <summary>参与分析的物品总数。</summary>
    public int TotalCount { get; init; }

    /// <summary>候选市场覆盖了全部参与分析的物品。</summary>
    public bool CoversAll => CoveredCount >= TotalCount;

    public bool HasResult => MarketIndex >= 0 && TotalCount > 0;
}

/// <summary>某个市场整体取数失败（不中断整体比价，仅在结果里标注）。</summary>
public sealed class CompareMarketFailure
{
    public int MarketIndex { get; init; }

    public string MarketName { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;
}

/// <summary>比价结果（服务层输出契约；VM 直接绑定，数量变更时由 VM 自行重算汇总）。</summary>
public sealed class CompareMarketResult
{
    public List<MarketLocation> Markets { get; init; } = [];

    public List<CompareMarketRow> Rows { get; init; } = [];

    /// <summary>整体取数失败的市场。</summary>
    public List<CompareMarketFailure> Failures { get; init; } = [];

    public double TotalSellIncome { get; init; }

    public double TotalBuyExpense { get; init; }

    public double NetAmount => TotalSellIncome - TotalBuyExpense;

    public double TotalVolume { get; init; }

    /// <summary>两侧在所有市场都无报价的物品数（必须显式提示，否则汇总会静默漏算）。</summary>
    public int NoQuoteCount { get; init; }

    public ConcentrationResult? SellConcentration { get; init; }

    public ConcentrationResult? BuyConcentration { get; init; }

    public bool HasRows => Rows.Count > 0;
}
