using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Home;
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

    /// <summary>
    /// 把详情窗当前星系固定到主页（P3 参数化磁贴：<c>map + 星系 ID</c>）——
    /// 点主页磁贴会跳回星图并定位到该星系。
    /// </summary>
    private void PinSystem_Click(object sender, RoutedEventArgs e)
    {
        var node = ViewModel.Node;
        if (HomeShortcutService.Add("map", node.Id.ToString(CultureInfo.InvariantCulture), node.Name))
        {
            PageNotifyService.Success(LanguageService.Format("Home.Quick.Pinned", node.Name));
        }
        else
        {
            PageNotifyService.Info(HomeShortcutService.IsFull
                ? LanguageService.Format("Home.Quick.LimitReached", HomeShortcutService.MaxItems)
                : LanguageService.Format("Home.Quick.AlreadyPinned", node.Name));
        }
    }
}
