using System.Windows;
using System.Windows.Controls;
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
}
