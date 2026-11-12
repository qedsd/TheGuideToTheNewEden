using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.ViewModels.Business;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 「复制游戏订单（分批）」弹窗（阶段 290）：购物车超过 100 个物品时，由复制按钮打开。
/// 每一批一行，用户自己点哪批复制哪批；复制过的批次显示 √ 已复制。
/// </summary>
public partial class GameOrderBatchWindow : FluentWindow
{
    public GameOrderBatchWindow(ScalperShoppingCartViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        if (Application.Current?.MainWindow is { } mainWindow && !ReferenceEquals(mainWindow, this))
        {
            Owner = mainWindow;
        }
    }

    private void OnCopyBatchClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: GameOrderBatchItem batch })
        {
            return;
        }

        if (!ClipboardHelper.CopyOrNotify(batch.Text))
        {
            return;
        }

        batch.IsCopied = true;
        PageNotifyService.Success(LanguageService.Format("BusinessPage_GameOrderBatchCopied",
            batch.Index + 1, batch.Start, batch.End));
    }

}
