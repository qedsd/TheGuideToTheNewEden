using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.ViewModels.Industry;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

public partial class IndustryRecyclePage : Page
{
    private readonly IndustryRecyclePageViewModel _viewModel = new();

    public IndustryRecyclePage()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void OnSearchClick(object sender, System.Windows.RoutedEventArgs e) => _viewModel.Search();
    private void OnAddToBatchClick(object sender, System.Windows.RoutedEventArgs e) => _viewModel.AddSelected();
    private void OnImportClick(object sender, System.Windows.RoutedEventArgs e) => _viewModel.ImportPaste();
    private void OnClearBatchClick(object sender, System.Windows.RoutedEventArgs e) => _viewModel.ClearBatch();
    private void OnReprocessClick(object sender, System.Windows.RoutedEventArgs e) => _viewModel.Reprocess();
    private void OnCompressClick(object sender, System.Windows.RoutedEventArgs e) => _viewModel.Compress();
}
