using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.ChannelIntel;
using TheGuideToTheNewEden.WPF.ViewModels.Channel;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 频道查价页（对齐 WinUI 版 Views/Channel/ChannelMarketPage）：
/// 左角色列表、中频道勾选列表、右查价设置（触发关键词/物品分隔符/市场星域）。
/// 命中查价请求后由 <see cref="Services.ChannelIntel.ChannelMarketService"/>
/// 弹出置顶结果窗展示卖买 5%/最优价、总量与历史走势。
/// </summary>
public partial class ChannelMarketPage : Page
{
    private readonly ChannelMarketViewModel _viewModel = new();

    public ChannelMarketPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ChannelMarketViewModel.HasSession))
            {
                RegionList.ItemsSource = _viewModel.Regions;
                ApplyRegionFilter(RegionSearchBox.Text);
            }
        };
    }

    // ---------- 角色与频道 ----------

    private void OnCharacterSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CharacterList.SelectedItem is ChannelIntelListener character)
        {
            _viewModel.SelectedCharacter = character;
        }
    }

    private async void OnRefreshCharactersClick(object sender, RoutedEventArgs e) => await _viewModel.RefreshCharactersAsync();

    private async void OnRefreshChannelsClick(object sender, RoutedEventArgs e) => await _viewModel.RefreshChannelsAsync();

    // ---------- 按钮 ----------

    private async void OnStartClick(object sender, RoutedEventArgs e) => await _viewModel.StartAsync();

    private void OnStopClick(object sender, RoutedEventArgs e) => _viewModel.StopSelected();

    private async void OnStartAllClick(object sender, RoutedEventArgs e) => await _viewModel.StartAllAsync();

    private void OnStopAllClick(object sender, RoutedEventArgs e) => _viewModel.StopAll();

    private void OnRestorePosClick(object sender, RoutedEventArgs e) => Services.ChannelIntel.ChannelMarketService.Current.RestorePos();

    private void OnApplySettingToAllClick(object sender, RoutedEventArgs e) => _viewModel.ApplySettingToAll();

    // ---------- 市场星域选择 ----------

    private void OnRegionSearchChanged(object sender, TextChangedEventArgs e)
        => ApplyRegionFilter(RegionSearchBox.Text);

    /// <summary>星域列表默认列出全部，搜索就地过滤。</summary>
    private void ApplyRegionFilter(string? keyword)
    {
        var text = keyword?.Trim();
        RegionList.ItemsSource = string.IsNullOrEmpty(text)
            ? _viewModel.Regions
            : _viewModel.Regions.Where(p => p.RegionName?.Contains(text, StringComparison.OrdinalIgnoreCase) == true
                || p.RegionID.ToString().Contains(text)).ToList();
    }

    private void OnRegionSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RegionList.SelectedItem is MapRegion region)
        {
            _viewModel.SelectRegion(region);
            RegionToggle.IsChecked = false;
        }
    }
}
