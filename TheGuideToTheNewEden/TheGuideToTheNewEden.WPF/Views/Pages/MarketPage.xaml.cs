using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.Core.Models.Universe;
using TheGuideToTheNewEden.WPF.ViewModels.Business;
using TheGuideToTheNewEden.WPF.Views.UserControls;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>市场页：左侧选市场与物品，右侧看订单与历史图表。</summary>
public partial class MarketPage : Page
{
    private readonly MarketPageViewModel _viewModel;

    private ToolWindow? _typeInfoWindow;
    private ToolWindow? _calculatorWindow;

    public MarketPage()
    {
        InitializeComponent();

        _viewModel = new MarketPageViewModel();
        DataContext = _viewModel;

        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }


    private void OnTypeTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is MarketItem { IsType: true } item)
        {
            _viewModel.SelectedInvTypeItem = item;
        }
    }

    private void OnSearchResultSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: MarketItem item })
        {
            _viewModel.SelectedInvTypeItem = item;
        }
    }

    private void OnStaredSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: MarketItem item })
        {
            _viewModel.SelectedInvTypeItem = item;
        }
    }

    private void OnStarClick(object sender, RoutedEventArgs e) => _viewModel.ToggleStar();

    /// <summary>物品简介：工具窗口 + 简介 UserControl（单实例，重复点击仅激活）。</summary>
    private void OnDescriptionClick(object sender, RoutedEventArgs e)
    {
        if (_typeInfoWindow is null)
        {
            _typeInfoWindow = new ToolWindow(
                new MarketTypeInfoView(_viewModel),
                ToolWindowTitleStyle.Default,
                showTopmostButton: false,
                showInTaskbar: true,
                width: 440,
                height: 520)
            {
                Owner = Window.GetWindow(this),
                DisplayTitle = FindString("MarketPage_Description"),
            };
            _typeInfoWindow.Closed += (_, _) => _typeInfoWindow = null;
        }

        _typeInfoWindow.Show();
        _typeInfoWindow.Activate();
    }

    /// <summary>买入计算：工具窗口 + 计算 UserControl（单实例，重复点击仅激活；该窗口带置顶按钮）。</summary>
    private void OnCalculatorClick(object sender, RoutedEventArgs e)
    {
        if (_calculatorWindow is null)
        {
            _calculatorWindow = new ToolWindow(
                new MarketCalculatorView(_viewModel),
                ToolWindowTitleStyle.Default,
                showTopmostButton: true,
                showInTaskbar: true,
                width: 400,
                height: 620)
            {
                Owner = Window.GetWindow(this),
                DisplayTitle = FindString("MarketPage_Cal_Buy"),
            };
            _calculatorWindow.Closed += (_, _) => _calculatorWindow = null;
        }

        _calculatorWindow.Show();
        _calculatorWindow.Activate();
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await _viewModel.RefreshAsync();
}
