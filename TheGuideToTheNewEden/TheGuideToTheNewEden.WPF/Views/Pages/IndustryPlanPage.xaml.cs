using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.ViewModels.Industry;

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

    private void OnRemoveRigClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is IndustryRigItem rig)
        {
            _viewModel.RemoveRig(rig);
        }
    }

    private void OnCalculateClick(object sender, RoutedEventArgs e) => _ = _viewModel.CalculateAsync();

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
        if (MessageBox.Show($"确定删除计划「{_viewModel.SelectedSavedPlan.Name}」？", "删除计划",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            _viewModel.DeleteSelectedPlan();
        }
    }
}
