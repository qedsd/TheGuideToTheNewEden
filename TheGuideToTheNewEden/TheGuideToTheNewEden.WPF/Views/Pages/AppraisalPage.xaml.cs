using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.ViewModels.Business;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 物品估价页：左侧粘贴物品清单（合同/货柜/资产等）与估价设置，右侧展示估价结果。
/// 数据直接来自 ESI 公开市场接口（与 WinUI 版的第三方 API 方案不同），
/// 价格来源支持 星域 / 星系 / 建筑（统一走 <c>MarketLocationSelectorView</c>）。
/// </summary>
public partial class AppraisalPage : Page
{
    private readonly AppraisalPageViewModel _viewModel = new();

    public AppraisalPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _viewModel.Init();
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _viewModel.Dispose();
    }

    private async void OnEstimateClick(object sender, System.Windows.RoutedEventArgs e)
    {
        await _viewModel.EstimateAsync();
    }

    private void OnCopyClick(object sender, System.Windows.RoutedEventArgs e)
    {
        _viewModel.CopyResult();
    }
}
