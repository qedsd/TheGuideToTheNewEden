using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TheGuideToTheNewEden.WPF.Services.Industry;
using TheGuideToTheNewEden.WPF.ViewModels.Industry;
using TheGuideToTheNewEden.WPF.Views.Windows;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>工业 → 制造计划：Tab 分区、左侧常驻物品树、多目标/多角色、可视化生产链。</summary>
public partial class IndustryPlanPage : Page
{
    private readonly IndustryPlanPageViewModel _viewModel = new();

    public IndustryPlanPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => _ = _viewModel.LoadAsync();

    private void OnAddProductClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is IndustryProductTreeItem product)
        {
            _viewModel.AddProduct(product);
        }
    }

    private void OnToggleNodeClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is IndustryPlanNodeViewModel node)
        {
            _viewModel.ToggleNode(node);
        }
    }

    private void OnRemoveTargetClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is IndustryTargetItem target)
        {
            _viewModel.RemoveTarget(target);
        }
    }

    private void OnClearTargetsClick(object sender, RoutedEventArgs e) => _viewModel.ClearTargets();

    private void OnAddRigClick(object sender, RoutedEventArgs e) => _viewModel.AddRig();

    private void OnSaveBuildingProfileClick(object sender, RoutedEventArgs e) => _viewModel.SaveBuildingProfile();

    private void OnNewBuildingProfileClick(object sender, RoutedEventArgs e) => _viewModel.NewBuildingProfile();

    private void OnBuildingProfileDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender as ListBox)?.SelectedItem is IndustryBuildingProfileItem item)
        {
            item.IsEditing = true;
        }
    }

    private void OnBuildingProfileEditBoxVisible(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox box && box.IsVisible)
        {
            box.Focus();
            box.SelectAll();
        }
    }

    private void OnBuildingProfileNameLostFocus(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is IndustryBuildingProfileItem item)
        {
            item.IsEditing = false;
        }
    }

    private void OnBuildingProfileNameKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Escape
            && (sender as FrameworkElement)?.DataContext is IndustryBuildingProfileItem item)
        {
            item.IsEditing = false;
            e.Handled = true;
        }
    }

    private void OnApplyBuildingProfileClick(object sender, RoutedEventArgs e) => _viewModel.ApplyBuildingProfile();

    private void OnDeleteBuildingProfileClick(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                LanguageService.Get("IndustryPage_DeleteProfileConfirm"),
                LanguageService.Get("IndustryPage_ConfirmDeleteTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }
        _viewModel.DeleteBuildingProfile();
    }

    private void OnViewBomCostClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is IndustryPlanNodeViewModel node)
        {
            _viewModel.FocusBomCost(node.TypeId);
        }
    }

    private void OnAddCustomCharacterClick(object sender, RoutedEventArgs e)
    {
        var id = _viewModel.NextCustomCharacterId();
        var character = IndustryCustomCharacter.CreateDefault(id);
        var window = new IndustryCustomCharacterWindow(character) { Owner = Window.GetWindow(this) };
        if (window.ShowDialog() == true)
        {
            _viewModel.AddCustomCharacter(window.BuildModel(id));
        }
    }

    private void OnEditCustomCharacterClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is IndustryCustomCharacterItem item)
        {
            var window = new IndustryCustomCharacterWindow(item.Model.Clone()) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() == true)
            {
                _viewModel.UpdateCustomCharacter(window.BuildModel(item.Id));
            }
        }
    }

    private void OnDeleteCustomCharacterClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is IndustryCustomCharacterItem item)
        {
            if (MessageBox.Show(
                    LanguageService.Format("IndustryCustomCharacter_DeleteConfirm", item.Name),
                    LanguageService.Get("IndustryCustomCharacter_DeleteTitle"),
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _viewModel.DeleteCustomCharacter(item);
            }
        }
    }

    private void OnRemoveRigClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is IndustryRigItem rig)
        {
            _viewModel.RemoveRig(rig);
        }
    }

    private void OnCalculateClick(object sender, RoutedEventArgs e) => _ = _viewModel.CalculateAsync();

    private void OnCancelClick(object sender, RoutedEventArgs e) => _viewModel.CancelCurrentOperation();

    private void OnCopyClick(object sender, RoutedEventArgs e) => _viewModel.CopyPurchaseList();

    private void OnExportClick(object sender, RoutedEventArgs e) => _viewModel.ExportCsv();

    private void OnCopySelectedTargetClick(object sender, RoutedEventArgs e)
        => _viewModel.CopyTargetPurchaseList(_viewModel.SelectedOverviewTarget);

    private void OnExportSelectedTargetClick(object sender, RoutedEventArgs e)
        => _viewModel.ExportTargetCsv(_viewModel.SelectedOverviewTarget);

    private void OnRefreshMarketClick(object sender, RoutedEventArgs e) => _ = _viewModel.RefreshMarketAsync();

    private void OnRefreshCharacterClick(object sender, RoutedEventArgs e) => _ = _viewModel.RefreshCharacterAsync();

    private void OnForceBuildClick(object sender, RoutedEventArgs e) => _viewModel.ForceBuildSelected();

    private void OnForceBuyClick(object sender, RoutedEventArgs e) => _viewModel.ForceBuySelected();

    private void OnBlacklistClick(object sender, RoutedEventArgs e) => _viewModel.BlacklistSelected();

    private void OnClearDecisionsClick(object sender, RoutedEventArgs e) => _viewModel.ClearManualDecisions();

    private void OnSavePlanClick(object sender, RoutedEventArgs e) => _viewModel.SaveCurrentPlan();

    private void OnOverwritePlanClick(object sender, RoutedEventArgs e) => _viewModel.OverwriteSelectedPlan();

    private void OnLoadPlanClick(object sender, RoutedEventArgs e) => _viewModel.LoadSelectedPlan();

    private void OnRenamePlanClick(object sender, RoutedEventArgs e) => _viewModel.RenameSelectedPlan();

    private void OnDeletePlanClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedSavedPlan is null)
        {
            _viewModel.DeleteSelectedPlan();
            return;
        }
        if (MessageBox.Show(
                LanguageService.Format("IndustryPage_DeletePlanConfirm", _viewModel.SelectedSavedPlan.Name),
                LanguageService.Get("IndustryPage_DeletePlanTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            _viewModel.DeleteSelectedPlan();
        }
    }
}
