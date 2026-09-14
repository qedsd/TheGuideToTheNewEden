using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Navigation;
using System.Windows.Threading;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.KB;
using TheGuideToTheNewEden.WPF.ViewModels.KB;
using TheGuideToTheNewEden.WPF.Views.Pages.KB;
using ZKB.NET;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// Zkillboard 主页面：顶部实体搜索 + 多标签宿主。
/// 首个标签固定为"击杀流"，其余为按需打开的实体统计标签与 KB 详情标签（可关闭，实例常驻）。
/// 标签内容统一用 <see cref="Frame"/> 承载（Page 只能由 Window/Frame 托管）。
/// </summary>
public partial class ZKBPage : Page
{
    private readonly ZkbPageViewModel _viewModel = new();
    private readonly DispatcherTimer _searchTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly Dictionary<(EntityType Type, int Id), TabItem> _entityTabs = [];
    private readonly Dictionary<int, TabItem> _killTabs = [];

    public ZKBPage()
    {
        InitializeComponent();
        DataContext = _viewModel;

        // 第一个标签固定为击杀流（不可关闭）
        Tabs.Items.Add(CreateTabItem(FindString("ZKBHomePage_KillStream"), new KillStreamPage(), closeable: false));

        _searchTimer.Tick += async (_, _) =>
        {
            _searchTimer.Stop();
            await _viewModel.SearchAsync(SearchBox.Text);
        };

        KbNavigation.Requested += DrainAndOpen;
        Loaded += (_, _) => DrainAndOpen();
    }

    // ==================================================================
    //  实体搜索
    // ==================================================================

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _searchTimer.Stop();

        if (string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            _viewModel.ClearResults();
            return;
        }

        _searchTimer.Start();
    }

    private void OnSearchResultSelected(object sender, SelectionChangedEventArgs e)
    {
        if (SearchResultList.SelectedItem is not IdName idName)
        {
            return;
        }

        SearchResultList.SelectedItem = null;
        SearchBox.Text = string.Empty;
        _viewModel.ClearResults();

        if (ZkbMapping.TryToEntityType(idName.GetCategory(), out var entityType))
        {
            OpenEntity(entityType, idName.Id, idName.Name);
        }
        else
        {
            PageNotifyService.Info(FindString("ZKBPage_UnsupportedEntity"));
        }
    }

    // ==================================================================
    //  外部跳转（角色工作区 ZKB 卡片、击杀列表、统计页里的实体名…）
    // ==================================================================

    private void DrainAndOpen()
    {
        var (entity, killmailId) = KbNavigation.Drain();

        if (entity is not null)
        {
            OpenEntity(entity.EntityType, entity.Id, entity.Title);
        }
        else if (killmailId > 0)
        {
            _ = OpenKillmailAsync(killmailId);
        }
    }

    private void OpenEntity(EntityType entityType, int id, string? title)
    {
        if (id <= 0)
        {
            return;
        }

        var key = (entityType, id);
        if (_entityTabs.TryGetValue(key, out var existing))
        {
            Tabs.SelectedItem = existing;
            return;
        }

        var page = new EntityStatistPage(entityType, id, title);

        var header = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        header.SetBinding(TextBlock.TextProperty, new Binding(nameof(EntityStatistViewModel.Title)) { Source = page.ViewModel });

        TabItem? tab = null;
        tab = CreateTabItem(BuildHeader(header, () =>
        {
            if (tab is not null)
            {
                CloseTab(tab);
            }
        }), page, closeable: true);

        _entityTabs[key] = tab;
        Tabs.Items.Add(tab);
        Tabs.SelectedItem = tab;
    }

    private async Task OpenKillmailAsync(int killmailId)
    {
        if (_killTabs.TryGetValue(killmailId, out var existing))
        {
            Tabs.SelectedItem = existing;
            return;
        }

        // 先取回富化数据再建页（标签标题要用受害者名）
        var info = await ZkbQueryService.GetKillmailAsync(killmailId);
        if (info is null)
        {
            PageNotifyService.Error(FindString("ZKBPage_QueryFailed"));
            return;
        }

        var page = new KbDetailPage(info);

        var header = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        header.SetBinding(TextBlock.TextProperty, new Binding(nameof(KbDetailViewModel.Title)) { Source = page.ViewModel });

        TabItem? tab = null;
        tab = CreateTabItem(BuildHeader(header, () =>
        {
            if (tab is not null)
            {
                CloseTab(tab);
            }
        }), page, closeable: true);

        _killTabs[killmailId] = tab;
        Tabs.Items.Add(tab);
        Tabs.SelectedItem = tab;
    }

    // ==================================================================
    //  标签管理
    // ==================================================================

    private TabItem CreateTabItem(string title, Page page, bool closeable)
    {
        var header = new TextBlock
        {
            Text = title,
            VerticalAlignment = VerticalAlignment.Center,
        };

        return closeable
            ? CreateTabItem(BuildHeader(header, null), page, closeable: true)
            : new TabItem { Header = header, Content = HostInFrame(page) };
    }

    private static TabItem CreateTabItem(object header, Page page, bool closeable) =>
        new() { Header = header, Content = HostInFrame(page) };

    /// <summary>标签头部：标题 + 可选的关闭按钮。</summary>
    private object BuildHeader(TextBlock title, Action? onClose)
    {
        if (onClose is null)
        {
            return title;
        }

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        panel.Children.Add(title);

        var close = new Button
        {
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(2),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Content = new Wpf.Ui.Controls.SymbolIcon
            {
                Symbol = Wpf.Ui.Controls.SymbolRegular.Dismiss24,
                FontSize = 10,
            },
            ToolTip = FindString("General_Close"),
        };
        close.Click += (_, _) => onClose();
        panel.Children.Add(close);

        return panel;
    }

    private void CloseTab(TabItem tab)
    {
        foreach (var key in _entityTabs.Where(p => ReferenceEquals(p.Value, tab)).Select(p => p.Key).ToList())
        {
            _entityTabs.Remove(key);
        }

        foreach (var key in _killTabs.Where(p => ReferenceEquals(p.Value, tab)).Select(p => p.Key).ToList())
        {
            _killTabs.Remove(key);
        }

        var index = Tabs.Items.IndexOf(tab);
        Tabs.Items.Remove(tab);

        if (Tabs.Items.Count > 0)
        {
            Tabs.SelectedIndex = Math.Clamp(index, 0, Tabs.Items.Count - 1);
        }
    }

    private static Frame HostInFrame(Page page) => new()
    {
        Content = page,
        NavigationUIVisibility = NavigationUIVisibility.Hidden,
    };

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
