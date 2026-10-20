using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TheGuideToTheNewEden.WPF.ViewModels.Industry;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>工业 → 制造计划：搜索目标、计算 BOM、展示结果。</summary>
public partial class IndustryPlanPage : Page
{
    private readonly IndustryPlanPageViewModel _viewModel = new();

    public IndustryPlanPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _ = _viewModel.SearchAsync();
        }
    }

    private void OnSearchClick(object sender, RoutedEventArgs e) => _ = _viewModel.SearchAsync();

    private void OnCalculateClick(object sender, RoutedEventArgs e) => _ = _viewModel.CalculateAsync();

    private void OnCopyClick(object sender, RoutedEventArgs e) => _viewModel.CopyPurchaseList();
}
