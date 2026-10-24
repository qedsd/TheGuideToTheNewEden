using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TheGuideToTheNewEden.WPF.ViewModels.Industry;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>工业 → 蓝图浏览器：树形浏览可制造物品，查看材料 / 产物 / 技能。</summary>
public partial class IndustryBlueprintPage : Page
{
    private readonly IndustryBlueprintPageViewModel _viewModel = new();

    public IndustryBlueprintPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnPageLoaded;
        await _viewModel.LoadAllProductsAsync();
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

    private void OnProductTreeSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is IndustryProductTreeItem item)
        {
            _viewModel.SelectProduct(item);
        }
    }
}
