using Microsoft.UI.Xaml.Controls;
using Syncfusion.UI.Xaml.Grids;
using TheGuideToTheNewEden.WinUI.ViewModels;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class PlanetaryOverviewPage : Page
    {
        public PlanetaryOverviewPage()
        {
            this.InitializeComponent();
            Loaded += async (s, e) => await VM.LoadDataAsync();
        }

        private void OnProductionSelectionChanged(object sender, GridSelectionChangedEventArgs e)
        {
            if (sender is Syncfusion.UI.Xaml.DataGrid.SfDataGrid grid
                && grid.SelectedItem is ProductionRow row)
            {
                VM.SelectedProductionRow = row;
            }
        }
    }
}
