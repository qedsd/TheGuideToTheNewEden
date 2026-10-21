using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using TheGuideToTheNewEden.WPF.ViewModels.Industry;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>工业 → 库存：多仓库管理、手动添加、角色资产导入、CSV 导入/导出。</summary>
public partial class IndustryInventoryPage : Page
{
    private readonly IndustryInventoryPageViewModel _viewModel = new();

    public IndustryInventoryPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += (_, _) => _viewModel.Load();
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

    private void OnAddItemClick(object sender, RoutedEventArgs e) => _viewModel.AddItem();

    private void OnRemoveItemClick(object sender, RoutedEventArgs e) => _viewModel.RemoveItem();

    private void OnSetQuantityClick(object sender, RoutedEventArgs e) => _viewModel.SetItemQuantity();

    private void OnCreateWarehouseClick(object sender, RoutedEventArgs e) => _viewModel.CreateWarehouse();

    private void OnRenameWarehouseClick(object sender, RoutedEventArgs e) => _viewModel.RenameWarehouse();

    private void OnDeleteWarehouseClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedWarehouse is null)
        {
            _viewModel.DeleteWarehouse();
            return;
        }
        if (MessageBox.Show($"确定删除仓库「{_viewModel.SelectedWarehouse.Name}」？", "删除仓库",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            _viewModel.DeleteWarehouse();
        }
    }

    private void OnMergeImportClick(object sender, RoutedEventArgs e) => _ = _viewModel.ImportFromCharactersAsync(false);

    private void OnReplaceImportClick(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("覆盖导入会清空当前仓库已有物品，确定继续？", "覆盖导入",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }
        _ = _viewModel.ImportFromCharactersAsync(true);
    }

    private void OnImportCsvClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CSV (*.csv)|*.csv|所有文件 (*.*)|*.*",
        };
        if (dialog.ShowDialog() == true)
        {
            _viewModel.ImportCsv(dialog.FileName, false);
        }
    }

    private void OnExportCsvClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"industry-inventory-{DateTime.Now:yyyyMMdd-HHmm}.csv",
        };
        if (dialog.ShowDialog() == true)
        {
            _viewModel.ExportCsv(dialog.FileName);
        }
    }
}
