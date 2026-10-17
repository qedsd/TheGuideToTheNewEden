using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Manual;
using TheGuideToTheNewEden.WPF.ViewModels.Settings;
using TheGuideToTheNewEden.WPF.Views.Pages.Settings;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 设置壳页：分类列表 + 子页详情（面包屑返回）。
/// 子页实例常驻缓存，切回时保留状态。
/// </summary>
public partial class SettingsPage : Page, IManualAware
{
    private readonly List<SettingsCategory> _categories = [];
    private readonly Dictionary<Type, Page> _pageCache = [];
    private Type? _currentPageType;

    /// <summary>当前子页若支持"第三段面包屑"（<see cref="ISettingsDetailPage"/>），挂在这里。</summary>
    private ISettingsDetailPage? _detailPage;

    /// <summary>待打开的分类（由别的页面"去配置"这类按钮设置，进入本页时消费一次）。</summary>
    private static Type? _pendingCategory;

    public SettingsPage()
    {
        InitializeComponent();
        BuildCategories();

        LanguageService.LanguageChanged += OnLanguageChanged;
        Unloaded += (_, _) => LanguageService.LanguageChanged -= OnLanguageChanged;
        Loaded += (_, _) => ApplyPendingCategory();
    }

    /// <summary>请求"下次进入设置页时直接打开某个分类"（如翻译页 AI 页签的「去配置」）。
    /// 只记一个待办，进入页面时消费一次，避免影响用户之后正常进入设置页的默认视图。
    /// </summary>
    public static void RequestCategory(Type pageType) => _pendingCategory = pageType;

    /// <summary>当前详情区里的子页（无 = 停在分类列表）。</summary>
    public Page? CurrentDetailPage => DetailFrame.Content as Page;

    /// <summary>
    /// F1：停在分类列表 → <c>settings</c>（怎么进分类）；已进某个分类 → 该分类的手册
    /// （<c>settings/general</c>…，映射表在 <see cref="ManualToolMap"/>）。
    /// </summary>
    public string? ManualId
    {
        get
        {
            if (DetailView.Visibility != Visibility.Visible || _currentPageType is null)
            {
                return "settings";
            }

            return ManualToolMap.TryGetManual(_currentPageType) ?? "settings";
        }
    }

    private void ApplyPendingCategory()
    {
        var pending = _pendingCategory;
        if (pending is null)
        {
            return;
        }

        _pendingCategory = null;
        if (FindCategory(pending) is { } category)
        {
            OpenCategory(category);
        }
    }

    private void BuildCategories()
    {
        _categories.Clear();

        Add(SymbolRegular.Settings24, "SettingPage_General", "SettingPage_General_Desc", typeof(GeneralSettingPage), () => new GeneralSettingPage());
        Add(SymbolRegular.Home24, "SettingPage_Home", "SettingPage_Home_Desc", typeof(HomeSettingPage), () => new HomeSettingPage());
        Add(SymbolRegular.Document24, "SettingPage_GameLog", "SettingPage_GameLog_Desc", typeof(GameLogSettingPage), () => new GameLogSettingPage());
        Add(SymbolRegular.DataTrending24, "SettingPage_Market", "SettingPage_Market_Desc", typeof(MarketSettingPage), () => new MarketSettingPage());
        Add(SymbolRegular.Building24, "SettingPage_Structures", "SettingPage_Structures_Desc", typeof(StructuresSettingPage), () => new StructuresSettingPage());
        Add(SymbolRegular.Shield24, "SettingPage_ESIScope", "SettingPage_ESIScope_Desc", typeof(ESIScopeSettingPage), () => new ESIScopeSettingPage());
        // 注意：这里**没有** ZKB 分类——ZKB 实时流的全部设置（连接/通知/上限/排序/过滤）已并入
        // "击杀流"页右上角的设置弹窗（KillStreamSettingView）。原先的"设置 → Zkillboard"子页只维护
        // 一组没有任何代码读取的旧过滤字段，已整体删除（REFACTORING §8 第 48 条）。
        // 阶段 121：原「AI 翻译」分类已并入「AI 助手」——模型端点（Key/地址/模型/生成参数）搬到那里，
        // 翻译相关设置作为该页的「模型与翻译」子设置。设置里因此只有一个 AI 入口。
        Add(SymbolRegular.Sparkle24, "SettingPage_AiAssistant", "SettingPage_AiAssistant_Desc", typeof(AiAssistantSettingPage), () => new AiAssistantSettingPage());
        Add(SymbolRegular.LocalLanguage24, "SettingPage_Glossary", "SettingPage_Glossary_Desc", typeof(GlossarySettingPage), () => new GlossarySettingPage());
        // 「主页」分类（阶段 99 重建，阶段 100/101 扩充）：① 钱包/行情/最近使用三张卡片的显示开关
        // （阶段 99 从主页「⋯」菜单移入）；② 快捷方式编辑——拖动排序 / 移除 / 添加 / 宽窄 / 恢复默认。
        // 阶段 101 起主页的编辑模式与「⋯」菜单已删除，这里是快捷方式**全局唯一**的编辑入口
        // （阶段 94 删除的旧主页子页是上下移按钮的管理页，与现实现的拖动方式不同，不构成恢复）。
        Add(SymbolRegular.Keyboard24, "SettingPage_KeyboardList", "SettingPage_KeyboardList_Desc", typeof(KeyboardListPage), () => new KeyboardListPage());
        Add(SymbolRegular.Bug24, "SettingPage_Test", "SettingPage_Test_Desc", typeof(TestSettingPage), () => new TestSettingPage());
        Add(SymbolRegular.ArrowSync24, "SettingPage_Update", "SettingPage_Update_Desc", typeof(UpdateSettingPage), () => new UpdateSettingPage());

        CategoryList.ItemsSource = _categories;

        void Add(SymbolRegular icon, string titleKey, string descKey, Type pageType, Func<Page> factory)
        {
            _categories.Add(new SettingsCategory
            {
                Icon = icon,
                TitleKey = titleKey,
                DescriptionKey = descKey,
                PageType = pageType,
                CreatePage = factory,
            });
        }
    }

    private void OnLanguageChanged(object? sender, string language)
    {
        foreach (var category in _categories)
        {
            category.Refresh();
        }

        if (DetailView.Visibility == Visibility.Visible && _currentPageType is not null)
        {
            BreadcrumbTitle.Text = FindCategory(_currentPageType)?.Title ?? BreadcrumbTitle.Text;
        }
    }

    private void Category_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SettingsCategory category })
        {
            OpenCategory(category);
        }
    }

    /// <summary>
    /// 分类行右侧的小问号：打开该分类的内置说明书（<c>settings/general</c> …），
    /// **不进入分类**。必须把 <c>e.Handled</c> 置位——<c>Click</c> 是冒泡事件，
    /// 否则这一下会同时触发外层整行按钮的 <see cref="Category_Click"/>。
    /// </summary>
    private void CategoryManual_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not FrameworkElement { Tag: SettingsCategory category })
        {
            return;
        }

        if (ManualToolMap.TryGetManual(category.PageType) is { } id)
        {
            Views.Windows.ManualWindow.Show(id);
        }
    }

    /// <summary>打开某个分类的子页（分类列表 → 详情，并写好面包屑）。</summary>
    private void OpenCategory(SettingsCategory category)
    {
        if (!_pageCache.TryGetValue(category.PageType, out var page))
        {
            page = category.CreatePage();
            _pageCache[category.PageType] = page;
        }

        _currentPageType = category.PageType;
        DetailFrame.Content = page;
        BreadcrumbTitle.Text = category.Title;
        CategoryView.Visibility = Visibility.Collapsed;
        DetailView.Visibility = Visibility.Visible;
        AttachDetailBreadcrumb(page);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        AttachDetailBreadcrumb(null);
        DetailView.Visibility = Visibility.Collapsed;
        CategoryView.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 挂上/摘下子页的"第三段面包屑"（阶段 124）。子页自己有层级时（如 AI 页的分节），
    /// 面包屑显示成「设置 › AI › 行为」，**不再由子页自己画一条标题栏**。
    /// <para>
    /// 公开是给探针用的：**离屏窗口里的 <c>Frame</c> 不承载内容**（<c>Content</c>/<c>Navigate</c> 都"成功"
    /// 但 <c>Content</c> 始终为 null，连普通控件也一样），所以"面包屑联动"这一段只能直接驱动挂载点来核验。
    /// </para>
    /// </summary>
    public void AttachDetailBreadcrumb(Page? page)
    {
        if (_detailPage is not null)
        {
            _detailPage.BreadcrumbChanged -= OnDetailBreadcrumbChanged;
        }

        _detailPage = page as ISettingsDetailPage;
        if (_detailPage is not null)
        {
            _detailPage.BreadcrumbChanged += OnDetailBreadcrumbChanged;
        }

        UpdateSubBreadcrumb();
    }

    private void OnDetailBreadcrumbChanged(object? sender, EventArgs e) => UpdateSubBreadcrumb();

    private void UpdateSubBreadcrumb()
    {
        var subTitle = _detailPage?.BreadcrumbSubTitle;
        var hasSub = !string.IsNullOrWhiteSpace(subTitle);
        BreadcrumbSubTitle.Text = hasSub ? subTitle : string.Empty;
        BreadcrumbSubTitle.Visibility = hasSub ? Visibility.Visible : Visibility.Collapsed;
        BreadcrumbSubChevron.Visibility = hasSub ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>点面包屑里的分类名：让子页回到它自己的上一层（无下一层时什么都不做）。</summary>
    private void BreadcrumbParent_Click(object sender, RoutedEventArgs e) => _detailPage?.NavigateBackToParent();

    private SettingsCategory? FindCategory(Type pageType)
    {
        return _categories.FirstOrDefault(c => c.PageType == pageType);
    }
}