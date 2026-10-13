using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.ViewModels.Business;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 比价页（薄壳）：只承载 <see cref="Views.UserControls.CompareMarketView"/> 并管理 VM 生命周期。
/// <para>
/// 页面是 <c>NavigationCacheMode="Required"</c> 的常驻页：<see cref="CompareMarketPageViewModel.Init"/> 自带
/// "只初始化一次"守卫，所以切走再切回不会丢失已算出的结果。
/// </para>
/// <para>
/// **<c>Unloaded</c> 里刻意什么都不做，更不取消取数**（§9 第 30 条）：页面切走只是离开可视树、不是关闭，
/// 在这里 <c>Cancel()</c> 会让"切个页面"变成"比价被静默取消"。要中止只能走页内遮罩上的「取消」按钮。
/// </para>
/// </summary>
public partial class CompareMarketPage : Page
{
    private readonly CompareMarketPageViewModel _viewModel = new();

    public CompareMarketPage()
    {
        InitializeComponent();
        CompareView.DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e) => _viewModel.Init();
}
