using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
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
/// Zkillboard 主页面：多标签宿主 + 标签行右端的**常驻搜索框**（宽 200，输入即展开结果下拉；结果列表自身仍用
/// <see cref="Wpf.Ui.Controls.Flyout"/> 弹出）。
/// 自定义 TabControl 模板：头行为 WinUI3 TabView 式单行滚动（溢出经左右卷动钮/滚轮横滚，不再换行），
/// 右侧预留 240px 由页面级覆盖层的搜索框与之对齐（与 TabItem Header 同行）；
/// **"击杀流"的标签头钉在头行左端**（页面级覆盖层，见 <c>PinnedStreamHeader</c>），实体/详情标签在它右侧滚动，
/// 因此首个 TabItem 的头部被收敛为零宽——它只负责承载内容与选中态，不再在标签行里占位。
/// 首个标签固定为"击杀流"，其余为按需打开的实体统计标签与 KB 详情标签（可关闭，实例常驻）。
/// 标签内容统一用 <see cref="Frame"/> 承载（Page 只能由 Window/Frame 托管）。
/// </summary>
public partial class ZKBPage : Page
{
    private readonly ZkbPageViewModel _viewModel = new();
    private readonly DispatcherTimer _searchTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly Dictionary<(EntityType Type, int Id), TabItem> _entityTabs = [];
    private readonly Dictionary<int, TabItem> _killTabs = [];

    /// <summary>头行标签溢出时向左/向右卷动（KbTabsTemplate 内的卷动钮经路由命令调用）。</summary>
    public static readonly RoutedCommand HeaderScrollLeftCommand = new("HeaderScrollLeft", typeof(ZKBPage));
    public static readonly RoutedCommand HeaderScrollRightCommand = new("HeaderScrollRight", typeof(ZKBPage));

    /// <summary>头行滚动宿主——模板命名空间元素，x:Name 不生成页面字段，Loaded 时经 Template.FindName 解析。</summary>
    private ScrollViewer? _headerScroll;

    /// <summary>模板里的标签容器（DockPanel）——同样在模板命名空间，用于让开左侧钉住的头部。</summary>
    private FrameworkElement? _headerTabsPanel;

    public ZKBPage()
    {
        InitializeComponent();
        DataContext = _viewModel;

        // 第一个标签固定为击杀流（不可关闭）。它的头部由头行左端**钉住**的 PinnedStreamHeader 代替，
        // 所以这里把真实 TabItem 的头部与尺寸都收敛掉：它照常承载内容与选中态，但不在标签行里占位置
        // （否则会出现第二个"击杀流"）。零宽的形状不影响程序化选中与 BringIntoView。
        // 注意 Height 必须显式给 36：头部为空 + Padding=0 时 TabItem 会连高度一起塌掉，
        // 标签行（Auto 行）随之高度为 0，页面级覆盖层（钉住头部 / 搜索框）就会压到内容区上。
        Tabs.Items.Add(new TabItem
        {
            Header = null,
            Content = HostInFrame(new KillStreamPage()),
            Margin = new Thickness(0),
            Padding = new Thickness(0),
            MinWidth = 0,
            Width = 0,
            Height = 36,
        });

        _searchTimer.Tick += async (_, _) =>
        {
            _searchTimer.Stop();
            await _viewModel.SearchAsync(SearchBox.Text);
        };

        // 头行卷动：命令绑定 + 滚动宿主解析 + 选中标签滚入可视区（单行滚动模式下不会自动带出）
        CommandBindings.Add(new CommandBinding(HeaderScrollLeftCommand, OnHeaderScrollExecuted, OnHeaderScrollCanExecute));
        CommandBindings.Add(new CommandBinding(HeaderScrollRightCommand, OnHeaderScrollExecuted, OnHeaderScrollCanExecute));
        Tabs.Loaded += OnTabsLoaded;
        Tabs.SelectionChanged += OnTabsSelectionChanged;

        // 钉住头部宽度变化（语言切换会让标题变宽变窄）时，重新让开标签行的左端
        PinnedStreamHeaderHost.SizeChanged += (_, _) => SyncPinnedHeaderOffset();

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
            SearchFlyout.Hide();
            return;
        }

        // 搜索框常驻，结果下拉随输入展开（内容随后由防抖搜索填入）
        if (!SearchFlyout.IsOpen)
        {
            SearchFlyout.Show();
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
        SearchFlyout.Hide();
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
        var (entity, killmailId, knownInfo) = KbNavigation.Drain();

        if (entity is not null)
        {
            OpenEntity(entity.EntityType, entity.Id, entity.Title);
        }
        else if (killmailId > 0)
        {
            _ = OpenKillmailAsync(killmailId, knownInfo);
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

    private async Task OpenKillmailAsync(int killmailId, KBItemInfo? knownInfo = null)
    {
        if (_killTabs.TryGetValue(killmailId, out var existing))
        {
            Tabs.SelectedItem = existing;
            return;
        }

        // 通知/列表点击携带的已富化数据直接用——击杀刚广播的几秒内 ZKB API 还查不到，重查必然"查询失败"
        var info = knownInfo;
        if (info is null)
        {
            // 先取回富化数据再建页（标签标题要用受害者名）
            info = await ZkbQueryService.GetKillmailAsync(killmailId);
            if (info is null)
            {
                PageNotifyService.Error(FindString("ZKBPage_QueryFailed"));
                return;
            }
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
    //  标签行：钉住的击杀流头部 + 溢出卷动（WinUI3 TabView 式）
    // ==================================================================

    /// <summary>点击左端钉住的"击杀流"头部 → 选中第一个标签。</summary>
    private void OnPinnedStreamHeaderClick(object sender, RoutedEventArgs e)
    {
        if (Tabs.Items.Count > 0)
        {
            Tabs.SelectedIndex = 0;
        }
    }

    private void OnTabsLoaded(object sender, RoutedEventArgs e)
    {
        // 模板命名空间里的 x:Name 不生成页面字段，只能经 Template.FindName 解析（REFACTORING §9 第 46 条）
        _headerScroll = Tabs.Template?.FindName("HeaderScroll", Tabs) as ScrollViewer;
        if (_headerScroll is not null)
        {
            // 滚动位置/标签增删都会改变 ScrollableWidth，及时刷新卷动钮的 CanExecute（到端点置灰）
            _headerScroll.ScrollChanged += (_, _) => CommandManager.InvalidateRequerySuggested();
        }

        _headerTabsPanel = Tabs.Template?.FindName("HeaderTabsPanel", Tabs) as FrameworkElement;
        SyncPinnedHeaderOffset();
    }

    /// <summary>
    /// 让标签行让开左端"钉住的击杀流头部"：把模板里的标签容器左外边距设为钉住头部的实际宽度，
    /// 于是实体/详情标签始终在它右侧滚动；标题随语言变宽变窄时也会自动跟随。
    /// </summary>
    private void SyncPinnedHeaderOffset()
    {
        if (_headerTabsPanel is null)
        {
            return;
        }

        _headerTabsPanel.Margin = new Thickness(PinnedStreamHeaderHost.ActualWidth, 0, 0, 0);
    }

    private void OnTabsSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        (Tabs.SelectedItem as FrameworkElement)?.BringIntoView();

    private void OnHeaderScrollExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (_headerScroll is null)
        {
            return;
        }

        var delta = e.Command == HeaderScrollLeftCommand ? -160d : 160d;
        _headerScroll.ScrollToHorizontalOffset(_headerScroll.HorizontalOffset + delta);
    }

    private void OnHeaderScrollCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (_headerScroll is null)
        {
            e.CanExecute = false;
            return;
        }

        var overflow = _headerScroll.ScrollableWidth > 0.5;
        e.CanExecute = overflow && (e.Command == HeaderScrollLeftCommand
            ? _headerScroll.HorizontalOffset > 0.5
            : _headerScroll.ScrollableWidth - _headerScroll.HorizontalOffset > 0.5);
    }

    private void OnHeaderScrollPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // 头行没有纵向滚动，把滚轮转成横向卷动（对齐 WinUI3 TabView 手感）
        if (sender is ScrollViewer sv && e.Delta != 0)
        {
            e.Handled = true;
            sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta);
        }
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
