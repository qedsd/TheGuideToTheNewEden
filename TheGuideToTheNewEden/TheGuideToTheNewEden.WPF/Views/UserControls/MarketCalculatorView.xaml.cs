using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.ViewModels.Business;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>市场买入/卖出计算内容（宿主为 <see cref="Windows.ToolWindow"/>），含"计算明细"逐档吃单。</summary>
public partial class MarketCalculatorView : UserControl
{
    public MarketCalculatorView(MarketPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
