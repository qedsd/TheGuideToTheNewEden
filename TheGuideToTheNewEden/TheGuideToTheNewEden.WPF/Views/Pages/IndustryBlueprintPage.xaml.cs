using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TheGuideToTheNewEden.WPF.ViewModels.Industry;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>工业 → 蓝图浏览器：按产品名搜索，查看材料 / 产物 / 技能。</summary>
public partial class IndustryBlueprintPage : Page
{
    private readonly IndustryBlueprintPageViewModel _viewModel = new();

    public IndustryBlueprintPage()
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
}
