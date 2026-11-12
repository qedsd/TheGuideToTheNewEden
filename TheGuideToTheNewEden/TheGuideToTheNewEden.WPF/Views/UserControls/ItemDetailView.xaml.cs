using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.ViewModels;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>
/// 物品详情内容（阶段 283）：由 <see cref="Windows.ItemDetailWindow"/> 装进统一的 ToolWindow 外壳里显示。
/// 「查看市场详情」只跳市场并选中物品，窗口本身保持打开；从市场自己的入口打开时可用 <see cref="ShowMarketButton"/> 隐藏这个按钮。
/// </summary>
public partial class ItemDetailView : UserControl
{
    public static readonly DependencyProperty ShowMarketButtonProperty = DependencyProperty.Register(
        nameof(ShowMarketButton),
        typeof(bool),
        typeof(ItemDetailView),
        new PropertyMetadata(true, OnShowMarketButtonChanged));

    public ItemDetailView()
    {
        InitializeComponent();
        ApplyMarketBarVisibility(ShowMarketButton);
    }

    /// <summary>是否显示底部的「查看市场详情」按钮（从市场功能里打开时隐藏，避免"从市场跳市场"）。</summary>
    public bool ShowMarketButton
    {
        get => (bool)GetValue(ShowMarketButtonProperty);
        set => SetValue(ShowMarketButtonProperty, value);
    }

    private static void OnShowMarketButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ItemDetailView view)
        {
            view.ApplyMarketBarVisibility((bool)e.NewValue);
        }
    }

    private void ApplyMarketBarVisibility(bool visible)
    {
        if (MarketBar is not null)
        {
            MarketBar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void OnGoMarketClick(object sender, RoutedEventArgs e)
    {
        // 只把主窗口带到市场页并选中物品，详情窗口保持打开（用户要对照着看）。
        if (DataContext is ItemDetailViewModel viewModel)
        {
            Navigation.NavigateToMarket(viewModel.TypeId);
        }
    }
}
