using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Views.Pages;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.Map;

/// <summary>
/// 星图设置窗里的「筛选与图层」页：筛选控件直接绑定 MapPageViewModel，
/// 图层开关回调 MapPage，保证与画布/持久化状态一致。
/// </summary>
public partial class MapFilterLayerSettingsView : UserControl
{
    private readonly MapPage _page;
    private bool _updating;

    public MapFilterLayerSettingsView(MapPage page)
    {
        InitializeComponent();
        _page = page;
        DataContext = page.ViewModel;

        _updating = true;
        BridgesToggle.IsChecked = page.ViewModel.ShowBridges;
        CharactersToggle.IsChecked = page.ViewModel.ShowCharacters;
        LogosToggle.IsChecked = page.IsLogosVisible;
        _updating = false;
    }

    private void FilterApply_Click(object sender, RoutedEventArgs e)
        => _page.ApplyMapFilter();

    private void FilterClear_Click(object sender, RoutedEventArgs e)
        => _page.ClearMapFilter();

    private void LayerToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_updating)
        {
            return;
        }

        if (ReferenceEquals(sender, BridgesToggle))
        {
            _page.SetBridgesVisible(BridgesToggle.IsChecked == true);
        }
        else if (ReferenceEquals(sender, CharactersToggle))
        {
            _page.SetCharactersVisible(CharactersToggle.IsChecked == true);
        }
        else if (ReferenceEquals(sender, LogosToggle))
        {
            _page.SetLogosVisible(LogosToggle.IsChecked == true);
        }
    }
}
