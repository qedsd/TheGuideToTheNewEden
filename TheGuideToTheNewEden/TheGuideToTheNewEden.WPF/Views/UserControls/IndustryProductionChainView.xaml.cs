using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.ViewModels.Industry;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>BOM / 成本视图：扁平节点树 + 节点详情；作为生产详情的内层页签复用。</summary>
public partial class IndustryProductionChainView : UserControl
{
    public IndustryProductionChainView()
    {
        InitializeComponent();
    }

    private IndustryPlanPageViewModel? ViewModel => DataContext as IndustryPlanPageViewModel;

    private void OnToggleNodeClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is IndustryPlanNodeViewModel node)
        {
            ViewModel?.ToggleNode(node);
        }
    }

    private void OnCopyClick(object sender, RoutedEventArgs e) => ViewModel?.CopyPurchaseList();

    private void OnExportClick(object sender, RoutedEventArgs e) => ViewModel?.ExportCsv();

    private void OnForceBuildClick(object sender, RoutedEventArgs e) => ViewModel?.ForceBuildSelected();

    private void OnForceBuyClick(object sender, RoutedEventArgs e) => ViewModel?.ForceBuySelected();

    private void OnBlacklistClick(object sender, RoutedEventArgs e) => ViewModel?.BlacklistSelected();
}
