using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.ViewModels.KB;
using TheGuideToTheNewEden.WPF.Views.UserControls.KB;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Views.Pages.KB;

/// <summary>
/// 击杀流页（ZKB 主页面的第一个固定标签）：整页卡片布局，Footer 放连接/断开/清空/设置按钮，
/// 列表区展示"筛选 KB / 已过滤 KB"两个标签；设置在独立弹窗（<see cref="ToolWindow"/> + <see cref="KillStreamSettingView"/>）中打开。
/// </summary>
public partial class KillStreamPage : Page
{
    private readonly KillStreamViewModel _viewModel = new();
    private ToolWindow? _settingWindow;

    public KillStreamPage()
    {
        InitializeComponent();
        DataContext = _viewModel;

        Loaded += async (_, _) => await _viewModel.InitializeAsync();
        Unloaded += (_, _) =>
        {
            // 页签切换/关闭即收掉设置弹窗（设置视图绑定的是本页 VM，VM 已随 Unloaded 退订流事件）
            _settingWindow?.Close();
            _settingWindow = null;
            _viewModel.Dispose();
        };
    }

    private async void OnConnectClick(object sender, RoutedEventArgs e) => await _viewModel.ConnectAsync();

    private void OnDisconnectClick(object sender, RoutedEventArgs e) => _viewModel.Disconnect();

    private void OnClearClick(object sender, RoutedEventArgs e) => _viewModel.ClearList();

    /// <summary>设置弹窗：ToolWindow 单实例（重复点击仅激活），绑定同一份 KillStreamViewModel / Config，改动即全局生效。</summary>
    private void OnSettingClick(object sender, RoutedEventArgs e)
    {
        if (_settingWindow is null)
        {
            _settingWindow = new ToolWindow(
                new KillStreamSettingView(_viewModel),
                ToolWindowTitleStyle.Default,
                showTopmostButton: false,
                showInTaskbar: true,
                width: 680,
                height: 640)
            {
                Owner = Window.GetWindow(this),
                DisplayTitle = FindString("General_Setting"),
            };
            _settingWindow.Closed += (_, _) => _settingWindow = null;
        }

        _settingWindow.Show();
        _settingWindow.Activate();
    }

    private void OnListOpenKillmail(KBItemInfo info) => KbNavigation.OpenKillmail(info);

    private void OnListEntityClicked(IdName idName) => KbNavigation.OpenEntity(idName);

    private static string FindString(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? key;
}
