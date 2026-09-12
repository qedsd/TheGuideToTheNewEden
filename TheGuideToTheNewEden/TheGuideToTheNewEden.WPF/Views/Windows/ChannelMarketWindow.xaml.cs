using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using TheGuideToTheNewEden.Core.Models.ChannelMarket;
using TheGuideToTheNewEden.WPF.ViewModels.Channel;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 频道查价的置顶结果窗（对齐 WinUI 版 <c>Wins/ChannelMarketWindow</c>）：
/// 识别到查价请求后弹到前台展示报价（多物品汇总+卡片列表 / 单物品卡片+三个月价格曲线）。
/// 关闭即隐藏（复用时再显示），窗体标题带市场星域名。
/// </summary>
public partial class ChannelMarketWindow : Wpf.Ui.Controls.FluentWindow
{
    public ChannelMarketResultViewModel ViewModel { get; } = new();

    private bool _allowClose;

    public ChannelMarketWindow()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    /// <summary>弹到前台并更新结果。</summary>
    public void Query(IEnumerable<MarketChatContent> contents, int regionId, string regionName)
    {
        TitleBar.Title = $"{FindString("Nav.ChannelMarket")} - {regionName}";
        if (!IsVisible)
        {
            Show();
        }
        else
        {
            Activate();
        }

        _ = ViewModel.UpdateContentAsync(contents, regionId);
    }

    public void RestorePos()
    {
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Left = double.NaN;
        Top = double.NaN;
    }

    /// <summary>隐藏（不销毁，下次查询复用）。</summary>
    public void HideWindow()
    {
        if (IsVisible)
        {
            Hide();
        }
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await ViewModel.RefreshAsync();

    /// <summary>关闭即隐藏（复用小窗）：直接关掉会导致下次查询 Show() 抛异常。</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideWindow();
        }

        base.OnClosing(e);
    }

    /// <summary>真正销毁窗口。</summary>
    public void CloseWindow()
    {
        _allowClose = true;
        Close();
    }

    private void OnDetailClick(object sender, RoutedEventArgs e)
    {
        // 多物品列表里按钮的 DataContext 是该物品的结果；单物品卡片回退到 VM 的结果
        var typeId = ((sender as FrameworkElement)?.DataContext as ChannelMarketResult)?.Item?.TypeID
            ?? ViewModel.Result?.Item?.TypeID;
        if (typeId is > 0)
        {
            // 跳到市场页并选中该物品，同时把主窗口置前
            TheGuideToTheNewEden.WPF.Services.Navigation.NavigateToMarket(typeId.Value);
        }
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
