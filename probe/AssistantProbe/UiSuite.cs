using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using TheGuideToTheNewEden.WPF;
using TheGuideToTheNewEden.WPF.Controls;
using TheGuideToTheNewEden.WPF.Services.Assistant;
using TheGuideToTheNewEden.WPF.ViewModels.Assistant;
using TheGuideToTheNewEden.WPF.Views;
using TheGuideToTheNewEden.WPF.Views.Pages;
using TheGuideToTheNewEden.WPF.Views.Pages.Settings;
using TheGuideToTheNewEden.WPF.Views.UserControls;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.Probe;

/// <summary>
/// 界面层：载入应用资源字典后**真实构造**助手页与对话视图（缺失 <c>StaticResource</c> 会在这里抛），
/// 挂到离屏窗口渲染，监听 WPF 绑定错误（"绑定到不存在的属性会静默失败"是本项目的老坑），
/// 并断言关键结构（工具步骤区 / 确认卡片 / Markdown 表格渲染）与本地化键覆盖。
/// </summary>
internal static class UiSuite
{
    public static void Run(Check c)
    {
        // 先初始化 Core：否则读不到 settings.json 里的 AI 端点 → 助手页会显示成"未配置引导"形态，
        // 对话区整块 Collapsed，界面上那些"待确认卡片可见"之类的断言就成了假失败（阶段 115 踩到）
        ProbeCore.Ensure();

        c.Section("UiSuite · 应用资源字典");
        // App.xaml 里的字典 Source 都带程序集名（…;component/…），因此从探针这个"别的入口程序集"也能正常加载；
        // 不带程序集名时 WPF 会按入口程序集解析，会报"找不到资源 controls/globalcontrolstyles.xaml"。
        var app = ProbeApplication.Ensure();   // 与 Program.cs 一致：解析加载 WPF-UI 主题/控件字典 + 全局样式 + 语言字典

        c.That(Application.Current is not null, "Application 已建立");
        c.That(Application.Current!.TryFindResource("Assistant_Chat_Sessions") is string, "语言字典已载入（Assistant_Chat_Sessions 命中）");
        c.That(Application.Current.TryFindResource("StretchListItem") is not null, "全局样式 StretchListItem 可用（会话列表项 BasedOn 它）");
        c.That(Application.Current.TryFindResource("InverseBoolToVisibility") is not null, "App 级转换器 InverseBoolToVisibility 可用");

        c.Section("UiSuite · 本地化键覆盖（助手全部界面键 + 工具名）");
        LocalizationKeys(c);
        LanguageDictionaryIntegrity(c);

        c.Section("UiSuite · 图标颜色跟随主题");
        IconColors(c);

        c.Section("UiSuite · 页面 / 视图构造与绑定");
        ConstructionAndBinding(c);
    }

    /// <summary>
    /// 阶段 125：设置页里"深色主题下图标变黑"的回归守卫。
    /// <para>
    /// 根因：WPF-UI 的 <c>SymbolIcon</c> 默认样式**不设 Foreground**，会退回 Control 的默认值（系统色）；
    /// 项目里 12 处裸 <c>&lt;ui:SymbolIcon&gt;</c>（含设置页面包屑的 ">"）因此变成黑图标。
    /// 修法是 <c>Controls/GlobalControlStyles.xaml</c> 里一条全局隐式样式；
    /// 这里断言"裸图标拿到的就是主题色刷子"，并且**换主题后真的会变**（不是写死的黑/白）。
    /// </para>
    /// </summary>
    private static void IconColors(Check c)
    {
        var primary = Application.Current?.TryFindResource("TextFillColorPrimaryBrush") as SolidColorBrush;
        c.That(primary is not null, "主题刷子 TextFillColorPrimaryBrush 可用");

        var host = new StackPanel();
        var bareIcon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.ChevronRight24 };
        var secondaryIcon = new Wpf.Ui.Controls.SymbolIcon
        {
            Symbol = Wpf.Ui.Controls.SymbolRegular.ChevronRight24,
            Foreground = Application.Current?.TryFindResource("TextFillColorSecondaryBrush") as Brush,
        };
        host.Children.Add(bareIcon);
        host.Children.Add(secondaryIcon);

        var window = new Window
        {
            Width = 300,
            Height = 120,
            Left = -4000,
            Top = -4000,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            Content = host,
        };

        try
        {
            window.Show();
            Pump();

            var bareColor = (bareIcon.Foreground as SolidColorBrush)?.Color;
            c.That(bareColor == primary?.Color,
                $"裸 SymbolIcon 的 Foreground = TextFillColorPrimaryBrush（实际 {bareColor}）");
            c.That((secondaryIcon.Foreground as SolidColorBrush)?.Color
                   == (Application.Current?.TryFindResource("TextFillColorSecondaryBrush") as SolidColorBrush)?.Color,
                "元素上显式写的 Foreground 仍然优先（局部值不被隐式样式覆盖）");

            // 换主题必须跟着变（这才是"跟随主题"的证据；写死的黑/白会在这里露馅）。
            // 注意：主题刷子本身也会随主题换掉，所以每次切换后要**重新取一次**刷子再比。
            var before = bareColor;
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark, updateAccent: false);
            Pump();
            var darkBrush = (Application.Current?.TryFindResource("TextFillColorPrimaryBrush") as SolidColorBrush)?.Color;
            var dark = (bareIcon.Foreground as SolidColorBrush)?.Color;
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Light, updateAccent: false);
            Pump();
            var lightBrush = (Application.Current?.TryFindResource("TextFillColorPrimaryBrush") as SolidColorBrush)?.Color;
            var light = (bareIcon.Foreground as SolidColorBrush)?.Color;

            c.That(dark == darkBrush && light == lightBrush,
                $"切主题后图标色跟着变（{before} → 深 {dark} vs {darkBrush} → 浅 {light} vs {lightBrush}）");
            c.That(dark != light, "深浅主题下的图标色确实不同（不是写死的颜色）");
        }
        catch (Exception ex)
        {
            c.That(false, $"图标颜色核验抛异常：{ex.Message}");
        }
        finally
        {
            window.Close();
        }
    }

    private static void LocalizationKeys(Check c)
    {
        var keys = new List<string>
        {
            "Nav.Assistant",
            "Assistant_Chat_Sessions", "Assistant_Chat_New", "Assistant_Chat_NoSessions", "Assistant_Chat_Rename",
            "Assistant_Chat_Rename_Tip", "Assistant_Chat_Delete", "Assistant_Chat_Clear", "Assistant_Chat_Request",
            "Assistant_Chat_Answer", "Assistant_Chat_Generating", "Assistant_Chat_Stopped", "Assistant_Chat_Expand",
            "Assistant_Chat_Collapse", "Assistant_Chat_Confirm_Title", "Assistant_Chat_Confirm_Hint",
            "Assistant_Chat_Confirm_Apply", "Assistant_Chat_Confirm_Reject", "Assistant_Chat_CopyAnswer",
            "Assistant_Chat_CopySuccess", "Assistant_Chat_EmptyTip", "Assistant_Chat_UseContext",
            "Assistant_Chat_UseContext_Tip", "Assistant_Chat_Input", "Assistant_Chat_Input_Tip",
            "Assistant_Chat_Input_Hint", "Assistant_Chat_Send", "Assistant_Chat_Stop", "Assistant_Chat_PopWindow",
            "Assistant_Chat_Untitled", "Assistant_Chat_EmptySession", "Assistant_Chat_NewSession",
            "Assistant_Chat_ContextOn", "Assistant_Chat_ContextOff",
            "Assistant_Chat_ContextUsage", "Assistant_Chat_ContextUsageTrimmed", "Assistant_Chat_ContextUsageUnlimited",
            "Assistant_Chat_PendingCount",
            "Assistant_Chat_NoInput", "Assistant_Chat_AnswerEmpty", "Assistant_Chat_Timeout",
            "Assistant_Chat_ConfirmDisabled", "Assistant_AiUnavailable", "Assistant_NotConfigured_Title",
            "Assistant_NotConfigured_Support", "Assistant_GoConfigure", "Assistant_Recheck",
            "AssistantStep_Done", "AssistantStep_Failed", "SettingPage_AiAssistant", "SettingPage_AiAssistant_Desc",
            // 阶段 124：AI 设置页的二级菜单（四节标题 + 菜单简介 + 通知文案）
            "AssistantSettingPage_Model", "AssistantSettingPage_Behavior", "AssistantSettingPage_Esi",
            "AssistantSettingPage_Tools", "AssistantSettingPage_Section_Model", "AssistantSettingPage_Section_Behavior",
            "AssistantSettingPage_Section_Esi", "AssistantSettingPage_Section_Tools",
            "AssistantSettingPage_Section_Translate", "AiSettingPage_TranslationBehavior", "AiSettingPage_CustomPrompt",
            "AssistantSettingPage_Behavior", "AssistantSettingPage_Esi",
            "AssistantSettingPage_Model", "AssistantSettingPage_Translate",
            // 阶段 132：上下文预算（设置卡片 + 底栏提示）
            "AssistantSettingPage_ContextChars", "AssistantSettingPage_ContextChars_Desc",
            // 工具分组标题是运行时拼出来的键（$"AssistantSettingPage_ToolsGroup_{category}"），这里显式列出来防拼错
            "AssistantSettingPage_ToolsGroup_Esi", "AssistantSettingPage_ToolsGroup_Character",
            "AssistantSettingPage_ToolsGroup_Business", "AssistantSettingPage_ToolsGroup_Map",
            "AssistantSettingPage_ToolsGroup_Channel", "AssistantSettingPage_ToolsGroup_General",
            "Home.Quick.Placeholder", "Home.Quick.Go", "Home.Quick.JumpedMarket", "Home.Quick.JumpedKb",
            "Home.Quick.JumpedSystem", "Home.Quick.JumpedCharacter", "Home.Quick.ToAssistant",
        };

        // 工具中文名：每个**产品**工具都要有 AssistantTool_<name>（探针自己的 probe_* 不面向界面）
        keys.AddRange(ToolRegistry.All
            .Where(t => !t.Name.StartsWith("probe_", StringComparison.Ordinal))
            .Select(t => t.DisplayNameKey));

        var missing = keys.Where(key => Application.Current?.TryFindResource(key) is not string).Distinct().ToList();
        c.That(missing.Count == 0, $"{keys.Count} 个界面键全部命中（缺失 {missing.Count} 个）");
        foreach (var key in missing)
        {
            Console.WriteLine($"    缺键：{key}");
        }
    }

    /// <summary>
    /// 两份语言字典自身的完整性：**能各自独立加载**（ResourceDictionary 里同键重复 = 加载即抛
    /// XamlParseException）+ 中英键集一致。
    /// <para>
    /// 为什么要有这条：探针只在中文环境加载 zh-CN，**en-US 的问题永远暴露不出来**——阶段 124 就真漏进去
    /// 一个重复键（<c>SettingPage_AiAssistant_Desc</c>），只有切到英文界面才会崩。语言字典是编译成 BAML 的，
    /// 不能当 XML 解析，所以这里直接 new 一个 ResourceDictionary 去加载（不并入 Application.Resources，
    /// 不污染当前界面），加载成功本身就证明"没有重复键"。
    /// </para>
    /// </summary>
    private static void LanguageDictionaryIntegrity(Check c)
    {
        var zh = LoadLanguageDictionary("zh-CN.xaml");
        var en = LoadLanguageDictionary("en-US.xaml");
        c.That(zh is not null, "zh-CN 字典能独立加载（有重复键会在这里抛 XamlParseException）");
        c.That(en is not null, "en-US 字典能独立加载（探针默认只加载 zh-CN，这条专门守住 en）");

        if (zh is null || en is null)
        {
            return;
        }

        var zhKeys = zh.Keys.Cast<object>().Select(key => key.ToString()!).ToHashSet(StringComparer.Ordinal);
        var enKeys = en.Keys.Cast<object>().Select(key => key.ToString()!).ToHashSet(StringComparer.Ordinal);
        c.That(zhKeys.Count == zh.Keys.Count && enKeys.Count == en.Keys.Count,
            $"两份字典内部都不重复（zh {zhKeys.Count} / en {enKeys.Count} 个键）");

        var onlyZh = zhKeys.Except(enKeys, StringComparer.Ordinal).ToList();
        var onlyEn = enKeys.Except(zhKeys, StringComparer.Ordinal).ToList();
        c.That(onlyZh.Count == 0 && onlyEn.Count == 0,
            $"中英键集一致（仅中文 {onlyZh.Count} 个 / 仅英文 {onlyEn.Count} 个）");
        foreach (var key in onlyZh.Concat(onlyEn).Take(10))
        {
            Console.WriteLine($"    [键集差异] {key}");
        }

        // 阶段 129：界面上是 WPF 的 TextBlock/SettingCard，**不渲染 markdown**——
        // 语言值里出现 ** 就会把星号原样显示给用户（本轮实测：AI 设置的几段说明就是这样）。
        var markdown = zh.Cast<System.Collections.DictionaryEntry>()
            .Concat(en.Cast<System.Collections.DictionaryEntry>())
            .Where(entry => entry.Value is string text && text.Contains("**", StringComparison.Ordinal))
            .Select(entry => entry.Key?.ToString() ?? "<null>")
            .Distinct(StringComparer.Ordinal)
            .ToList();
        c.That(markdown.Count == 0, $"语言值里没有 markdown 残留（** 等；实际 {markdown.Count} 个键）");
        foreach (var key in markdown.Take(10))
        {
            Console.WriteLine($"    [markdown 残留] {key}");
        }
    }

    /// <summary>按 pack URI 加载语言字典（探针是另一个入口程序集，必须带程序集名，同 App.xaml 的写法）。
    /// <c>internal</c>：ManualsSuite 也要用同一份加载途径核对 group 键。</summary>
    internal static ResourceDictionary? LoadLanguageDictionary(string fileName)
    {
        var candidates = new[]
        {
            $"pack://application:,,,/TheGuideToTheNewEden;component/Resources/Languages/{fileName}",
            $"pack://application:,,,/Resources/Languages/{fileName}",
        };

        foreach (var candidate in candidates)
        {
            try
            {
                return new ResourceDictionary { Source = new Uri(candidate, UriKind.Absolute) };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    [诊断] 加载 {candidate} 失败：{ex.Message}");
            }
        }

        return null;
    }

    private static void ConstructionAndBinding(Check c)
    {
        // 绑定错误监听：静态资源缺失在构造期抛，绑定路径错误只在这里能被看见
        var bindingErrors = new List<string>();
        var listener = new CollectingTraceListener(bindingErrors);
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error | SourceLevels.Warning;

        Page? page = null;
        try
        {
            page = new AiAssistantPage();
        }
        catch (Exception ex)
        {
            c.That(false, $"AiAssistantPage 构造抛异常（多半是缺失 StaticResource）：{ex.Message}");
        }

        c.That(page is not null, "AiAssistantPage 构造成功");

        // 阶段 124：AI 设置页是**钻取式**导航——二级菜单占满整页，点某一节后整页换成该节详情（三级）+ 面包屑。
        // 真构造一次，然后逐节钻进去：StaticResource 缺失、绑定到只读属性、绑定路径写错这类问题
        // **编译期与 BAML 期都不报**，只有构造/渲染时才暴露（§9 第 88 条），本套件末尾的
        // "绑定错误 0 条"断言正好把四节的绑定问题一起看住。
        try
        {
            var settingsPage = new AiAssistantSettingPage();
            var settingsWindow = new Window
            {
                Width = 1000,
                Height = 780,
                Left = -4000,
                Top = -4000,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Content = settingsPage,
            };
            settingsWindow.Show();
            Pump();

            var expectedToggles = ToolRegistry.Product.Count;
            c.That(settingsPage.SectionKeys.Count == 5,
                $"二级菜单五节齐备（{string.Join(" / ", settingsPage.SectionKeys)}）");

            // 切节之后必须**同步跑一次布局**：新换上去的分节视图只有在布局后才出现在可视树里
            // （离屏窗口的 Dispatcher 泵不会替我们做这件事，第一版漏了这步 → 除首节外全查到 0 个控件）
            void Layout()
            {
                settingsPage.UpdateLayout();
                Pump();
            }

            // ⓪ 初始形态：二级菜单（五节列表）在，三级详情不在
            var sectionButtons = FindVisuals<System.Windows.Controls.Primitives.ButtonBase>(settingsPage)
                .Count(button => button is FrameworkElement { Tag: AiAssistantSettingPage.AiSection });
            c.That(settingsPage.IsShowingSectionList && settingsPage.CurrentSectionKey is null,
                "初始停在二级菜单（未钻取）");
            c.That(sectionButtons == 5, $"二级菜单渲染出 5 个可点击的分节行（实际 {sectionButtons} 个）");
            c.That(!FindVisuals<Wpf.Ui.Controls.PasswordBox>(settingsPage).Any(),
                "还没钻取时三级内容尚未加载（模型端点不在可视树里）");

            // ① 模型（阶段 125 与翻译拆成两节）：协议 / 思考模式下拉 + API Key 输入框 + 生成参数数字框
            c.That(settingsPage.SelectSection("Model"), "钻取到「模型」");
            Layout();
            c.That(!settingsPage.IsShowingSectionList && settingsPage.CurrentSectionKey == "Model",
                "已离开二级菜单，进入「模型」三级详情");
            c.That(settingsPage.BreadcrumbSubTitle is { Length: > 0 },
                $"三级详情把标题交给外层面包屑（{settingsPage.BreadcrumbSubTitle}），页内不再自带标题栏");
            var detailRoot = settingsPage.FindName("DetailView") as DependencyObject ?? settingsPage;
            var bigTexts = FindVisuals<TextBlock>(detailRoot)
                .Where(block => block.FontSize >= 22 && !string.IsNullOrWhiteSpace(block.Text))
                .ToList();
            foreach (var block in bigTexts)
            {
                Console.WriteLine($"    [诊断] 三级详情区里的大号文本 {block.FontSize}: 「{block.Text}」");
            }

            c.That(bigTexts.Count == 0,
                "三级详情区里没有大号标题栏（标题只在设置页的面包屑里；图标字形是 20 号，已排除）");
            c.That(FindVisuals<Wpf.Ui.Controls.PasswordBox>(settingsPage).Any(), "模型端点区已加载（API Key 输入框在位）");
            var combos = FindVisuals<ComboBox>(settingsPage).Count();
            c.That(combos >= 2, $"模型端点区已加载（协议 / 思考模式下拉在位，共 {combos} 个）");
            c.That(FindVisuals<Wpf.Ui.Controls.NumberBox>(settingsPage).Count() >= 3,
                "「模型」节有生成参数（温度 / 最大输出 token / 超时三个数字框）");

            // ①b 翻译（阶段 125 独立成节）：术语库 + 翻译行为（分段字符数 / 结果缓存）+ 自定义提示词
            var translateTitle = Application.Current?.TryFindResource("AssistantSettingPage_Translate") as string;
            c.That(settingsPage.SelectSection("Translate"), $"钻取到「{translateTitle}」");
            Layout();
            var translateToggles = FindVisuals<Wpf.Ui.Controls.ToggleSwitch>(settingsPage).Count();
            var translateNumbers = FindVisuals<Wpf.Ui.Controls.NumberBox>(settingsPage).Count();
            var translateBoxes = FindVisuals<Wpf.Ui.Controls.TextBox>(settingsPage).Count();
            c.That(translateToggles >= 2 && translateNumbers >= 2 && translateBoxes >= 1,
                $"「翻译」节渲染出术语库/缓存开关、注入上限与分段字符数、自定义提示词（开关 {translateToggles}、数字框 {translateNumbers}、文本框 {translateBoxes}）");
            c.That(!FindVisuals<Wpf.Ui.Controls.PasswordBox>(settingsPage).Any(),
                "「翻译」节里没有模型端点控件（模型与翻译确实分开了）");

            // ② 行为：轮数 / 默认角色 / 写确认 / 快捷键 / 系统提示词
            settingsPage.SelectSection("Behavior");
            Layout();
            var behaviorToggles = FindVisuals<Wpf.Ui.Controls.ToggleSwitch>(settingsPage).Count();
            var behaviorNumbers = FindVisuals<Wpf.Ui.Controls.NumberBox>(settingsPage).Count();
            var behaviorBoxes = FindVisuals<Wpf.Ui.Controls.TextBox>(settingsPage).Count();
            c.That(behaviorToggles >= 2 && behaviorNumbers >= 1 && behaviorBoxes >= 3,
                $"「行为」节渲染出轮数框 / 角色框 / 快捷键框 / 提示词框与两个开关（写操作确认 + 在主窗口显示 AI，开关 {behaviorToggles}、数字框 {behaviorNumbers}、文本框 {behaviorBoxes}）");
            // 阶段 129：逐项用 SettingCard 行 + 分隔线（不再标签+控件堆叠）
            var behaviorCards = FindVisuals<TheGuideToTheNewEden.WPF.Controls.SettingCard>(settingsPage).Count();
            var behaviorDividers = FindVisuals<Border>(settingsPage).Count(border => border.Height == 1);
            c.That(behaviorCards >= 6 && behaviorDividers >= 4,
                $"「行为」节是 6 个 SettingCard 行 + 分隔线（实际卡片 {behaviorCards}、分隔线 {behaviorDividers}）");

            // 阶段 132：行为节多了一张「上下文预算」卡片
            var contextBox = FindVisuals<Wpf.Ui.Controls.NumberBox>(settingsPage)
                .FirstOrDefault(box => box is FrameworkElement { Name: "ContextCharsBox" });
            c.That(contextBox is not null, "「行为」节有「上下文预算（字符）」数字框");
            c.That(contextBox is null || Math.Abs((contextBox.Value ?? -1) - AiAssistantSettingService.MaxContextChars) < 0.5,
                "上下文预算框回填了当前设置值");

            // 阶段 130：快捷键**默认不启用**（默认值为空），但输入框提示保留（示例 Alt+Q）
            c.That(AiAssistantSettingService.DefaultHotkey.Length == 0,
                "全局快捷键的默认值是空 = 默认不启用");
            c.Equal(AiAssistantSettingService.HotkeyHint, "Alt+Q", "输入框提示仍是默认示例 Alt+Q");
            var hotkeyBox = FindVisuals<Wpf.Ui.Controls.TextBox>(settingsPage)
                .FirstOrDefault(box => box is FrameworkElement { Name: "HotkeyBox" });
            c.That(hotkeyBox?.PlaceholderText == AiAssistantSettingService.HotkeyHint,
                $"设置页的快捷键输入框提示 = {AiAssistantSettingService.HotkeyHint}（实际「{hotkeyBox?.PlaceholderText}」）");

            // 三级详情首行高度必须与其它设置子页一致（阶段 129：用户实机反馈"与导航栏隔了一段高度"）：
            // AI 页曾多给一层 24px（ContentControl 顶部边距）+ 节标题自身 24px ⇒ 比别的页深一段。
            // 这里用"行为"节的首个 TextBlock（节标题）与参考页（通用设置）比 Y，差值必须 ≤ 4px。
            try
            {
                var aiView = (settingsPage.FindName("SectionHost") as ContentControl)?.Content as UserControl;
                var aiHeader = aiView is null ? null : FindVisuals<TextBlock>(aiView).FirstOrDefault();
                var aiY = aiHeader?.TransformToAncestor(settingsPage).Transform(new Point(0, 0)).Y ?? -1;

                var referencePage = new GeneralSettingPage();
                var referenceWindow = new Window
                {
                    Width = 1000,
                    Height = 780,
                    Left = -4000,
                    Top = -4000,
                    ShowActivated = false,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None,
                    Content = referencePage,
                };
                referenceWindow.Show();
                referencePage.UpdateLayout();
                Pump();
                var referenceHeader = FindVisuals<TextBlock>(referencePage).FirstOrDefault();
                var referenceY = referenceHeader?.TransformToAncestor(referencePage).Transform(new Point(0, 0)).Y ?? -1;
                referenceWindow.Close();

                c.That(aiY >= 0 && referenceY >= 0 && Math.Abs(aiY - referenceY) <= 4,
                    $"三级详情首行与其它设置页齐平（AI {aiY:F1}px vs 参考 {referenceY:F1}px）");
            }
            catch (Exception ex)
            {
                c.That(false, $"三级详情首行高度核验抛异常：{ex.Message}");
            }


            // ③ ESI 全量访问：三个开关 + 三个数字框 + 黑名单
            settingsPage.SelectSection("Esi");
            Layout();
            var esiToggles = FindVisuals<Wpf.Ui.Controls.ToggleSwitch>(settingsPage).Count();
            var esiNumbers = FindVisuals<Wpf.Ui.Controls.NumberBox>(settingsPage).Count();
            c.That(esiToggles >= 3 && esiNumbers >= 3,
                $"「ESI 全量访问」节渲染出网关/写/富化三个开关与页数·字符·错误配额三个数字框（开关 {esiToggles}、数字框 {esiNumbers}）");
            var esiCards = FindVisuals<TheGuideToTheNewEden.WPF.Controls.SettingCard>(settingsPage).Count();
            var esiDividers = FindVisuals<Border>(settingsPage).Count(border => border.Height == 1);
            c.That(esiCards >= 7 && esiDividers >= 6,
                $"「ESI 全量访问」节是 7 个 SettingCard 行 + 分隔线（实际卡片 {esiCards}、分隔线 {esiDividers}）");

            // ④ 工具开关：按需声明 + 每个产品工具一个开关（**只在这一节**，这正是钻取式导航的意义）
            settingsPage.SelectSection("Tools");
            Layout();
            var toolToggles = FindVisuals<Wpf.Ui.Controls.ToggleSwitch>(settingsPage).Count();
            c.That(toolToggles >= expectedToggles + 1,
                $"「工具开关」节渲染出 {toolToggles} 个开关 ≥ 产品工具数 {expectedToggles} + 按需声明开关（模型/行为/ESI 三节此时不占界面）");
            c.That(FindVisuals<Wpf.Ui.Controls.NumberBox>(settingsPage).Count() == 0,
                "「工具开关」节里没有别的节的控件（确认是「点击才加载」而不是全部铺在一页）");

            // ⑤ 面包屑返回二级菜单（点「AI」），再钻一次确认视图被复用
            settingsPage.ShowSectionList();
            Layout();
            c.That(settingsPage.IsShowingSectionList && settingsPage.CurrentSectionKey is null,
                "面包屑返回后回到二级菜单（四节列表）");
            c.That(FindVisuals<System.Windows.Controls.Primitives.ButtonBase>(settingsPage)
                    .Count(button => button is FrameworkElement { Tag: AiAssistantSettingPage.AiSection }) == 5,
                "返回后五节列表重新可见");
            c.That(!FindVisuals<Wpf.Ui.Controls.NumberBox>(settingsPage).Any(),
                "返回后三级内容已卸下（工具节留下的数字框不该还在）");

            // 未知分节键不崩，留在二级菜单
            c.That(!settingsPage.SelectSection("NoSuchSection"), "未知分节键返回 false（不抛异常）");
            Layout();
            c.That(settingsPage.IsShowingSectionList, "未知键时留在二级菜单，不会显示半截详情");

            settingsWindow.Close();
        }
        catch (Exception ex)
        {
            c.That(false, $"AiAssistantSettingPage 构造/渲染抛异常：{ex.Message}");
        }

        // 阶段 124：第三段导航标题由**外层设置页的面包屑**显示（「设置 › AI › 行为」），
        // AI 页里不再自带标题栏。这里把设置壳页也真构造一次，端到端核验两个方向：
        // ① 进「行为」节 → 面包屑出现第三段；② 点面包屑里的「AI」→ AI 页回到二级菜单、第三段收起。
        //
        // 先单独确认"深链直接落在某一节"这条（用一个自己加载的 AI 页；离屏 Frame 里的子页 Loaded
        // 时序不好把控，深链逻辑本身在这里独立验），再验壳页的面包屑联动。
        try
        {
            AiAssistantSettingPage.RequestSection("Behavior");
            var deepLinked = new AiAssistantSettingPage();
            var linkWindow = new Window
            {
                Width = 900,
                Height = 700,
                Left = -4000,
                Top = -4000,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Content = deepLinked,
            };
            linkWindow.Show();
            deepLinked.UpdateLayout();
            Pump();
            c.That(deepLinked.CurrentSectionKey == "Behavior" && !deepLinked.IsShowingSectionList,
                $"深链 RequestSection(\"Behavior\") 生效（当前节 {deepLinked.CurrentSectionKey}）");
            linkWindow.Close();
        }
        catch (Exception ex)
        {
            c.That(false, $"深链核验抛异常：{ex.Message}");
        }

        try
        {
            SettingsPage.RequestCategory(typeof(AiAssistantSettingPage));

            var shell = new SettingsPage();
            var shellWindow = new Window
            {
                Width = 1000,
                Height = 780,
                Left = -4000,
                Top = -4000,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Content = shell,
            };
            shellWindow.Show();
            shell.UpdateLayout();
            Pump();

            var crumbTitle = shell.FindName("BreadcrumbTitle") as TextBlock;
            var crumbSubTitle = shell.FindName("BreadcrumbSubTitle") as TextBlock;
            var crumbSubChevron = shell.FindName("BreadcrumbSubChevron") as Wpf.Ui.Controls.SymbolIcon;

            c.That(crumbTitle is not null && crumbSubTitle is not null && crumbSubChevron is not null,
                "设置壳页的面包屑三段元素就位");
            c.That(crumbTitle?.Text == (Application.Current?.TryFindResource("SettingPage_AiAssistant") as string),
                $"深链 RequestCategory(AI) 生效，面包屑第二段是分类名「{crumbTitle?.Text}」");

            // 离屏窗口里的 Frame **不承载内容**：Content 赋值与 Navigate 都"成功"（Navigate 返回 True、
            // 无 NavigationFailed），但 Frame.Content 始终是 null——连塞普通控件也一样（已实测）。
            // 真实窗口里 Frame 正常（设置子页一直在用），所以这里直接驱动面包屑挂载点，
            // 用一个**真实的** AI 页验两个方向：① 页面层级变化 → 面包屑第三段；② 点面包屑 → 页面回上一层。
            var aiPage = new AiAssistantSettingPage();
            shell.AttachDetailBreadcrumb(aiPage);
            shell.UpdateLayout();
            Pump();
            c.That(aiPage.IsShowingSectionList && string.IsNullOrEmpty(crumbSubTitle?.Text)
                   && crumbSubChevron?.Visibility != Visibility.Visible,
                "AI 页停在二级菜单时，面包屑只有两段（第三段收起）");

            // ① 进「行为」节 → 面包屑第三段出现
            aiPage.SelectSection("Behavior");
            shell.UpdateLayout();
            Pump();
            c.That(crumbSubTitle?.Text == (Application.Current?.TryFindResource("AssistantSettingPage_Behavior") as string)
                   && crumbSubTitle.Visibility == Visibility.Visible
                   && crumbSubChevron?.Visibility == Visibility.Visible,
                $"AI 页进入「行为」节后，外层面包屑第三段显示「{crumbSubTitle?.Text}」（页内不再自带标题栏）");

            // ② 点面包屑里的「AI」→ 回二级菜单，第三段收起
            (shell.FindName("BreadcrumbTitleButton") as System.Windows.Controls.Primitives.ButtonBase)?
                .RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            shell.UpdateLayout();
            Pump();
            c.That(aiPage.IsShowingSectionList, "点面包屑里的「AI」后，AI 页回到二级菜单");
            c.That(string.IsNullOrEmpty(crumbSubTitle?.Text) && crumbSubChevron?.Visibility != Visibility.Visible,
                "回到二级菜单后第三段收起（面包屑只剩「设置 › AI」）");

            shell.AttachDetailBreadcrumb(null);
            c.That(string.IsNullOrEmpty(crumbSubTitle?.Text), "离开详情（回到分类列表）时第三段也收起");

            shellWindow.Close();
        }
        catch (Exception ex)
        {
            c.That(false, $"设置壳页的面包屑端到端核验抛异常：{ex.Message}");
        }

        c.Section("UiSuite · 主窗口导航（助手是常驻入口）");

        var viewModel = new AiAssistantViewModel();
        var view = new AiAssistantChatView { DataContext = viewModel };

        // 造一条"最复杂"的记录：工具步骤（成功 + 失败）+ Markdown 表格回答 + 待确认写操作
        var session = new AiChatSessionViewModel(new AiAssistantSession { Title = "探针会话" });
        viewModel.Sessions.Insert(0, session);
        viewModel.SelectedSession = session;

        var turn = session.AddTurn(new AiAssistantTurn
        {
            UserText = "巨神兵级在吉他的价格，顺便帮我看下走势",
            Success = true,
        });
        turn.AddStep(new ToolStep("market_orders", "巨神兵级 @ 伏尔戈", "最低卖价 4,949,000", true, 0.83));
        turn.AddStep(new ToolStep("market_trend", "巨神兵级 @ 伏尔戈 90天", "位于区间 50%", false, 0.12));

        // 阶段 132：步骤必须**同时**写进落盘模型（此前只进界面 → 历史文件里一个步骤都没有，
        // 于是重新打开应用后既看不到旧步骤，也没法把工具往返重放给模型）
        c.Equal(turn.Model.Steps.Count, 2, "工具步骤写进了落盘模型（不再只进界面）");
        c.Equal(turn.Model.Steps[0].ToolName, "market_orders", "落盘步骤带工具名");
        c.That(turn.Model.Steps[0].ResultSummary.Length > 0, "落盘步骤带结果摘要（界面重开后仍能看到）");
        turn.SetAnswer(
            "| 项目 | 值 |\n|---|---|\n| 当前最低卖价 | 4,949,000 ISK |\n| 当前最高买价 | 4,759,000 ISK |\n\n" +
            "**涨了多少**：从 90 天低点涨了约 +5.6%。\n\n- 区间位置 50%，不算低\n- 价差 190,000 ISK\n");
        turn.SetPending([new PendingToolAction("c1", "map_jumpbridge_add", "吉他 ↔ 皮尔米特", """{"systemA":"吉他","systemB":"皮尔米特"}""")]);
        turn.IsPending = false;

        var window = new Window
        {
            Width = 1040,
            Height = 760,
            Left = -4000,
            Top = -4000,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            Content = view,
        };

        try
        {
            window.Show();
            Pump();
            c.That(view.IsLoaded, "对话视图已挂到离屏窗口并完成布局");
        }
        catch (Exception ex)
        {
            c.That(false, $"视图渲染抛异常：{ex.Message}");
        }

        // 结构断言
        var markdownHosts = FindVisuals<MarkdownPresenter>(view).ToList();
        c.That(markdownHosts.Count >= 1, $"回答气泡里有 MarkdownPresenter（实际 {markdownHosts.Count} 个）");
        if (markdownHosts.Count > 0)
        {
            var document = markdownHosts[0].Document;
            c.That(document.Blocks.Count >= 2, $"Markdown 渲染出多个块（实际 {document.Blocks.Count}）");
            c.That(document.Blocks.OfType<Table>().Any(), "Markdown 表格渲染成了 FlowDocument 的 Table 块");
            c.That(document.Blocks.OfType<System.Windows.Documents.List>().Any(), "Markdown 列表渲染成了 List 块");
        }

        var itemsControls = FindVisuals<ItemsControl>(view).ToList();
        c.That(itemsControls.Any(ic => ic.Items.Count == 2 && ReferenceEquals(ic.ItemsSource, turn.Steps)),
            "工具步骤区绑定了 2 条步骤");

        // 确认卡片：PendingLines 是"每次求值都新建的列表"，不能按引用比较——按内容 + 按钮可见性断言
        c.That(itemsControls.Any(ic => ic.ItemsSource is IEnumerable<string> lines && lines.Any(line => line.Contains("吉他"))),
            "确认卡片列出了待确认动作（含参数摘要）");
        var applyText = Application.Current?.TryFindResource("Assistant_Chat_Confirm_Apply") as string ?? "应用";
        var applyButton = FindVisuals<System.Windows.Controls.Primitives.ButtonBase>(view)
            .FirstOrDefault(button => button.Content is string content && content == applyText);
        if (applyButton is null)
        {
            var buttons = FindVisuals<System.Windows.Controls.Primitives.ButtonBase>(view)
                .Select(b => b.Content switch { string s => s, null => "<null>", _ => $"<{b.Content.GetType().Name}>" })
                .ToList();
            var transcripts = FindVisuals<ItemsControl>(view)
                .Where(ic => ic.ItemsSource is System.Collections.IEnumerable)
                .Select(ic => $"{ic.Items.Count} 项/{ic.ItemsSource?.GetType().Name}")
                .ToList();
            Console.WriteLine($"    [诊断] IsConfigured={viewModel.IsConfigured}，HasSession={viewModel.HasSession}，会话数={viewModel.Sessions.Count}");
            Console.WriteLine($"    [诊断] SelectedSession 记录数={viewModel.SelectedSession?.Turns.Count}，turn.HasPendingConfirmation={turn.HasPendingConfirmation}");
            Console.WriteLine($"    [诊断] 视图内 ButtonBase 共 {buttons.Count} 个：{string.Join(" | ", buttons.Take(12))}");
            Console.WriteLine($"    [诊断] 视图内 ItemsControl（有 ItemsSource）共 {transcripts.Count} 个：{string.Join(" | ", transcripts.Take(12))}");
            Console.WriteLine($"    [诊断] 期望按钮文案「{applyText}」");
        }

        c.That(applyButton is not null, $"确认卡片有「{applyText}」按钮");
        if (applyButton?.IsVisible != true)
        {
            Console.WriteLine($"    [诊断] turn.HasPendingConfirmation={turn.HasPendingConfirmation}，IsPending={turn.IsPending}，Success={turn.Success}");
            Console.WriteLine("    [诊断] 「应用」按钮的祖先链（类型 / Visibility / IsVisible）：");
            for (DependencyObject? node = applyButton; node is not null; node = VisualTreeHelper.GetParent(node))
            {
                var visibility = node is UIElement element ? element.Visibility.ToString() : "-";
                var visible = node is UIElement ui ? ui.IsVisible.ToString() : "-";
                Console.WriteLine($"      {node.GetType().Name} / {visibility} / {visible}");
            }
        }

        c.That(applyButton?.IsVisible == true || !viewModel.IsConfigured,
            viewModel.IsConfigured
                ? "确认卡片按钮可见（待确认时整卡片应展开）"
                : "端点未配置 → 对话区按设计收起（跳过可见性断言，改由已配置环境覆盖）");
        c.That(FindVisuals<System.Windows.Controls.Primitives.ButtonBase>(view)
                .Any(button => button.Content is string content && content == (Application.Current?.TryFindResource("Assistant_Chat_Confirm_Reject") as string ?? "拒绝")),
            "确认卡片有「拒绝」按钮");

        var checkBoxes = FindVisuals<CheckBox>(view).ToList();
        c.That(checkBoxes.Count >= 1, "上下文开关存在（CheckBox）");

        // 阶段 132：同一对话默认带上完整历史（含工具往返）；记忆只存在于单个对话
        c.That(new AiAssistantSession().UseContext, "新建对话默认带上下文（UseContext 默认 true）");
        c.Equal(AiAssistantSettingService.DefaultMaxContextChars, 200000, "上下文预算默认 200000 字符");

        // 「弹窗」按钮在主窗口的对话页里必须可见（只在独立窗口里才隐藏，见下面的窗口核验）
        var pagePopButton = FindVisuals<System.Windows.Controls.Primitives.ButtonBase>(view)
            .FirstOrDefault(button => button is FrameworkElement { Name: "PopWindowButton" });
        c.That(pagePopButton is not null && pagePopButton.Visibility == Visibility.Visible,
            "主窗口里的对话页显示「弹窗」按钮（可把它弹出成独立窗口）");

        // 阶段 127：**外部入口（主页 banner 快捷输入）必须新开对话**，不续在上一轮里。
        // 这里只断言"目标会话"这一层（BeginExternalConversation）——再往下就是真发请求了，不适合离线探针。
        var externalVm = new AiAssistantViewModel();
        externalVm.NewSession();
        var previousSession = externalVm.SelectedSession;
        var freshSession = externalVm.BeginExternalConversation();
        c.That(previousSession is not null, "准备：先有一个对话（模拟上一次对话）");
        c.That(!ReferenceEquals(freshSession, previousSession) && ReferenceEquals(externalVm.SelectedSession, freshSession),
            "外部入口会**新开**一个对话并选中它（不是续在上一轮）");
        c.That(externalVm.Sessions.Count >= 2 && ReferenceEquals(externalVm.Sessions[0], freshSession),
            "新对话插在列表最前（与「新建对话」一致）");
        c.That(previousSession is not null && previousSession.Turns.Count == 0
               && ReferenceEquals(externalVm.SelectedSession, freshSession),
            "后续提问会落到新对话里（SendAsync 用的就是 SelectedSession）");

        externalVm.RefreshConfiguration();
        if (!externalVm.IsConfigured)
        {
            var countBefore = externalVm.Sessions.Count;
            externalVm.SubmitExternalQuery("探针：未配置端点时不应留下空对话");
            c.That(externalVm.Sessions.Count == countBefore, "端点未配置时外部提问不会留下一个空对话");
        }
        else
        {
            Console.WriteLine("    [诊断] 本机已配置端点 → 跳过「未配置不留空对话」这条（避免真发请求）");
        }

        Pump();
        c.That(bindingErrors.Count == 0, $"WPF 绑定错误 0 条（实际 {bindingErrors.Count}）");
        foreach (var error in bindingErrors.Take(8))
        {
            Console.WriteLine($"    [绑定] {error}");
        }

        window.Close();
        PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);

        // 主窗口核验放在**最后**：它会把自己变成 Application.MainWindow，
        // 提前做会干扰上面这些"用 App.MainWindow 的界面"断言（本轮实测：后面全部查不到控件）。
        c.Section("UiSuite · 主窗口导航（助手是常驻入口）");
        MainWindowNavigation(c);

        c.Section("UiSuite · AI 显示位置（主窗口 vs 独立窗口）");
        AssistantWindowHosting(c);
    }

    // ============================ 基础设施 ============================
    /// <summary>
    /// 阶段 128：设置 → AI → 行为 →「在主窗口中显示 AI」**默认不勾**，不勾时所有入口都开独立窗口。
    /// 这里断言三件事：① 默认值；② 路由真值表（纯函数，不碰用户 settings.json）；③ 独立窗口会被复用。
    /// </summary>
    private static void AssistantWindowHosting(Check c)
    {
        c.Equal(AiAssistantSettingService.DefaultShowInMainWindow, false, "默认**不**在主窗口显示 AI");

        c.That(AssistantEntryRouter.ShouldOpenWindow(false, typeof(AiAssistantPage)),
            "不勾 + 助手页 → 用独立窗口");
        c.That(!AssistantEntryRouter.ShouldOpenWindow(true, typeof(AiAssistantPage)),
            "勾上 + 助手页 → 走主窗口页面");
        c.That(!AssistantEntryRouter.ShouldOpenWindow(false, typeof(SettingsPage)),
            "不勾 + 别的页 → 照常在主窗口里导航（只影响助手页）");

        // 独立窗口：开一个离屏锚点窗口当宿主，窗口会被居中到它上面（所以不会闪到用户屏幕上）
        Window? anchor = null;
        try
        {
            anchor = new Window
            {
                Width = 900,
                Height = 700,
                Left = -4000,
                Top = -4000,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Content = new StackPanel(),
            };
            anchor.Show();
            Pump();

            // 阶段 128：窗口可能**已经被更早的套件打开过**（窗口模式下的转助手会开它），
            // 所以这里比"实例是否复用"，不比"窗口总数有没有 +1"——后者依赖进入本套件时的状态。
            var wasOpen = AssistantWindow.IsOpen;
            var previousInstance = AssistantWindow.CurrentWindow;
            AssistantWindow.Show(anchor);
            Pump();
            c.That(AssistantWindow.IsOpen, "窗口模式：Show 之后存在独立窗口");
            c.That(!wasOpen || ReferenceEquals(previousInstance, AssistantWindow.CurrentWindow),
                "已开则复用同一个窗口实例");

            var firstInstance = AssistantWindow.CurrentWindow;
            firstInstance?.UpdateLayout();
            Pump();

            // 「弹窗」按钮的显示规则用纯函数断言（离屏 ToolWindow 的内容不进可视树，与 §9-101 同类限制）；
            // "主窗口里的对话页显示它"那条是上面用真视图查过的。
            c.That(!AiAssistantChatView.ShouldShowPopWindowButton(firstInstance, firstInstance),
                "对话视图被 AI 独立窗口承载时，隐藏「弹窗」按钮（点它只会关掉自己）");
            c.That(AiAssistantChatView.ShouldShowPopWindowButton(anchor, firstInstance),
                "对话视图在主窗口里时，显示「弹窗」按钮");
            c.That(AiAssistantChatView.ShouldShowPopWindowButton(null, firstInstance),
                "宿主未知时（视图尚未挂上窗口）按显示处理，不会误藏");

            AssistantWindow.Show(anchor);
            Pump();
            c.That(AssistantWindow.IsOpen && ReferenceEquals(firstInstance, AssistantWindow.CurrentWindow),
                "重复 Show 复用同一个窗口（不会再开一个、也不会把它关掉）");

            AssistantWindow.Close();
            c.That(!AssistantWindow.IsOpen
                   && !Application.Current.Windows.Cast<Window>().Contains(firstInstance!),
                "Close 之后窗口状态复位（下一次 Show 会新开一个）");
        }
        catch (Exception ex)
        {
            // §9 第 99 条：外层异常只说"在 TypeConverterMarkupExtension 上提供值时引发了异常"，
            // 真因在内层（本轮就是 ToolWindow.xaml 的 Icon 少了程序集名）——整条链打出来
            var chain = new List<string>();
            for (var e = ex; e is not null; e = e.InnerException)
            {
                chain.Add($"{e.GetType().Name}: {e.Message}");
            }

            c.That(false, $"独立窗口核验抛异常：{string.Join(" ← ", chain)}");
        }
        finally
        {
            AssistantWindow.Close();
            anchor?.Close();
        }
    }

    /// <summary>
    /// 阶段 126：AI 助手是**常驻入口**，不是普通功能项——它必须在导航页脚（与「设置」同级、且排在设置之前），
    /// 并且标题栏还有一个无论如何都点得到的按钮。真构造主窗口来核验（离屏）。
    /// </summary>
    private static void MainWindowNavigation(Check c)
    {
        MainWindow? main = null;
        try
        {
            main = new MainWindow
            {
                Left = -4000,
                Top = -4000,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
            };
            main.Show();
            main.UpdateLayout();
            Pump();

            var navigationView = FindVisuals<Wpf.Ui.Controls.NavigationView>(main).FirstOrDefault();
            if (navigationView is null)
            {
                c.That(false, "主窗口里找到 NavigationView");
                return;
            }

            c.That(navigationView is not null, "主窗口里找到 NavigationView");

            var footerTypes = EnumerateNavigationItems(navigationView.FooterMenuItems.Cast<object>())
                .Select(item => item.TargetPageType)
                .ToList();
            var menuTypes = EnumerateNavigationItems(navigationView.MenuItems.Cast<object>())
                .Select(item => item.TargetPageType)
                .ToList();

            c.That(footerTypes.Contains(typeof(AiAssistantPage)),
                $"助手在**页脚**（页脚页类型：{string.Join(" / ", footerTypes.Select(t => t?.Name ?? "<null>"))}）");
            c.That(!menuTypes.Contains(typeof(AiAssistantPage)),
                "助手**不在**会滚动的顶部菜单里");
            c.That(footerTypes.Count >= 2 && footerTypes[0] == typeof(AiAssistantPage)
                   && footerTypes.Contains(typeof(SettingsPage)),
                "页脚顺序：助手在设置之上（高频入口靠上、设置留最底）");

            // 标题栏按钮已按用户要求去掉（阶段 126 收尾）：只保留页脚这一个常驻入口
            c.That(!FindVisuals<System.Windows.Controls.Primitives.ButtonBase>(main)
                    .Any(button => button is FrameworkElement { Name: "AssistantEntryButton" }),
                "标题栏没有助手按钮（只保留页脚入口）");
        }
        catch (Exception ex)
        {
            // §9 第 99 条：外层异常只会说"在 TypeConverterMarkupExtension 上提供值时引发了异常"，
            // 真正原因在内层——这里把整条链打出来，别让人再猜一遍
            var chain = new List<string>();
            for (var e = ex; e is not null; e = e.InnerException)
            {
                chain.Add($"{e.GetType().Name}: {e.Message}");
            }

            c.That(false, $"主窗口导航核验抛异常：{string.Join(" ← ", chain)}");
        }
        finally
        {
            main?.Close();
        }
    }

    /// <summary>递归枚举导航项（NavigationViewItem 可以嵌套子项）。</summary>
    private static IEnumerable<Wpf.Ui.Controls.NavigationViewItem> EnumerateNavigationItems(IEnumerable<object> items)
    {
        foreach (var item in items)
        {
            if (item is not Wpf.Ui.Controls.NavigationViewItem navigationItem)
            {
                continue;
            }

            yield return navigationItem;
            foreach (var child in EnumerateNavigationItems(navigationItem.MenuItems.Cast<object>()))
            {
                yield return child;
            }
        }
    }

    /// <summary>把 Dispatcher 队列推着跑一段（离屏渲染靠它完成布局与绑定求值）。</summary>
    private static void Pump() => ProbeApplication.Pump();

    private static IEnumerable<T> FindVisuals<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in FindVisuals<T>(child))
            {
                yield return nested;
            }
        }
    }

    /// <summary>收集 WPF 绑定错误（含 Warning 级）。</summary>
    private sealed class CollectingTraceListener : TraceListener
    {
        private readonly List<string> _sink;

        public CollectingTraceListener(List<string> sink) => _sink = sink;

        public override void Write(string? message)
        {
            // 绑定错误是逐段写出的：Write 收片段、WriteLine 收行尾，这里只在 WriteLine 里归集
        }

        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _sink.Add(message);
            }
        }
    }
}
