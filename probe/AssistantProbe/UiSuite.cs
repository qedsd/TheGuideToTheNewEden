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
using TheGuideToTheNewEden.WPF.Views.Pages;
using TheGuideToTheNewEden.WPF.Views.UserControls;

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

        c.Section("UiSuite · 页面 / 视图构造与绑定");
        ConstructionAndBinding(c);
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
            "Assistant_Chat_ContextOn", "Assistant_Chat_ContextOff", "Assistant_Chat_PendingCount",
            "Assistant_Chat_NoInput", "Assistant_Chat_AnswerEmpty", "Assistant_Chat_Timeout",
            "Assistant_Chat_ConfirmDisabled", "Assistant_AiUnavailable", "Assistant_NotConfigured_Title",
            "Assistant_NotConfigured_Support", "Assistant_GoConfigure", "Assistant_Recheck",
            "AssistantStep_Done", "AssistantStep_Failed", "SettingPage_AiAssistant", "SettingPage_AiAssistant_Desc",
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

        Pump();
        c.That(bindingErrors.Count == 0, $"WPF 绑定错误 0 条（实际 {bindingErrors.Count}）");
        foreach (var error in bindingErrors.Take(8))
        {
            Console.WriteLine($"    [绑定] {error}");
        }

        window.Close();
        PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
    }

    // ============================ 基础设施 ============================

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
