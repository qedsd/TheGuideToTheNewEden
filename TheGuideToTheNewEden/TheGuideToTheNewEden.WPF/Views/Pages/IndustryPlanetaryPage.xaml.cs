using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.ViewModels.Industry;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>工业 → 行星开发：我的殖民地、需求反推与殖民地产能方案。</summary>
public partial class IndustryPlanetaryPage : Page
{
    private readonly PlanetaryPlanPageViewModel _viewModel = new();
    private bool _loaded;

    public IndustryPlanetaryPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync();
        _loaded = true;
        if (_viewModel.SectionTabIndex == 0)
        {
            await _viewModel.RefreshLiveAsync(false);
        }
    }

    private void OnSectionTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loaded
            && sender is TabControl tabs
            && tabs.SelectedIndex == 0
            && _viewModel.LivePlanets.Count == 0
            && !_viewModel.IsLiveBusy)
        {
            _ = _viewModel.RefreshLiveAsync(false);
        }
    }

    private void OnAddProductClick(object sender, RoutedEventArgs e) => _viewModel.AddSelectedProduct();

    private void OnRemoveTargetClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is IndustryTargetItem target)
        {
            _viewModel.RemoveTarget(target);
        }
    }

    private void OnClearTargetsClick(object sender, RoutedEventArgs e) => _viewModel.ClearTargets();

    private void OnImportBlueprintClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _viewModel.ImportBlueprint();
        }
        catch (Exception ex)
        {
            PageNotifyService.Error("导入蓝图失败：" + ex.Message);
        }
    }

    private async void OnRefreshPricesClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.RefreshPricesAsync(true);
        }
        catch (Exception ex)
        {
            PageNotifyService.Error("市场价格刷新失败：" + ex.Message);
        }
    }

    private void OnCalculateClick(object sender, RoutedEventArgs e) => _ = _viewModel.CalculateAsync();

    private void OnCancelClick(object sender, RoutedEventArgs e) => _viewModel.Cancel();

    private void OnRefreshLiveClick(object sender, RoutedEventArgs e) => _ = _viewModel.RefreshLiveAsync(true);

    private void OnCancelLiveClick(object sender, RoutedEventArgs e) => _viewModel.CancelLive();

    private void OnSavePlanClick(object sender, RoutedEventArgs e) => _viewModel.SaveCurrentPlan();

    private void OnOverwritePlanClick(object sender, RoutedEventArgs e) => _viewModel.OverwriteSelectedPlan();

    private void OnLoadPlanClick(object sender, RoutedEventArgs e) => _viewModel.LoadSelectedPlan();

    private void OnDeletePlanClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedSavedPlan is null) return;
        if (MessageBox.Show($"删除行星计划「{_viewModel.SelectedSavedPlan.Name}」？", "删除行星计划",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }
        _viewModel.DeleteSelectedPlan();
    }
}
