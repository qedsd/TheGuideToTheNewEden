using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.ViewModels.Map;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.Map;

/// <summary>行星资源清单视图（星域 / 星系 / 设施升级三页签）。</summary>
public partial class PlanetResourceListView : UserControl
{
    public PlanetResourceListView()
    {
        InitializeComponent();
        ViewModel = new PlanetResourceListViewModel();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }

    public PlanetResourceListViewModel ViewModel { get; }

    private async void Reload_Click(object sender, RoutedEventArgs e) => await ViewModel.ReloadAsync();
}
