using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.ViewModels.Business;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>
/// 比价主视图：左栏（市场 / 卖出物品 / 买入物品 / 价格口径）+ 右栏（汇总 / 建议 / 警告 / 结果两层 Tab）。
/// <para>
/// "添加物品 / 粘贴导入"用**共用的弹出层**：两个清单页签各有自己的按钮，点开前先调
/// <see cref="CompareMarketPageViewModel.BeginAdd"/> 决定这次加进哪一组；弹出层不在同一可视树里，
/// 因此它的 <c>DataContext</c> 由本类显式赋给 VM（比 RelativeSource 更省事也更可靠）。
/// </para>
/// </summary>
public partial class CompareMarketView : UserControl
{
    private CompareMarketPageViewModel? _viewModel;

    public CompareMarketView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.ColumnsInvalidated -= OnColumnsInvalidated;
        }

        _viewModel = DataContext as CompareMarketPageViewModel;

        // 弹出层里的绑定**不依赖 DataContext 继承**（实测在 Popup 里拿不到 VM）：
        // XAML 里统一写成 {Binding DataContext.Xxx, ElementName=RootView}，因此这里不需要给 Popup 赋 DataContext。
        if (_viewModel is not null)
        {
            _viewModel.ColumnsInvalidated += OnColumnsInvalidated;
        }
    }

    /// <summary>结果或市场变化 → 让两个结果控件的矩阵表格重建列（列头含市场名与本地化的"卖 / 买"）。</summary>
    private void OnColumnsInvalidated()
    {
        SellResultView.ReloadColumns();
        BuyResultView.ReloadColumns();
    }

    // ==================================================================
    //  市场
    // ==================================================================

    private void OnAddMarketClick(object sender, RoutedEventArgs e) => _viewModel?.AddMarket();

    private void OnRemoveMarketClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CompareMarketSlot slot })
        {
            _viewModel?.RemoveMarket(slot);
        }
    }

    // ==================================================================
    //  添加物品 / 粘贴导入（共用弹出层）
    // ==================================================================

    private void OnSellAddClick(object sender, RoutedEventArgs e) => OpenAddPopup(sender, sell: true);

    private void OnBuyAddClick(object sender, RoutedEventArgs e) => OpenAddPopup(sender, sell: false);

    private void OnSellImportClick(object sender, RoutedEventArgs e) => OpenPastePopup(sender, sell: true);

    private void OnBuyImportClick(object sender, RoutedEventArgs e) => OpenPastePopup(sender, sell: false);

    private void OpenAddPopup(object sender, bool sell)
    {
        _viewModel?.BeginAdd(sell);
        if (sender is UIElement target)
        {
            AddItemPopup.PlacementTarget = target;
        }

        AddItemPopup.IsOpen = true;
    }

    private void OpenPastePopup(object sender, bool sell)
    {
        _viewModel?.BeginAdd(sell);
        if (sender is UIElement target)
        {
            PastePopup.PlacementTarget = target;
        }

        PastePopup.IsOpen = true;
    }

    private void OnAddSelectedClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var viewModel = _viewModel;
        RunGuarded(async () =>
        {
            await viewModel.AddSelectedToTargetAsync();
            AddItemPopup.IsOpen = false; // 加完自动收起
        });
    }

    private void OnImportPasteClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var viewModel = _viewModel;
        RunGuarded(async () =>
        {
            await viewModel.ImportPasteAsync();
            PastePopup.IsOpen = false;
        });
    }

    // ==================================================================
    //  清单编辑
    // ==================================================================

    private void OnRemoveSellItemClick(object sender, RoutedEventArgs e) => RemoveSingleItem(sender);

    private void OnRemoveBuyItemClick(object sender, RoutedEventArgs e) => RemoveSingleItem(sender);

    private void RemoveSingleItem(object sender)
    {
        if (_viewModel is not null && sender is FrameworkElement { DataContext: CompareMarketItemEdit item })
        {
            _viewModel.RemoveEditItems([item]);
        }
    }

    private void OnSellItemsRemoveClick(object sender, RoutedEventArgs e) => RemoveSelectedItems(SellItemsGrid);

    private void OnBuyItemsRemoveClick(object sender, RoutedEventArgs e) => RemoveSelectedItems(BuyItemsGrid);

    private void OnSellItemsMoveToBuyClick(object sender, RoutedEventArgs e) => MoveSelectedItems(SellItemsGrid, toSell: false);

    private void OnBuyItemsMoveToSellClick(object sender, RoutedEventArgs e) => MoveSelectedItems(BuyItemsGrid, toSell: true);

    private void RemoveSelectedItems(DataGrid grid)
    {
        var items = grid.SelectedItems.OfType<CompareMarketItemEdit>().ToList();
        if (items.Count > 0)
        {
            _viewModel?.RemoveEditItems(items);
        }
    }

    private void MoveSelectedItems(DataGrid grid, bool toSell)
    {
        var items = grid.SelectedItems.OfType<CompareMarketItemEdit>().ToList();
        if (items.Count > 0)
        {
            _viewModel?.MoveEditItems(items, toSell);
        }
    }

    private void OnDismissWarningClick(object sender, RoutedEventArgs e) => _viewModel?.DismissWarning();

    // ==================================================================
    //  计算 / 取消
    // ==================================================================

    private void OnCalculateClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            RunGuarded(_viewModel.CalculateAsync);
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => _viewModel?.Cancel();

    /// <summary>异步事件处理器的统一守卫（界面事件是 async void，异常不能逃逸）。</summary>
    private static async void RunGuarded(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(ex.Message);
        }
    }
}
