using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.ViewModels.Business;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>购物记录子视图：记录文件列表 + 选中记录明细，可删除记录或把明细加回购物车。</summary>
public partial class ScalperShoppingRecordView : UserControl
{
    public ScalperShoppingRecordView()
    {
        InitializeComponent();
    }

    private ScalperShoppingRecordViewModel? Vm => DataContext as ScalperShoppingRecordViewModel;

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm)
        {
            return;
        }

        var files = RecordList.SelectedItems.Cast<string>().ToList();
        if (files.Count == 0)
        {
            return;
        }

        var confirm = $"{FindString("BusinessPage_ShoppingRecord_DeleteCount")}";
        var result = MessageBox.Show(
            string.Format(confirm, files.Count),
            FindString("General_RemoveSelected"),
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        if (result == MessageBoxResult.OK)
        {
            vm.RemoveFiles(files);
        }
    }

    private void OnAddToCartClick(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm)
        {
            vm.AddToCart(RecordItemsGrid.SelectedItems.Cast<ScalperShoppingItem>().ToList());
        }
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
