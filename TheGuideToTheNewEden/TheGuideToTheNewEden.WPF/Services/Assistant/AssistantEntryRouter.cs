namespace TheGuideToTheNewEden.WPF.Services.Assistant;

/// <summary>
/// AI 入口的**显示位置判定**（阶段 128）：把"这一次打开 AI 该走主窗口页面还是独立窗口"抽成纯函数，
/// 好处是① 生产代码只有一个判定点；② 探针能离线断言真值表，不用改用户的 settings.json。
/// <para>
/// 规则：设置 → AI → 行为 →「在主窗口中显示 AI」**勾选**时，助手页显示在主窗口里（老行为）；
/// **不勾**（默认）时，助手页的所有入口都改用 <c>Views.Windows.AssistantWindow</c> 的独立窗口。
/// 全局快捷键 <c>Alt+Q</c> 与对话页的「弹窗」按钮**始终**是独立窗口，不受本设置影响。
/// </para>
/// </summary>
public static class AssistantEntryRouter
{
    /// <summary>该页类型在当前设置下是否应该改用独立窗口（而不是在主窗口里导航过去）。</summary>
    public static bool ShouldOpenWindow(bool showInMainWindow, Type? pageType)
        => !showInMainWindow && pageType == typeof(Views.Pages.AiAssistantPage);
}
