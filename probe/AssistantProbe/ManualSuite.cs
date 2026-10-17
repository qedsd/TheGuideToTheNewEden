using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Assistant;
using TheGuideToTheNewEden.WPF.Services.Home;
using TheGuideToTheNewEden.WPF.Services.Manual;
using TheGuideToTheNewEden.WPF.Views.Pages.Settings;
using TheGuideToTheNewEden.WPF.Views.UserControls;

namespace TheGuideToTheNewEden.Probe;

/// <summary>
/// 阶段 131：内置说明书（<c>Resources/Manuals/&lt;lang&gt;/*.md</c>）的**结构、服务与 AI 接线**。
/// <para>
/// 设计口径见 <c>MANUAL_DESIGN.md</c> §8.1：结构层全部机械断言，事实（这段话说得对不对）只能人工评审。
/// 本套件管的是前一半——front-matter 完整性、固定章节与 slug、体量预算、功能覆盖率（功能目录 ↔ 手册）、
/// 工具双向覆盖（每篇声明的工具真实存在 / 每个产品工具都有手册讲它）、检索与分片读取的纯函数行为、
/// 失败回填的关键词基线、以及两个元工具的 token 文体契约。
/// </para>
/// <para>
/// 目录通过 <see cref="ManualService.DirectoryOverride"/> **直接指向仓库源目录**：探针输出目录里
/// 未必带着随包资源（重定向 <c>OutDir</c> 时肯定没有），指向源目录既稳定又顺便校验了"真正要发布的那些文件"。
/// </para>
/// <para>纯离线：不发请求、不写盘（只读文件 + 内存索引）。</para>
/// </summary>
internal static class ManualSuite
{
    /// <summary>章节固定顺序（MANUAL_DESIGN §3.3）：可跳过可选节，不允许顺序外的 H2。</summary>
    private static readonly string[] FixedSectionOrder =
        ["tldr", "where", "prereq", "steps", "fields", "ai", "errors", "limits", "related"];

    /// <summary>必填节。</summary>
    private static readonly string[] RequiredSections = ["tldr", "where", "steps", "ai", "limits"];

    /// <summary>单篇上限（设计预算 9,000 字符）。</summary>
    private const int MaxDocumentChars = 9000;

    /// <summary><c>steps</c> 节上限（设计预算 2,500）。</summary>
    private const int MaxStepsChars = 2500;

    /// <summary>
    /// 其它节上限。设计写的是 1,600，实测 <c>errors</c>（报错对照表）与 <c>fields</c>（口径表）超了；
    /// 与其砍掉真正有用的表格，不如把预算定为实测基线（当前最大 2,187）——超过 1,800 的节由
    /// <see cref="ManualService.ReadSection"/> 分片返回，模型按 part 续读，体验有兜底。
    /// </summary>
    private const int MaxOtherSectionChars = 2200;

    /// <summary>说明书自身的元工具：不隶属任何功能手册（反向覆盖断言里豁免）。</summary>
    private static readonly string[] MetaToolWhitelist = ["manual_search", "manual_read"];

    /// <summary>设置分类页（设置壳页的 11 个分类；用于断言"每个分类都有手册"）。</summary>
    private static readonly Type[] SettingCategoryPages =
    [
        typeof(GeneralSettingPage), typeof(HomeSettingPage), typeof(GameLogSettingPage),
        typeof(MarketSettingPage), typeof(StructuresSettingPage), typeof(ESIScopeSettingPage),
        typeof(AiAssistantSettingPage), typeof(GlossarySettingPage), typeof(KeyboardListPage),
        typeof(TestSettingPage), typeof(UpdateSettingPage),
    ];

    private static readonly Regex IdRegex = new(@"^[a-z0-9][a-z0-9-]*(/[a-z0-9-]+)*$", RegexOptions.Compiled);
    private static readonly Regex SlugRegex = new(@"^[a-z0-9][a-z0-9-]*$", RegexOptions.Compiled);
    private static readonly Regex DateRegex = new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);
    private static readonly Regex AsciiRegex = new(@"^[\x20-\x7E]+$", RegexOptions.Compiled);
    private static readonly Regex CjkRegex = new(@"[\u4e00-\u9fff]", RegexOptions.Compiled);

    public static void Run(Check c)
    {
        // 语言字典与 App 级资源（Application 一个进程只能有一个；UiSuite 会复用这里建的那个）
        ProbeCore.Ensure();
        ProbeApplication.Ensure();

        c.Section("ManualSuite · 目录与索引");

        var projectRoot = FindWpfProjectRoot();
        if (projectRoot is null)
        {
            c.That(false, "找不到 TheGuideToTheNewEden.WPF 项目目录（无法定位说明书源文件）");
            return;
        }

        var manualsRoot = Path.Combine(projectRoot, "Resources", "Manuals");
        c.That(Directory.Exists(manualsRoot), $"说明书根目录存在：{manualsRoot}");
        if (!Directory.Exists(manualsRoot))
        {
            return;
        }

        ManualService.DirectoryOverride = manualsRoot;
        ManualService.Reload();

        var documents = ManualService.All;
        c.That(documents.Count >= 50, $"索引到 {documents.Count} 篇说明书（≥50）");
        if (documents.Count == 0)
        {
            return;
        }

        c.That(ManualService.IsAvailable(), "ManualService.IsAvailable() = true");

        var language = LanguageService.Value;
        var hasOwnLanguage = Directory.Exists(Path.Combine(manualsRoot, language));
        c.That(ManualService.IsFallback == !hasOwnLanguage,
            $"语言回退状态与实际目录一致（界面语言 {language} 有目录={hasOwnLanguage} → IsFallback={ManualService.IsFallback}）");
        c.That(ManualService.SourceLanguage.Length > 0, $"SourceLanguage = {ManualService.SourceLanguage}");

        // ① 每篇：id/键齐全、章节规范、体量预算
        var idProblems = new List<string>();
        var metaProblems = new List<string>();
        var sectionProblems = new List<string>();
        var sizeProblems = new List<string>();
        var aliasProblems = new List<string>();
        var bodyProblems = new List<string>();

        foreach (var document in documents)
        {
            if (!IdRegex.IsMatch(document.Id))
            {
                idProblems.Add(document.Id);
            }

            if (document.Title.Length == 0 || document.Group.Length == 0 || document.Updated is null)
            {
                metaProblems.Add($"{document.Id}（title={document.Title.Length} / group={document.Group.Length} / updated={document.Updated ?? "null"}）");
            }
            else if (!DateRegex.IsMatch(document.Updated))
            {
                metaProblems.Add($"{document.Id}（updated={document.Updated} 不是 yyyy-MM-dd）");
            }

            // 节：全部有 slug、slug 合法且唯一、必填齐全、顺序在固定表内单调
            var sections = document.Sections;
            var slugs = sections.Select(section => section.Slug).ToList();
            if (slugs.Any(slug => slug.Length == 0) || slugs.Any(slug => !SlugRegex.IsMatch(slug)))
            {
                sectionProblems.Add($"{document.Id}（有无锚点或非法 slug 的 H2）");
            }
            else if (slugs.Distinct(StringComparer.Ordinal).Count() != slugs.Count)
            {
                sectionProblems.Add($"{document.Id}（slug 重复）");
            }
            else
            {
                var missing = RequiredSections.Where(required => !slugs.Contains(required, StringComparer.Ordinal)).ToList();
                if (missing.Count > 0)
                {
                    sectionProblems.Add($"{document.Id}（缺少必填节 {string.Join("/", missing)}）");
                }

                var order = slugs.Select(slug => Array.IndexOf(FixedSectionOrder, slug)).ToList();
                if (order.Any(index => index < 0) || !order.SequenceEqual(order.OrderBy(index => index)))
                {
                    sectionProblems.Add($"{document.Id}（章节顺序不符：{string.Join(" → ", slugs)}）");
                }
            }

            if (document.Body.Length > MaxDocumentChars)
            {
                sizeProblems.Add($"{document.Id} 全文 {document.Body.Length} > {MaxDocumentChars}");
            }

            foreach (var section in sections)
            {
                var cap = section.Slug == "steps" ? MaxStepsChars : MaxOtherSectionChars;
                if (section.Content.Length > cap)
                {
                    sizeProblems.Add($"{document.Id}#{section.Slug} {section.Content.Length} > {cap}");
                }
            }

            if (document.Aliases.Count < 4
                || !document.Aliases.Any(alias => AsciiRegex.IsMatch(alias))
                || !document.Aliases.Any(alias => CjkRegex.IsMatch(alias)))
            {
                aliasProblems.Add($"{document.Id}（{document.Aliases.Count} 个：{string.Join(", ", document.Aliases)}）");
            }

            // 渲染输入里不该再有 {#slug}（渲染器不认识它，会原样显示给用户）
            if (document.Body.Contains("{#", StringComparison.Ordinal))
            {
                bodyProblems.Add(document.Id);
            }
        }

        c.That(idProblems.Count == 0, $"每篇 id 都是合法相对路径（问题 {idProblems.Count}）");
        Print(idProblems);
        c.That(metaProblems.Count == 0, $"每篇 front-matter 的 title/group/updated 齐全且日期合法（问题 {metaProblems.Count}）");
        Print(metaProblems);
        c.That(sectionProblems.Count == 0, $"每篇章节都合规（有 slug、唯一、必填齐全、顺序正确；问题 {sectionProblems.Count}）");
        Print(sectionProblems);
        c.That(sizeProblems.Count == 0, $"体量预算内（全文 ≤{MaxDocumentChars}；steps ≤{MaxStepsChars}；其它节 ≤{MaxOtherSectionChars}；问题 {sizeProblems.Count}）");
        Print(sizeProblems);
        c.That(aliasProblems.Count == 0, $"每篇别名 ≥4 且中英混排（问题 {aliasProblems.Count}）");
        Print(aliasProblems);
        c.That(bodyProblems.Count == 0, $"渲染正文里没有残留的 {{#slug}} 锚点（问题 {bodyProblems.Count}）");
        Print(bodyProblems);

        // ② group 键在两份语言字典里都存在
        var zh = UiSuite.LoadLanguageDictionary("zh-CN.xaml");
        var en = UiSuite.LoadLanguageDictionary("en-US.xaml");
        if (zh is not null && en is not null)
        {
            var zhKeys = zh.Keys.Cast<object>().Select(key => key.ToString()!).ToHashSet(StringComparer.Ordinal);
            var enKeys = en.Keys.Cast<object>().Select(key => key.ToString()!).ToHashSet(StringComparer.Ordinal);
            var missingGroups = documents
                .Where(document => document.Group.Length == 0 || !zhKeys.Contains(document.Group) || !enKeys.Contains(document.Group))
                .Select(document => $"{document.Id} → {document.Group}")
                .Distinct(StringComparer.Ordinal)
                .ToList();
            c.That(missingGroups.Count == 0, $"每篇的 group 键在 zh/en 字典里都存在（问题 {missingGroups.Count}）");
            Print(missingGroups);

            // 手册窗口/入口新增的语言键
            var manualKeys = new[] { "Manual.Window.Title", "Manual.Search.Placeholder", "Manual.Search.Empty", "Manual.NotFound", "Manual.ShowAll", "Manual.FallbackToZh", "Nav.Manual" };
            var missingKeys = manualKeys.Where(key => !zhKeys.Contains(key) || !enKeys.Contains(key)).ToList();
            c.That(missingKeys.Count == 0, $"手册相关的 {manualKeys.Length} 个语言键中英齐备（缺 {missingKeys.Count}）");
            Print(missingKeys);
        }
        else
        {
            c.That(false, "语言字典加载失败（group 键校验与手册键校验无法进行）");
        }

        // ③ page 指向的文件存在（横切文档没有 page）
        var pageProblems = new List<string>();
        foreach (var document in documents)
        {
            if (document.Page is not { Length: > 0 } page)
            {
                continue;
            }

            foreach (var part in page.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var file = Path.Combine(projectRoot, part.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(file))
                {
                    pageProblems.Add($"{document.Id} → {part}");
                }
            }
        }

        c.That(pageProblems.Count == 0, $"每篇 page 指向的 XAML 都存在（问题 {pageProblems.Count}）");
        Print(pageProblems);

        // ④ children 双向一致：父声明的子必须存在
        var childProblems = new List<string>();
        foreach (var document in documents)
        {
            foreach (var child in document.Children)
            {
                if (ManualService.Find(child) is null)
                {
                    childProblems.Add($"{document.Id} → {child}");
                }
            }
        }

        c.That(childProblems.Count == 0, $"children 声明的子手册都存在（问题 {childProblems.Count}）");
        Print(childProblems);

        // ⑤ 功能目录覆盖：每个「页面 / 工具窗」条目都要能经 ManualToolMap 找到手册
        var coverageGaps = new List<string>();
        var actionFeatures = new List<string>();
        foreach (var feature in HomeFeatureCatalog.Features)
        {
            if (feature.Kind == HomeFeatureKind.Action || feature.PageType is null)
            {
                actionFeatures.Add(feature.Id);
                continue;
            }

            if (ManualToolMap.TryGetManual(feature.PageType) is null)
            {
                coverageGaps.Add($"{feature.Id}（{feature.PageType.Name}）");
            }
        }

        c.That(coverageGaps.Count == 0, $"功能目录的页面/工具窗条目都有手册（缺口 {coverageGaps.Count}）");
        Print(coverageGaps);
        Console.WriteLine($"    [信息] 无页面的动作条目（不参与硬断言）：{string.Join(", ", actionFeatures)}");

        var settingGaps = SettingCategoryPages
            .Where(pageType => ManualToolMap.TryGetManual(pageType) is null)
            .Select(pageType => pageType.Name)
            .ToList();
        c.That(settingGaps.Count == 0, $"设置壳页的 {SettingCategoryPages.Length} 个分类都有手册（缺口 {settingGaps.Count}）");
        Print(settingGaps);

        // ManualToolMap 的可达性已由上面两条覆盖断言保证（TryGetManual 对"手册不存在"返回 null）
        c.That(ManualToolMap.TryGetDefault() is not null, "兜底手册 getting-started 存在");

        // ⑥ 工具双向覆盖
        var declared = documents.SelectMany(document => document.Tools).ToHashSet(StringComparer.Ordinal);
        var unknownTools = declared.Where(tool => ToolRegistry.Find(tool) is null).OrderBy(tool => tool, StringComparer.Ordinal).ToList();
        c.That(unknownTools.Count == 0, $"手册声明的工具名都真实存在（问题 {unknownTools.Count}）");
        Print(unknownTools);

        var uncovered = ToolRegistry.Product
            .Where(tool => !declared.Contains(tool.Name) && !MetaToolWhitelist.Contains(tool.Name, StringComparer.Ordinal))
            .Select(tool => tool.Name)
            .ToList();
        c.That(uncovered.Count == 0, $"每个产品工具都至少被一篇手册讲到（未覆盖 {uncovered.Count}）");
        Print(uncovered);

        // ⑦ 两个元工具：注册 + 核心常驻 + token 文体契约
        c.That(ToolRegistry.Find("manual_search") is not null && ToolRegistry.Find("manual_read") is not null, "manual_search / manual_read 已注册");
        c.That(ToolRegistry.CoreToolNames.Contains("manual_search") && ToolRegistry.CoreToolNames.Contains("manual_read"),
            "两个元工具在 CoreToolNames 里（任何套餐路由都声明，问用法一定有手册）");

        foreach (var name in MetaToolWhitelist)
        {
            var tool = ToolRegistry.Find(name);
            if (tool is null)
            {
                continue;
            }

            c.That((tool.Description ?? string.Empty).Length <= 140,
                $"{name} 描述 {tool.Description?.Length ?? 0} ≤ 140 字符（TokenSuite 同一契约）");

            var paramProblems = new List<string>();
            foreach (var (parameter, description) in ParameterDescriptions(tool.Parameters))
            {
                if (description.Length > 24)
                {
                    paramProblems.Add($"{parameter}（{description.Length}）");
                }
            }

            c.That(paramProblems.Count == 0, $"{name} 参数描述都 ≤24 字符（问题 {paramProblems.Count}）");
            Print(paramProblems);
        }

        // ⑧ 检索纯函数
        var first = ManualService.Search("市场 订单");
        var second = ManualService.Search("市场 订单");
        c.That(first.Select(hit => hit.Id).SequenceEqual(second.Select(hit => hit.Id)), "同输入两次检索结果完全一致（纯函数）");
        c.That(ManualService.Search(string.Empty).Count == 0, "空检索词返回空集（目录由 BuildIndex 提供）");
        c.That(ManualService.BuildIndex().Contains("market", StringComparison.Ordinal), "BuildIndex() 含全部手册目录（market 在列）");

        var limited = ManualService.Search("订单", 3);
        c.That(limited.Count <= 3, $"limit 生效（要 3 条，得到 {limited.Count}）");
        c.That(limited.SequenceEqual(limited.OrderByDescending(hit => hit.Score)), "结果按分数降序");

        var scalperHits = ManualService.Search("倒货");
        c.That(scalperHits.Count > 0 && scalperHits[0].Id.StartsWith("scalper", StringComparison.Ordinal),
            $"「倒货」命中 scalper 系（首条 {scalperHits.FirstOrDefault()?.Id ?? "无"}）");

        var mailHits = ManualService.Search("邮件 未读");
        c.That(mailHits.Any(hit => hit.Id == "characters/mail"), "「邮件 未读」命中 characters/mail");

        // ⑨ Find 的宽松匹配
        c.That(ManualService.Find("characters/mail")?.Id == "characters/mail", "Find 精确 id");
        c.That(ManualService.Find("characters_mail")?.Id == "characters/mail", "Find 容忍 _ 与 - 互换");
        c.That(ManualService.Find("manual://characters/mail#errors")?.Id == "characters/mail", "Find 容忍 manual:// 前缀与 #锚点");
        c.That(ManualService.Find("characters/mail#errors")?.Id == "characters/mail", "Find 容忍 #锚点");
        c.That(ManualService.Find("market")?.Id == "market", "Find 顶层 id");
        c.That(ManualService.Find("不存在的说明书") is null, "Find 不存在的 id 返回 null");

        // ⑩ 概览与分片读取
        if (ManualService.Find("market") is { } market)
        {
            var overview = ManualService.BuildOverview(market);
            var tldr = market.FindSection("tldr");
            c.That(tldr is not null && overview.Contains(tldr.Content, StringComparison.Ordinal), "概览含 tldr 正文");
            c.That(market.Sections.Where(section => section.Slug.Length > 0)
                    .All(section => overview.Contains(section.Slug, StringComparison.Ordinal)),
                "概览的章节目录覆盖全部有锚点的节");
            c.That(!overview.Contains("{#", StringComparison.Ordinal), "概览里没有残留锚点");
        }
        else
        {
            c.That(false, "找不到 market 手册（概览断言无法进行）");
        }

        var chunkProblems = new List<string>();
        var oversizedChunks = new List<string>();
        var multiPartSections = 0;
        var totalSections = 0;
        foreach (var document in documents)
        {
            foreach (var section in document.Sections.Where(section => section.Slug.Length > 0))
            {
                totalSections++;
                var chunk = ManualService.ReadSection(document, section.Slug, 1);
                if (chunk is null)
                {
                    chunkProblems.Add($"{document.Id}#{section.Slug} 读不到");
                    continue;
                }

                var joined = string.Join("\n", Enumerable.Range(1, chunk.TotalParts)
                    .Select(part => ManualService.ReadSection(document, section.Slug, part)!.Text));
                if (!string.Equals(joined, section.Content, StringComparison.Ordinal))
                {
                    chunkProblems.Add($"{document.Id}#{section.Slug} 分片拼回不等于原文");
                }

                if (chunk.TotalParts > 1)
                {
                    multiPartSections++;

                    // 分片是"按行"切的：正常一片 ≤ MaxSectionChars；只有单行本身超长时才会更大。
                    var longestLine = section.Content.Split('\n').Max(line => line.Length);
                    var cap = Math.Max(ManualService.MaxSectionChars, longestLine);
                    foreach (var part in Enumerable.Range(1, chunk.TotalParts))
                    {
                        var text = ManualService.ReadSection(document, section.Slug, part)!.Text;
                        if (text.Length > cap)
                        {
                            oversizedChunks.Add($"{document.Id}#{section.Slug} 第 {part} 片 {text.Length} 字符");
                        }
                    }
                }
            }
        }

        c.That(chunkProblems.Count == 0, $"全部 {totalSections} 个节的分片读取无损（问题 {chunkProblems.Count}）");
        Print(chunkProblems);
        c.That(oversizedChunks.Count == 0, $"分片不超过 {ManualService.MaxSectionChars} 字符（或该节单行长度；问题 {oversizedChunks.Count}）");
        Print(oversizedChunks);
        Console.WriteLine($"    [信息] 需要分片读取的节：{multiPartSections} 个（单节 > {ManualService.MaxSectionChars} 字符）");

        if (ManualService.Find("characters/mail") is { } mail)
        {
            var paged = ManualService.ReadSection(mail, "steps", 1);
            c.That(paged is not null, "ReadSection 能读指定节");
            c.That(ManualService.ReadSection(mail, "steps", 999)?.Part == paged!.TotalParts, "part 越界钳到最后一片");
            c.That(ManualService.ReadSection(mail, "no-such-section", 1) is null, "未知 slug 返回 null");
        }

        // ⑪ 失败回填的启发式基线
        c.That(ManualToolMap.FailureKeywords.Length >= 3, $"失败关键词基线 {ManualToolMap.FailureKeywords.Length} 个");
        c.That(ManualToolMap.LooksLikeFailure("获取订单失败：建筑市场需要该角色有市场访问权"), "「失败」语义被识别");
        c.That(ManualToolMap.LooksLikeFailure("缺少 esi-mail.read_mail.v1 权限，需要授权后重试"), "「权限 / 授权」语义被识别");
        c.That(!ManualToolMap.LooksLikeFailure("查不到物品 645 的历史价格"), "普通的查不到不算失败（不浪费 token）");
        c.That(ManualToolMap.TryGetManualForTool("market_orders") is { Length: > 0 }, "market_orders → 有对应手册");
        c.That(ManualToolMap.TryGetManualForTool("manual_search") is null, "元工具不做回填（无对应功能手册）");
        c.That(ManualToolMap.WithManualHint("manual_search", "文本") == "文本", "元工具原样返回");
        c.That(
            ManualToolMap.TryGetManualForTool("market_orders") is { } manualId
            && ManualToolMap.WithManualHint("market_orders", "失败").Contains($"manual://{manualId}#errors", StringComparison.Ordinal),
            "失败回填带 manual://<id>#errors 链接");

        // ⑫ 界面构造：XAML 解析、图标名、语言键这三类问题**只在运行时炸**（编译期 0 错误），
        // 所以离屏构造一次视图（不 Show，不触发 Loaded 的数据装载）。
        c.Section("ManualSuite · 界面构造（只在运行时才会炸的那几类）");
        try
        {
            var view = new ManualBrowserView();
            c.That(view is not null, "ManualBrowserView 构造成功（XAML / 图标名 / 静态资源 / 手册工具条语言键）");

            // F1 首次打开窗口时，`Show(id)` 可能在 Loaded（→ RefreshList）之前就跳转：
            // 这里不 Show 直接跳，验证"列表还没填也能定位到目标手册"的兜底。
            view.NavigateTo("characters/mail");
            c.Equal(view.CurrentManualId, "characters/mail", "未 Show 就跳转也能定位（窗口刚建、列表未填充时的兜底）");

            // AI 回答里的链接写法：manual://<id>#<节> 同样要落到正确的手册上
            view.NavigateTo("manual://market#errors");
            c.Equal(view.CurrentManualId, "market", "manual://market#errors 链接落到 market");
        }
        catch (Exception ex)
        {
            c.That(false, $"ManualBrowserView 构造/跳转抛异常（多半是缺失 StaticResource 或图标名非法）：{ex.Message}");
        }
    }

    private static void Print(IEnumerable<string> problems)
    {
        foreach (var problem in problems.Take(20))
        {
            Console.WriteLine($"    [诊断] {problem}");
        }
    }

    private static IEnumerable<(string Name, string Description)> ParameterDescriptions(JsonObject? parameters)
    {
        if (parameters?["properties"] is not JsonObject properties)
        {
            yield break;
        }

        foreach (var pair in properties)
        {
            if (pair.Value is JsonObject spec && spec["description"]?.GetValue<string>() is { } text)
            {
                yield return (pair.Key, text);
            }
        }
    }

    /// <summary>
    /// 定位 <c>TheGuideToTheNewEden.WPF</c> 项目目录：先按**编译期源文件路径**反推
    /// （探针永远在 <c>&lt;repo&gt;\probe\AssistantProbe</c> 下），再退化成从运行目录向上找。
    /// </summary>
    private static string? FindWpfProjectRoot()
    {
        var seeds = new List<string>
        {
            Path.GetDirectoryName(SourceFilePath()) ?? string.Empty,
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
        };

        foreach (var seed in seeds.Where(seed => seed.Length > 0))
        {
            var directory = new DirectoryInfo(seed);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "TheGuideToTheNewEden", "TheGuideToTheNewEden.WPF");
                if (File.Exists(Path.Combine(candidate, "TheGuideToTheNewEden.WPF.csproj")))
                {
                    return candidate;
                }

                // 已经是项目目录（从 bin 向上找时）
                if (File.Exists(Path.Combine(directory.FullName, "TheGuideToTheNewEden.WPF.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }

    /// <summary>编译期把本文件路径写进程序集——用它反推仓库位置最稳（不受 OutDir 重定向影响）。</summary>
    private static string SourceFilePath([CallerFilePath] string path = "") => path;
}
