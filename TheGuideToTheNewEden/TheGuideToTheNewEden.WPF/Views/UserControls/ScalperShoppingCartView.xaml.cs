using TheGuideToTheNewEden.WPF.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.ViewModels.Business;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>倒货购物车子视图：统计 + 明细，复制/粘贴游戏内订单、保存记录、编辑/删除明细。</summary>
public partial class ScalperShoppingCartView : UserControl
{
    public ScalperShoppingCartView()
    {
        InitializeComponent();
    }

    private ScalperShoppingCartViewModel? Vm => DataContext as ScalperShoppingCartViewModel;

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm || !vm.HasItems)
        {
            return;
        }

        // 超过游戏单次 100 行上限：单独弹窗，让用户自己选复制哪一批（阶段 290）
        if (vm.GameOrderBatches.Count > 1)
        {
            new GameOrderBatchWindow(vm).ShowDialog();
            return;
        }

        if (!ClipboardHelper.CopyOrNotify(vm.BuildGameOrderText()))
        {
            return;
        }

        vm.NotifyCopied();
    }

    private void OnPasteClick(object sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm)
        {
            return;
        }

        var text = ClipboardHelper.TryGetText();
        vm.PasteFromGameOrderText(text);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e) => Vm?.SaveToRecord();

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm)
        {
            vm.Remove(ItemsGrid.SelectedItems.Cast<ScalperShoppingItem>().ToList());
        }
    }

    private void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (ItemsGrid.SelectedItem is ScalperShoppingItem item)
        {
            Edit(item);
        }
    }

    private void OnItemsGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsGrid.SelectedItem is ScalperShoppingItem item)
        {
            Edit(item);
        }
    }

    private void Edit(ScalperShoppingItem item)
    {
        var window = new ScalperShoppingItemEditWindow(item.InvType.TypeName)
        {
            Owner = Window.GetWindow(this),
        };
        window.SetItem(item);
        if (window.ShowDialog() == true)
        {
            Vm?.OnItemEdited();
        }
    }
}
