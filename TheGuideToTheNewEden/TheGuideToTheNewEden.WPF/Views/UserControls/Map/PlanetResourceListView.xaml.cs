using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.ViewModels.Map;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.Map;

/// <summary>行星资源清单视图（星域 / 星系 / 设施升级三页签）。</summary>
public partial class PlanetResourceListView : UserControl
{
    public PlanetResourceListView()
    {
        InitializeComponent();
        ViewModel = new PlanetResourceListViewModel();
        DataContext = ViewModel;
        ThemeForegroundHelper.Attach(this);
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }

    public PlanetResourceListViewModel ViewModel { get; }

    private void RegionGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!IsInsideRow(RegionGrid, e.OriginalSource)
            || RegionGrid.SelectedItem is not RegionResourceRow { RegionId: > 0 } row)
        {
            return;
        }

        Navigation.NavigateToRegion(row.RegionId);
    }

    private void SystemGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!IsInsideRow(SystemGrid, e.OriginalSource)
            || SystemGrid.SelectedItem is not SystemResourceRow { SystemId: > 0 } row)
        {
            return;
        }

        Navigation.NavigateToSystem(row.SystemId);
    }

    private static bool IsInsideRow(DataGrid grid, object source)
        => source is DependencyObject element
           && ItemsControl.ContainerFromElement(grid, element) is DataGridRow;
}
