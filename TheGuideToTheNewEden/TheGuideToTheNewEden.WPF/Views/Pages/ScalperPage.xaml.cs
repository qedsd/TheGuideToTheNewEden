using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.ViewModels.Business;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 倒货壳页：倒货分析 / 购物车 / 购物记录三个子页签。
/// 三个子视图共用同一份购物车数据（<c>BusinessService.ShoppingCart</c>），所以数据流不依赖页签切换。
/// </summary>
public partial class ScalperPage : Page
{
    private readonly ScalperPageViewModel _analyseViewModel = new();
    private readonly ScalperShoppingCartViewModel _cartViewModel = new();
    private readonly ScalperShoppingRecordViewModel _recordViewModel = new();

    public ScalperPage()
    {
        InitializeComponent();

        AnalyseView.DataContext = _analyseViewModel;
        CartView.DataContext = _cartViewModel;
        RecordView.DataContext = _recordViewModel;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _analyseViewModel.Init();
        _cartViewModel.Init();
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _analyseViewModel.Dispose();
        _cartViewModel.Dispose();
    }
}
