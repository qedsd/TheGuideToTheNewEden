using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.WPF.Models.Wormhole;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.ViewModels;
using TheGuideToTheNewEden.WPF.Views.UserControls.Wormhole;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 虫洞页：洞系浏览（搜索/筛选 + 详情 + zKillboard 活跃度分析）与洞口库两种模式。
/// 对应 WinUI 的 WormholePage，并新增：等级/天象筛选、天象与等级说明、洞口库、
/// 洞口详情窗（含过洞计算器）、热门船型与活跃时段分布、应用内 KB 跳转。
/// </summary>
public partial class WormholePage : Page
{
    private static readonly System.Collections.Generic.Dictionary<string, string> LinkUrlTemplates = new()
    {
        ["Anoik"] = "https://anoik.is/systems/{0}",
        ["Dotlan"] = "https://dotlan.eve-online.net/system/{0}",
        ["Ellatha"] = "https://www.ellatha.com/eve/wormholesystemview.asp?system={0}",
        ["Zkillboard"] = "https://zkillboard.com/system/{0}/",
        ["Chruker"] = "https://www.chruker.dk/eve_space/system_effects.php?system_name={0}",
    };

    private readonly WormholeViewModel _viewModel = new();

    public WormholePage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync();
        await _viewModel.LoadShipOptionsAsync();
        await _viewModel.LoadAllShipsAsync();
    }

    // ---------- 模式切换 ----------

    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        if (BrowserPanel is null || PortalLibraryPanel is null)
        {
            return;
        }

        var browser = ModeBrowserButton.IsChecked == true;
        BrowserPanel.Visibility = browser ? Visibility.Visible : Visibility.Collapsed;
        PortalLibraryPanel.Visibility = browser ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---------- 洞口详情 ----------

    private void OnPortalButtonClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is PortalButtonModel model)
        {
            ShowPortal(model.Portal);
        }
    }

    private void OnPortalGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (PortalGrid.SelectedItem is PortalRow row)
        {
            ShowPortal(row.Portal);
        }
    }

    private void ShowPortal(WormholePortal portal)
    {
        var title = string.Format(WormholeViewModel.FindString("WormholePage_PortalTitle"), portal.Name);
        var window = new ToolWindow(
            new WormholePortalView(portal, _viewModel.ShipOptions, _viewModel.AllShipItems),
            ToolWindowTitleStyle.Default,
            showTopmostButton: false,
            showInTaskbar: false,
            width: 540,
            height: 680)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            DisplayTitle = title,
            SystemTitle = title,
        };
        window.Show();
    }

    // ---------- 外链 / 复制 ----------

    private void OnLinkButtonClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Detail is null
            || (sender as FrameworkElement)?.DataContext is not string site
            || !LinkUrlTemplates.TryGetValue(site, out var template))
        {
            return;
        }

        OpenBrowser(string.Format(template, _viewModel.Detail.Name));
    }

    private void OnCopyNameClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Detail is null)
        {
            return;
        }

        try
        {
            Clipboard.SetText(_viewModel.Detail.Name);
            PageNotifyService.Success(WormholeViewModel.FindString("WormholePage_CopyDone"));
        }
        catch (System.Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (System.Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    // ---------- ZKB ----------

    private void OnRefreshKbClick(object sender, RoutedEventArgs e) => _viewModel.RefreshKb();

    private void OnOpenKbClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Detail is null)
        {
            return;
        }

        KbNavigation.OpenEntity(
            _viewModel.Detail.Id,
            Core.DBModels.IdName.CategoryEnum.SolarSystem,
            _viewModel.Detail.Name);
    }

    private void OnEntityButtonClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ActiveEntityItem item)
        {
            return;
        }

        KbNavigation.OpenEntity(
            item.EntityId,
            item.IsAlliance ? Core.DBModels.IdName.CategoryEnum.Alliance : Core.DBModels.IdName.CategoryEnum.Corporation,
            item.Name);
    }
}
