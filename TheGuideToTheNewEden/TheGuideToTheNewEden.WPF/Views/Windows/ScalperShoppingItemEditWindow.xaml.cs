using System.Windows;
using TheGuideToTheNewEden.Core.Models.Market;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>购物车条目的价格/数量编辑对话框（对应 WinUI 版的 AddToShoppingCartDialog）。</summary>
public partial class ScalperShoppingItemEditWindow : FluentWindow
{
    private ScalperShoppingItem? _item;
    private bool _loading;

    public ScalperShoppingItemEditWindow(string title)
    {
        InitializeComponent();
        TitleBar.Title = title;
        Title = title;

        BuyPriceBox.ValueChanged += (_, _) => UpdatePreview();
        SellPriceBox.ValueChanged += (_, _) => UpdatePreview();
        QuantityBox.ValueChanged += (_, _) => UpdatePreview();
    }

    public void SetItem(ScalperShoppingItem item)
    {
        _item = item;
        _loading = true;
        BuyPriceBox.Value = item.BuyPrice;
        SellPriceBox.Value = item.SellPrice;
        QuantityBox.Value = item.Quantity;
        _loading = false;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (_loading)
        {
            return;
        }

        var buy = BuyPriceBox.Value ?? 0;
        var sell = SellPriceBox.Value ?? 0;
        var quantity = QuantityBox.Value ?? 0;
        RoiText.Text = buy == 0 ? "0.00" : ((sell - buy) / buy * 100).ToString("N2");
        NetProfitText.Text = ((sell - buy) * quantity).ToString("N2");
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (_item is null)
        {
            DialogResult = false;
            return;
        }

        _item.BuyPrice = BuyPriceBox.Value ?? 0;
        _item.SellPrice = SellPriceBox.Value ?? 0;
        _item.Quantity = QuantityBox.Value ?? 0;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
