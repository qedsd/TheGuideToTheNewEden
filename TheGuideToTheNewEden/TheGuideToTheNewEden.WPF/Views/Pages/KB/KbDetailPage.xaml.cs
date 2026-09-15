using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.KB;
using TheGuideToTheNewEden.WPF.ViewModels.KB;
using TheGuideToTheNewEden.WPF.Views.UserControls.KB;

namespace TheGuideToTheNewEden.WPF.Views.Pages.KB;

/// <summary>KB 详情页：受害者/价值/攻击者/货柜，可复制链接或用浏览器打开。</summary>
public partial class KbDetailPage : Page
{
    private readonly KbDetailViewModel _viewModel;

    public KbDetailPage(KBItemInfo info)
    {
        InitializeComponent();

        _viewModel = new KbDetailViewModel(info);
        DataContext = _viewModel;

        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    /// <summary>标签标题。</summary>
    public string TabTitle => _viewModel.Title;

    /// <summary>供宿主页面把标签标题绑定到受害者名。</summary>
    public KbDetailViewModel ViewModel => _viewModel;

    private void OnCopyLinkClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(ZkbMapping.BuildKillWebUrl(_viewModel.KillmailId));
            PageNotifyService.Success(Application.Current?.TryFindResource("KB_Copied") as string ?? "OK");
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(ex.Message);
        }
    }

    private void OnOpenBrowserClick(object sender, RoutedEventArgs e) =>
        KillListControl.OpenUrl(ZkbMapping.BuildKillWebUrl(_viewModel.KillmailId));

    /// <summary>
    /// 详情页里的实体链接（受害者/参与者的名字、势力、舰船，以及受害者侧的星系、星域）：
    /// 各链接的 <c>Tag</c> 上挂着对应的 <see cref="IdName"/>，点击即跳到 ZKB 页打开该实体的统计标签
    /// ——与 KB 列表里的实体链接共用同一条导航链路。
    /// </summary>
    private void OnEntityLinkClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: IdName idName })
        {
            KbNavigation.OpenEntity(idName);
        }
    }
}
