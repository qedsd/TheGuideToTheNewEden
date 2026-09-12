using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.WPF.ViewModels.Channel;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 频道统计页（对齐 WinUI 版 Views/Channel/ChannelScanPage）：
/// 左侧粘贴频道成员名单，右侧"统计"（军团/联盟人数分布）与"详细"（每个角色的 ZKB 战绩表），
/// 中间设置面板（ZKB 开关与数量上限、忽略名单管理）。
/// </summary>
public partial class ChannelScanPage : Page
{
    private readonly ChannelScanViewModel _viewModel = new();

    public ChannelScanPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private async void OnStartClick(object sender, RoutedEventArgs e) => await _viewModel.StartAsync();

    private void OnSettingClick(object sender, RoutedEventArgs e) => _viewModel.IsSetting = true;

    private void OnHideSettingClick(object sender, RoutedEventArgs e) => _viewModel.IsSetting = false;

    private void OnAddIgnoreClick(object sender, RoutedEventArgs e) => _viewModel.ShowAddIgnore();

    private void OnConfirmAddIgnoreClick(object sender, RoutedEventArgs e) => _viewModel.ConfirmAddIgnore();

    private void OnCancelAddIgnoreClick(object sender, RoutedEventArgs e) => _viewModel.CancelAddIgnore();

    private void OnIgnoreDeleteClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is IdName item)
        {
            _viewModel.DeleteIgnore(item);
        }
    }

    private TheGuideToTheNewEden.Core.Models.CharacterScan.CharacterScanInfo? SelectedScanInfo
        => (ResultDataGrid.SelectedItem as TheGuideToTheNewEden.Core.Models.CharacterScan.CharacterScanInfo)
            ?? (ResultDataGrid.CurrentCell.Item as TheGuideToTheNewEden.Core.Models.CharacterScan.CharacterScanInfo);

    private void OnAddIgnoreCharacterClick(object sender, RoutedEventArgs e)
    {
        if (SelectedScanInfo is { } info)
        {
            _viewModel.AddIgnore(new IdName(info.Character.Id, info.Character.Name, IdName.CategoryEnum.Character));
        }
    }

    private void OnAddIgnoreCorporationClick(object sender, RoutedEventArgs e)
    {
        if (SelectedScanInfo is { } info)
        {
            _viewModel.AddIgnore(new IdName(info.Corporation.Id, info.Corporation.Name, IdName.CategoryEnum.Corporation));
        }
    }

    private void OnAddIgnoreAllianceClick(object sender, RoutedEventArgs e)
    {
        if (SelectedScanInfo is { } info)
        {
            _viewModel.AddIgnore(new IdName(info.Alliance.Id, info.Alliance.Name, IdName.CategoryEnum.Alliance));
        }
    }

    private async void OnReloadZKBClick(object sender, RoutedEventArgs e)
    {
        if (SelectedScanInfo is { } info)
        {
            await _viewModel.ReloadZKBInfoAsync(info);
        }
    }
}
