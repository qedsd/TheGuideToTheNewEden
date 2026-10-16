using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Assistant;
using TheGuideToTheNewEden.WPF.Views.UserControls.Settings;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Settings;

/// <summary>
/// 「设置 → AI」的宿主页，**钻取式导航**（与设置首页同一套观感）：
/// <list type="number">
///   <item><b>二级菜单</b>（<c>SectionView</c>）：四节占满整页，像设置首页那样一列卡片（图标 + 标题 + 说明 + 箭头）；</item>
///   <item><b>三级详情</b>（<c>DetailView</c>）：点某一节后整页换成该节内容。**本页不画标题栏**——
///     导航标题由外层设置页的面包屑统一显示成「设置 › AI › 节名」（<see cref="ISettingsDetailPage"/>），
///     点面包屑里的「AI」即回到二级菜单。</item>
/// </list>
/// 四节：模型与翻译（<see cref="AiTranslationSettingView"/>，改动即时保存）／行为（<see cref="AiBehaviorSettingView"/>）／
/// ESI 全量访问（<see cref="AiEsiSettingView"/>）／工具开关（<see cref="AiToolsSettingView"/>）。
/// <para>
/// 分节视图**首次进入才创建、之后缓存**（返回再进来不重新读设置，未保存的编辑不会丢）；
/// 每个分节自己带「保存」，「恢复默认」在二级菜单页面底部，只重置助手参数（不含模型端点与 Key）。
/// </para>
/// </summary>
public partial class AiAssistantSettingPage : Page, ISettingsDetailPage
{
    /// <summary>一个分节（二级菜单的一项 + 它的三级详情视图工厂）。</summary>
    public sealed class AiSection : INotifyPropertyChanged
    {
        public required string Key { get; init; }

        public required SymbolRegular Icon { get; init; }

        public required string TitleKey { get; init; }

        public required string DescriptionKey { get; init; }

        public required Func<UserControl> CreateView { get; init; }

        public string Title => Resolve(TitleKey);

        public string Description => Resolve(DescriptionKey);

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>语言切换后刷新标题与描述。</summary>
        public void Refresh()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
        }

        private static string Resolve(string key) => Application.Current?.TryFindResource(key) as string ?? key;
    }

    /// <summary>待打开的分节（由"去配置"这类按钮设置，进入本页时消费一次）。</summary>
    private static string? _pendingSection;

    /// <summary>请求"下次进入 AI 设置时直接打开某一节"（键见 <see cref="SectionKeys"/>）。</summary>
    public static void RequestSection(string key) => _pendingSection = key;

    private readonly List<AiSection> _sections = [];
    private readonly Dictionary<string, UserControl> _views = [];

    public AiAssistantSettingPage()
    {
        InitializeComponent();
        BuildSections();
        SectionList.ItemsSource = _sections;

        LanguageService.LanguageChanged += OnLanguageChanged;
        Unloaded += (_, _) => LanguageService.LanguageChanged -= OnLanguageChanged;
        Loaded += (_, _) => SelectInitialSection();
    }

    /// <summary>已注册的分节键（探针据此逐个切换核验）。</summary>
    public IReadOnlyList<string> SectionKeys => _sections.Select(s => s.Key).ToList();

    /// <summary>当前是否停在二级菜单（false = 已在某一节的三级详情里）。</summary>
    public bool IsShowingSectionList => SectionView.Visibility == Visibility.Visible;

    /// <summary>当前三级详情显示的是哪一节（在二级菜单时为空）。</summary>
    public string? CurrentSectionKey { get; private set; }

    /// <summary>第三段面包屑：某一节的标题（在二级菜单时为空 ⇒ 外层只显示「设置 › AI」）。</summary>
    public string? BreadcrumbSubTitle
        => CurrentSectionKey is null
            ? null
            : _sections.FirstOrDefault(s => s.Key == CurrentSectionKey)?.Title;

    /// <summary>层级变化时通知外层设置页刷新面包屑。</summary>
    public event EventHandler? BreadcrumbChanged;

    /// <summary>外层面包屑点了「AI」→ 回到二级菜单。</summary>
    public void NavigateBackToParent() => ShowSectionList();

    /// <summary>
    /// 跳到某一节的三级详情（供"去配置"深链与探针使用）；键不存在时回二级菜单，返回是否命中了给定键。
    /// </summary>
    public bool SelectSection(string key)
    {
        var section = _sections.FirstOrDefault(s => s.Key == key);
        if (section is null)
        {
            ShowSectionList();
            return false;
        }

        ShowSection(section);
        return true;
    }

    /// <summary>返回二级菜单（外层面包屑点「AI」、或深链键无效时）。</summary>
    public void ShowSectionList()
    {
        CurrentSectionKey = null;
        SectionHost.Content = null;
        DetailView.Visibility = Visibility.Collapsed;
        SectionView.Visibility = Visibility.Visible;
        BreadcrumbChanged?.Invoke(this, EventArgs.Empty);
    }

    private void BuildSections()
    {
        _sections.Clear();
        _sections.Add(new AiSection
        {
            Key = "Model",
            Icon = SymbolRegular.BoxMultiple24,
            TitleKey = "AssistantSettingPage_Model",
            DescriptionKey = "AssistantSettingPage_Section_Model",
            CreateView = () => new AiModelSettingView(),
        });
        _sections.Add(new AiSection
        {
            Key = "Translate",
            Icon = SymbolRegular.Translate24,
            TitleKey = "AssistantSettingPage_Translate",
            DescriptionKey = "AssistantSettingPage_Section_Translate",
            CreateView = () => new AiTranslationSettingView(),
        });
        _sections.Add(new AiSection
        {
            Key = "Behavior",
            Icon = SymbolRegular.Settings24,
            TitleKey = "AssistantSettingPage_Behavior",
            DescriptionKey = "AssistantSettingPage_Section_Behavior",
            CreateView = () => new AiBehaviorSettingView(),
        });
        _sections.Add(new AiSection
        {
            Key = "Esi",
            Icon = SymbolRegular.Shield24,
            TitleKey = "AssistantSettingPage_Esi",
            DescriptionKey = "AssistantSettingPage_Section_Esi",
            CreateView = () => new AiEsiSettingView(),
        });
        _sections.Add(new AiSection
        {
            Key = "Tools",
            Icon = SymbolRegular.Sparkle24,
            TitleKey = "AssistantSettingPage_Tools",
            DescriptionKey = "AssistantSettingPage_Section_Tools",
            CreateView = () => new AiToolsSettingView(),
        });
    }

    private void SelectInitialSection()
    {
        var pending = _pendingSection;
        _pendingSection = null;
        if (string.IsNullOrEmpty(pending))
        {
            ShowSectionList();
            return;
        }

        SelectSection(pending);
    }

    private void Section_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: AiSection section })
        {
            ShowSection(section);
        }
    }

    private void BackToList_Click(object sender, RoutedEventArgs e) => ShowSectionList();

    private void ShowSection(AiSection section)
    {
        if (!_views.TryGetValue(section.Key, out var view))
        {
            view = section.CreateView();
            _views[section.Key] = view;
        }

        CurrentSectionKey = section.Key;
        SectionHost.Content = view;
        SectionView.Visibility = Visibility.Collapsed;
        DetailView.Visibility = Visibility.Visible;
        BreadcrumbChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnRestoreAllClick(object sender, RoutedEventArgs e)
    {
        // 只恢复**助手自己的**参数（轮数/提示词/确认/默认角色/ESI/工具开关/按需声明）。
        // 模型端点与翻译设置不在这里重置——那会连带把 Key/地址清掉，代价太大。
        AiAssistantSettingService.RestoreDefaults();
        foreach (var view in _views.Values)
        {
            switch (view)
            {
                case AiModelSettingView model:
                    model.Load();
                    break;
                case AiTranslationSettingView translation:
                    translation.Load();
                    break;
                case AiBehaviorSettingView behavior:
                    behavior.Load();
                    break;
                case AiEsiSettingView esi:
                    esi.Load();
                    break;
                case AiToolsSettingView tools:
                    tools.Load();
                    break;
            }
        }

        App.MainWindow?.RegisterAssistantHotkey();
        PageNotifyService.Info(FindString("AssistantSettingPage_Restored"));
    }

    private void OnLanguageChanged(object? sender, string language)
    {
        foreach (var section in _sections)
        {
            section.Refresh();
        }

        // 分节标题变了 ⇒ 第三段面包屑也要跟着变
        if (CurrentSectionKey is not null)
        {
            BreadcrumbChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
