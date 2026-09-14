using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.ViewModels.KB;

namespace TheGuideToTheNewEden.WPF.Views.Pages.KB;

/// <summary>
/// 击杀流页（ZKB 主页面的第一个固定标签）：连接/断开 ZKB 实时流，展示"筛选 KB / 已过滤 KB"两个列表，
/// 内置设置面板（通用 + 三组过滤黑白名单）。
/// </summary>
public partial class KillStreamPage : Page
{
    private readonly KillStreamViewModel _viewModel = new();

    public KillStreamPage()
    {
        InitializeComponent();
        DataContext = _viewModel;

        Loaded += async (_, _) => await _viewModel.InitializeAsync();
        Unloaded += (_, _) => _viewModel.Dispose();
    }

    private async void OnConnectClick(object sender, RoutedEventArgs e) => await _viewModel.ConnectAsync();

    private void OnDisconnectClick(object sender, RoutedEventArgs e) => _viewModel.Disconnect();

    private void OnClearClick(object sender, RoutedEventArgs e) => _viewModel.ClearList();

    private void OnSettingClick(object sender, RoutedEventArgs e) =>
        _viewModel.IsSettingVisible = !_viewModel.IsSettingVisible;

    private void OnListOpenKillmail(KBItemInfo info) => KbNavigation.OpenKillmail(info.SKBDetail.KillmailId);

    private void OnListEntityClicked(IdName idName) => KbNavigation.OpenEntity(idName);
}
