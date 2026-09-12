using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.ViewModels.Business;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>倒货分析子视图：左侧设置/右侧结果。详情窗口在此弹出（VM 不接触窗口）。</summary>
public partial class ScalperAnalyseView : UserControl
{
    private ScalperItemDetailWindow? _detailWindow;

    public ScalperAnalyseView()
    {
        InitializeComponent();
    }

    private ScalperPageViewModel? Vm => DataContext as ScalperPageViewModel;

    private async void OnGetOrdersClick(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm)
        {
            await vm.GetOrdersAsync();
        }
    }

    private async void OnAnalyseClick(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm)
        {
            await vm.AnalyseAsync();
        }
    }

    private void OnRemoveFilterTypesClick(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm)
        {
            vm.RemoveFilterTypes(FilterTypeList.SelectedItems.Cast<InvType>().ToList());
        }
    }

    private void OnAddToCartClick(object sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm)
        {
            return;
        }

        var items = ResultGrid.SelectedItems.Cast<ScalperItem>().ToList();
        if (items.Count == 0)
        {
            return;
        }

        vm.AddToCart(items);
    }

    private void OnResultGridDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ResultGrid.SelectedItem is ScalperItem item)
        {
            OpenDetail(item);
        }
    }

    private void OpenDetail(ScalperItem item)
    {
        if (_detailWindow is null)
        {
            _detailWindow = new ScalperItemDetailWindow();
            _detailWindow.Closed += (_, _) => _detailWindow = null;
        }

        _detailWindow.SetItem(item);
        _detailWindow.Activate();
    }
}
