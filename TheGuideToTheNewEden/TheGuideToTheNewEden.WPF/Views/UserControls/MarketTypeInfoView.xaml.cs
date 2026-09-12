using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.ViewModels.Business;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>市场物品简介内容（宿主为 <see cref="Windows.ToolWindow"/>，后续在此扩展 SDE 更详细属性）。</summary>
public partial class MarketTypeInfoView : UserControl
{
    public MarketTypeInfoView(MarketPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
