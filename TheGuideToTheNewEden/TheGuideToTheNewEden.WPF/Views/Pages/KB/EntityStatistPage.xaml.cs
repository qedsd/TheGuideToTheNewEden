using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WPF.Models.KB;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.KB;
using TheGuideToTheNewEden.WPF.ViewModels.KB;
using TheGuideToTheNewEden.WPF.Views.UserControls.KB;
using ZKB.NET;

namespace TheGuideToTheNewEden.WPF.Views.Pages.KB;

/// <summary>
/// 实体统计页（一个 ZKB 实体一个标签）：左侧信息卡 + 右侧 6 个子页签。
/// 子页签数据懒加载——只有切到该页签才会取数（避免 WinUI 那种"打开实体就 new 出 5 个子页面并各自取数"）。
/// </summary>
public partial class EntityStatistPage : Page
{
    private readonly EntityStatistViewModel _viewModel;

    public EntityStatistPage(EntityType entityType, int id, string? title)
    {
        InitializeComponent();

        _viewModel = new EntityStatistViewModel(entityType, id, title);
        DataContext = _viewModel;

        // 页面实例常驻：只有首次进入才取数（重新进入不重复显示等待遮罩）
        Loaded += async (_, _) =>
        {
            if (_viewModel.HasStatistic)
            {
                return;
            }

            await RunAsync(() => _viewModel.LoadAsync(), "ZKBPage_LoadingStatistic");
        };
    }

    /// <summary>标签标题（ZKB 主页面用它命名标签）。</summary>
    public string TabTitle => _viewModel.Title;

    /// <summary>供宿主页面把标签标题绑定到实体的最终名称。</summary>
    public EntityStatistViewModel ViewModel => _viewModel;

    private async void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, StatTabs) || StatTabs.SelectedItem is not TabItem tab)
        {
            return;
        }

        switch (tab.Tag as string)
        {
            case "topvalue":
                await RunAsync(() => _viewModel.EnsureTopValueAsync());
                break;
            case "topalltime":
                await RunAsync(() => _viewModel.EnsureTopAllTimeAsync());
                break;
            case "group":
                await RunAsync(() => _viewModel.EnsureGroupsAsync());
                break;
            case "super":
                await RunAsync(() => _viewModel.EnsureSupersAsync());
                break;
        }
    }

    private async void OnRefreshKillListClick(object sender, RoutedEventArgs e) =>
        await RunAsync(() => _viewModel.ReloadKillmailsAsync(forceRefresh: true));

    private async void OnKillListPageChanged(object? sender, int page) =>
        await RunAsync(() => _viewModel.GoToPageAsync(page));

    private void OnListOpenKillmail(KBItemInfo info) => KbNavigation.OpenKillmail(info);

    private void OnListEntityClicked(IdName idName) => KbNavigation.OpenEntity(idName);

    /// <summary>概况卡"浏览器查看"按钮：打开该实体的 zkillboard 网页（同 WinUI 的 OpenInBrowerCommand）。</summary>
    private void OnOpenInBrowserClick(object sender, RoutedEventArgs e) =>
        KillListControl.OpenUrl(ZkbMapping.BuildEntityWebUrl(_viewModel.EntityType, _viewModel.Id));

    /// <summary>概况卡信息行的实体链接：跳转打开该实体的统计标签（同 WinUI 各 Button_*_Click）。</summary>
    private void OnInfoLinkClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: EntityStatistViewModel.EntityInfoRow row }
            && row.Link is not null)
        {
            KbNavigation.OpenEntity(row.Link);
        }
    }

    private void OnRankCardClicked(KillCardItem item)
    {
        if (item.IsKillmail)
        {
            KbNavigation.OpenKillmail(item.KillmailId);
        }
        else if (item.HasEntity)
        {
            KbNavigation.OpenEntity(item.EntityId, item.EntityCategory, item.Name);
        }
    }

    /// <summary>
    /// 执行一次异步操作并显示<b>页面局部</b>等待遮罩。
    /// 不走全局 PageNotifyService.ShowWaiting——实体页可同时打开多个并发加载，
    /// 全局遮罩会误遮其他实体标签；本页等待态由 <see cref="EntityStatistViewModel.IsBusy"/> 驱动、每页独立。
    /// </summary>
    private async Task RunAsync(Func<Task> action, string busyTextKey = "ZKBPage_Loading")
    {
        _viewModel.BeginBusy(FindString(busyTextKey));
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(ex.Message);
        }
        finally
        {
            _viewModel.EndBusy();
        }
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
