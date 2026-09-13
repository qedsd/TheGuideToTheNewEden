using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.ViewModels.Settings;
using TheGuideToTheNewEden.WPF.Views.Pages.Settings;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 设置壳页：分类列表 + 子页详情（面包屑返回）。
/// 子页实例常驻缓存，切回时保留状态。
/// </summary>
public partial class SettingsPage : Page
{
    private readonly List<SettingsCategory> _categories = [];
    private readonly Dictionary<Type, Page> _pageCache = [];
    private Type? _currentPageType;

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

    /// <summary>
    /// 请求"下次进入设置页时直接打开某个分类"（如翻译页 AI 页签的「去配置」）。
    /// 只记一个待办，进入页面时消费一次，避免影响用户之后正常进入设置页的默认视图。
    /// </summary>
    public static void RequestCategory(Type pageType) => _pendingCategory = pageType;

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
        Add(SymbolRegular.Document24, "SettingPage_GameLog", "SettingPage_GameLog_Desc", typeof(GameLogSettingPage), () => new GameLogSettingPage());
        Add(SymbolRegular.DataTrending24, "SettingPage_Market", "SettingPage_Market_Desc", typeof(MarketSettingPage), () => new MarketSettingPage());
        Add(SymbolRegular.Building24, "SettingPage_Structures", "SettingPage_Structures_Desc", typeof(StructuresSettingPage), () => new StructuresSettingPage());
        Add(SymbolRegular.Shield24, "SettingPage_ESIScope", "SettingPage_ESIScope_Desc", typeof(ESIScopeSettingPage), () => new ESIScopeSettingPage());
        Add(SymbolRegular.Scan24, "SettingPage_ZKB", "SettingPage_ZKB_Desc", typeof(ZKBSettingPage), () => new ZKBSettingPage());
        Add(SymbolRegular.Translate24, "SettingPage_AiTranslation", "SettingPage_AiTranslation_Desc", typeof(AiTranslationSettingPage), () => new AiTranslationSettingPage());
        Add(SymbolRegular.LocalLanguage24, "SettingPage_Glossary", "SettingPage_Glossary_Desc", typeof(GlossarySettingPage), () => new GlossarySettingPage());
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
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        DetailView.Visibility = Visibility.Collapsed;
        CategoryView.Visibility = Visibility.Visible;
    }

    private SettingsCategory? FindCategory(Type pageType)
    {
        return _categories.FirstOrDefault(c => c.PageType == pageType);
    }
}