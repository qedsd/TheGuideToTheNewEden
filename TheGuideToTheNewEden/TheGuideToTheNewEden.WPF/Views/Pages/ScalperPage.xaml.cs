using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.ViewModels.Business;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 倒货壳页：倒货分析 / 购物车 / 购物记录三个子页签。
/// 三个子视图共用同一份购物车数据（<c>BusinessService.ShoppingCart</c>），所以数据流不依赖页签切换。
/// </summary>
public partial class ScalperPage : Page, IManualAware
{
    /// <summary>子页签顺序 → 手册 id（见 <c>ScalperPage.xaml</c> 的三个 TabItem）。</summary>
    private static readonly string[] SubManualIds = ["scalper", "scalper/cart", "scalper/records"];

    private readonly ScalperPageViewModel _analyseViewModel = new();
    private readonly ScalperShoppingCartViewModel _cartViewModel = new();
    private readonly ScalperShoppingRecordViewModel _recordViewModel = new();

    /// <summary>F1：按当前页签给出倒货 / 购物车 / 购物记录三篇之一。</summary>
    public string? ManualId
    {
        get
        {
            var index = Tabs.SelectedIndex;
            return index >= 0 && index < SubManualIds.Length ? SubManualIds[index] : "scalper";
        }
    }

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
