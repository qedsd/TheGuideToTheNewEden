using System.Windows;
using TheGuideToTheNewEden.WPF.Views.UserControls;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 「使用手册」窗口：工具窗口外壳 + <see cref="ManualBrowserView"/>（左列表右正文）。
/// <para>
/// 单实例复用（重复 <see cref="Show"/> 只激活并跳转，不会开一堆）；**不设 Owner**——
/// 被拥有的窗口永远压在宿主之上，"取消置顶"看起来和置顶一样（阶段 46 实测，与助手窗口同一结论）。
/// </para>
/// <para>
/// 入口：主页品牌条「手册」按钮（默认打开快速上手）、主窗口 <c>F1</c>（按当前页打开对应手册）、
/// 说明书正文里的内链与 AI 助手的 <c>manual_*</c> 工具结果。
/// </para>
/// </summary>
public static class ManualWindow
{
    private static ToolWindow? _window;
    private static ManualBrowserView? _view;

    /// <summary>窗口当前是否打开。</summary>
    public static bool IsOpen => _window is not null;

    /// <summary>
    /// 打开手册窗口（已开则只激活），可选地直接跳到某篇手册。
    /// </summary>
    /// <param name="manualId">手册 id（如 <c>characters/mail</c>）；null = 打开列表让用户选。</param>
    public static void Show(string? manualId = null)
    {
        if (_window is null)
        {
            _view = new ManualBrowserView();
            _window = new ToolWindow(
                _view,
                ToolWindowTitleStyle.Default,
                showTopmostButton: true,
                showInTaskbar: true,
                width: 1080,
                height: 720)
            {
                DisplayTitle = Application.Current?.TryFindResource("Manual.Window.Title") as string ?? "使用手册",
            };
            _window.Closed += (_, _) =>
            {
                _window = null;
                _view = null;
            };
            _window.Show();
        }

        if (manualId is { Length: > 0 })
        {
            _view?.NavigateTo(manualId);
        }

        _window.Activate();
    }
}
