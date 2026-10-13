using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TheGuideToTheNewEden.WPF.Services.Business;

namespace TheGuideToTheNewEden.WPF.ViewModels.Business;

/// <summary>
/// 编辑清单里的一件物品（左侧列表，**事实来源**）。数量可就地修改；
/// 改动只把结果标记为"已过期"，直到点「开始计算」才重新计算（用户要求的交互）。
/// </summary>
public sealed class CompareMarketItemEdit : INotifyPropertyChanged
{
    public int TypeId { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>单位体积（打包体积，缺省回落普通体积）。</summary>
    public double UnitVolume { get; init; }

    private double _amount = 1;

    public double Amount
    {
        get => _amount;
        set
        {
            if (Math.Abs(_amount - value) < 0.0001)
            {
                return;
            }

            _amount = value <= 0 ? 1 : value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Volume));
        }
    }

    public double Volume => UnitVolume * Amount;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// 结果区的一个 Tab。<see cref="MarketIndex"/> &lt; 0 表示"全部"（完整价格矩阵，数据在 <see cref="Rows"/>）；
/// 否则是"某个市场被选为最优"的物品简表（数据在 <see cref="SimpleRows"/>）。
/// </summary>
public sealed class CompareResultTab
{
    public string Title { get; init; } = string.Empty;

    public int MarketIndex { get; init; } = -1;

    public bool IsAll => MarketIndex < 0;

    /// <summary>"全部"Tab 的行（完整矩阵）。</summary>
    public List<CompareMarketRow> Rows { get; init; } = [];

    /// <summary>市场 Tab 的行（简表）。</summary>
    public List<CompareResultSimpleRow> SimpleRows { get; init; } = [];

    /// <summary>该 Tab 的小计合计（标题里显示，便于一眼看出"这个市场一共多少钱"）。</summary>
    public double TotalSubtotal { get; init; }
}

/// <summary>市场 Tab 的简表行：只保留"这件物品在这个市场"的关键数字。</summary>
public sealed class CompareResultSimpleRow
{
    public string Name { get; init; } = string.Empty;

    public double Amount { get; init; }

    public double Volume { get; init; }

    /// <summary>该市场按成交方式取到的单价（卖出为到手价、买入为成本价）。</summary>
    public double UnitPrice { get; init; }

    public double Subtotal { get; init; }

    /// <summary>较次优优势%（null 显示为 —）。</summary>
    public double? AdvantagePercent { get; init; }
}
