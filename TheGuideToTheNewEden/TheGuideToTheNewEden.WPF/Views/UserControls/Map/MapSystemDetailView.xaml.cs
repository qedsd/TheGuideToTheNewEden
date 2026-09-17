using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.ViewModels.Map;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.Map;

/// <summary>星系详情视图（五个页签；由星图页用 ToolWindow 承载）。</summary>
public partial class MapSystemDetailView : UserControl
{
    public MapSystemDetailView(MapSystemNode node, MapPageViewModel map)
    {
        InitializeComponent();
        ViewModel = new MapSystemDetailViewModel(node, map);
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }

    public MapSystemDetailViewModel ViewModel { get; }
}
