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
        SystemPicker.SelectedItemChanged += OnSystemPicked;
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
            PageNotifyService.Error(LanguageService.Format("IndustryPlanetary_ImportFailed", ex.Message));
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
            PageNotifyService.Error(LanguageService.Format("IndustryPlanetary_PriceRefreshFailed", ex.Message));
        }
    }

    private void OnCalculateClick(object sender, RoutedEventArgs e) => _ = _viewModel.CalculateAsync();

    private void OnCancelClick(object sender, RoutedEventArgs e) => _viewModel.Cancel();

    private void OnRefreshLiveClick(object sender, RoutedEventArgs e) => _ = _viewModel.RefreshLiveAsync(true);

    private void OnCancelLiveClick(object sender, RoutedEventArgs e) => _viewModel.CancelLive();

    private void OnToggleProductNodeClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is PlanetaryProductNodeViewModel node)
        {
            _viewModel.ToggleProductNode(node);
        }
    }

    private void OnExpandAllNodesClick(object sender, RoutedEventArgs e) => _viewModel.ExpandAllProductNodes();

    private void OnCollapseAllNodesClick(object sender, RoutedEventArgs e) => _viewModel.CollapseAllProductNodes();

    private void OnClearPlanetsClick(object sender, RoutedEventArgs e) => _viewModel.ClearSelectedPlanets();

    private void OnRemovePlanetClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is Services.Industry.PlanetaryPlanetOption planet)
        {
            _viewModel.RemovePlanet(planet);
        }
    }

    private void OnSystemPicked(Core.DBModels.MapSolarSystem? system)
    {
        if (system is null)
        {
            return;
        }

        _viewModel.AddSystemPlanets(new Services.Industry.PlanetarySystemOption
        {
            SolarSystemId = system.SolarSystemID,
            Name = system.SolarSystemName,
        });
    }

    private void OnSavePlanHeaderClick(object sender, RoutedEventArgs e) => _viewModel.SaveCurrentPlan();

    private void OnSavedPlanDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedSavedPlan is not { } plan)
        {
            return;
        }

        var dialog = new Views.Windows.EveSettingsTextInputWindow(
            LanguageService.Get("IndustryPlanetary_RenameTitle"),
            LanguageService.Get("IndustryPlanetary_RenamePrompt"),
            plan.Name,
            LanguageService.Get("IndustryPlanetary_RenamePlaceholder"),
            value => string.IsNullOrWhiteSpace(value)
                ? LanguageService.Get("IndustryPlanetary_RenameEmpty")
                : null)
        {
            Owner = Window.GetWindow(this),
        };
        if (dialog.ShowDialog() == true)
        {
            _viewModel.RenameSelectedPlan(dialog.Value);
        }
    }

    private void OnLoadPlanClick(object sender, RoutedEventArgs e) => _viewModel.LoadSelectedPlan();

    private void OnDeletePlanClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedSavedPlan is null) return;
        if (MessageBox.Show(
                LanguageService.Format("IndustryPlanetary_DeleteConfirm", _viewModel.SelectedSavedPlan.Name),
                LanguageService.Get("IndustryPlanetary_DeleteTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }
        _viewModel.DeleteSelectedPlan();
    }
}
