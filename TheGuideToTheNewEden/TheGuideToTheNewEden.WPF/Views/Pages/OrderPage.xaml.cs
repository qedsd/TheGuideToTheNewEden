using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.ViewModels.Business;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>订单页：角色的个人/军团未结订单，以及与市场参考价的差值。</summary>
public partial class OrderPage : Page
{
    private readonly OrderPageViewModel _viewModel = new();

    /// <summary>首次加载时由"默认选中角色"触发的 SelectionChanged 不重复取数。</summary>
    private bool _initializing;

    public OrderPage()
    {
        InitializeComponent();

        DataContext = _viewModel;

        Loaded += async (_, _) =>
        {
            _initializing = true;
            _viewModel.EnsureDefaultCharacter();
            _initializing = false;
            await _viewModel.LoadAsync();
        };
    }

    private async void OnCharacterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initializing)
        {
            await _viewModel.LoadAsync();
        }
    }

    private async void OnOrderFromChanged(object sender, SelectionChangedEventArgs e)
    {
        await _viewModel.LoadAsync();
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await _viewModel.LoadAsync();

    /// <summary>复制为游戏批量购买订单：只复制被压单/未知的行，每行"物品名 1"。</summary>
    private void OnAddToGameOrderClick(object sender, RoutedEventArgs e)
    {
        var text = OrderPageViewModel.BuildGameOrderText(OrdersGrid.SelectedItems.Cast<StatusOrder>());
        if (text is null)
        {
            ShowMessage(FindString("OrderPage_AddToGameOrder_Failed"));
            return;
        }

        try
        {
            Clipboard.SetText(text);
            ShowMessage(FindString("OrderPage_AddToGameOrder_Success"));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            ShowMessage(ex.Message);
        }
    }

    /// <summary>在游戏客户端中打开该物品的市场详情。</summary>
    private async void OnShowInGameClick(object sender, RoutedEventArgs e)
    {
        if (OrdersGrid.SelectedItem is not StatusOrder order)
        {
            return;
        }

        var ok = await _viewModel.ShowInGameAsync(order);
        ShowMessage(FindString(ok ? "OrderPage_ShowInGame_Succcess" : "OrderPage_ShowInGame_Falied"));
    }

    private static void ShowMessage(string text) =>
        MessageBox.Show(text, FindString("AppDisplayName"), MessageBoxButton.OK, MessageBoxImage.Information);

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
