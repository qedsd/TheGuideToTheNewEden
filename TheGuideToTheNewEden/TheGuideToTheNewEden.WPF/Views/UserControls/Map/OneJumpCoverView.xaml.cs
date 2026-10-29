using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TheGuideToTheNewEden.Core.Models.Map;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.ViewModels.Map;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.Map;

/// <summary>一跳覆盖工具视图（由星图页用 ToolWindow 承载；支持窗口内搜索中心星系）。</summary>
public partial class OneJumpCoverView : UserControl
{
    private readonly MapPageViewModel? _mapViewModel;
    private readonly ObservableCollection<MapSystemNode> _searchResults = [];
    private bool _syncingCenter;

    public OneJumpCoverView()
        : this(null)
    {
    }

    public OneJumpCoverView(MapPageViewModel? mapViewModel)
    {
        InitializeComponent();
        _mapViewModel = mapViewModel;
        CenterSearchResultsList.ItemsSource = _searchResults;
        DataContext = ViewModel;
        ThemeForegroundHelper.Attach(this);
        SyncCenterBox();
    }

    public OneJumpCoverViewModel ViewModel { get; } = new();

    /// <summary>设置中心星系（星图页选中项 / 搜索结果）。</summary>
    public void SetCenter(MapSystemNode? node)
    {
        ViewModel.SetCenter(node);
        SyncCenterBox();
        CenterSearchPopup.IsOpen = false;
    }

    private void SyncCenterBox()
    {
        _syncingCenter = true;
        CenterSearchBox.Text = ViewModel.Center is null ? string.Empty : ViewModel.CenterText;
        _syncingCenter = false;
    }

    private void CenterSearchBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        => CenterSearchBox.SelectAll();

    private void CenterSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncingCenter || _mapViewModel is null)
        {
            return;
        }

        var text = CenterSearchBox.Text?.Trim() ?? string.Empty;
        _searchResults.Clear();
        if (text.Length < 2)
        {
            CenterSearchPopup.IsOpen = false;
            return;
        }

        _mapViewModel.Search(text, 8);
        foreach (var node in _mapViewModel.SearchResults)
        {
            _searchResults.Add(node);
        }

        CenterSearchPopup.IsOpen = _searchResults.Count > 0;
    }

    private void CenterSearchResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CenterSearchResultsList.SelectedItem is MapSystemNode node)
        {
            SetCenter(node);
            CenterSearchResultsList.SelectedItem = null;
        }
    }

    private void UseMapCenter_Click(object sender, RoutedEventArgs e)
    {
        if (_mapViewModel?.SelectedSystem is { } node)
        {
            SetCenter(node);
        }
    }

    private async void Compute_Click(object sender, RoutedEventArgs e) => await ViewModel.ComputeAsync();

    private void Clear_Click(object sender, RoutedEventArgs e) => ViewModel.Clear();
}
