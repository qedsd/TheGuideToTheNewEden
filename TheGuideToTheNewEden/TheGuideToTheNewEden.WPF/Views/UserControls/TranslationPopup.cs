using System.Windows;
using TheGuideToTheNewEden.WPF.ViewModels.Translation;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>
/// 翻译页的「弹窗」：把 AI 页 / 本地词库页的内容放进独立工具窗口，各保持一个实例（重复点击只激活）。
/// <para>
/// 弹窗里的视图是**另一份实例**（自己的 VM、自己的状态），与页面互不干扰；
/// 两边共用同一份对话历史文件（<c>Configs/AiTranslationHistory.json</c>）。
/// </para>
/// </summary>
internal static class TranslationPopup
{
    private enum Kind
    {
        Ai,
        Local,
    }

    private static readonly Dictionary<Kind, ToolWindow> Windows = [];

    /// <summary>弹出「AI 翻译」对话窗口。</summary>
    internal static void ShowAi(FrameworkElement anchor) => Show(
        Kind.Ai,
        anchor,
        () => new AiTranslationChatView { DataContext = new AiChatTranslationViewModel { CanPopWindow = false } },
        "Nav.TranslationAi",
        1040,
        760);

    /// <summary>弹出「本地词库」窗口。</summary>
    internal static void ShowLocal(FrameworkElement anchor) => Show(
        Kind.Local,
        anchor,
        () => new LocalTranslationPanelView { DataContext = new LocalTranslationViewModel { CanPopWindow = false } },
        "Nav.TranslationLocal",
        980,
        700);

    private static void Show(Kind kind, FrameworkElement anchor, Func<FrameworkElement> factory, string titleKey, int width, int height)
    {
        if (!Windows.TryGetValue(kind, out var window) || window is null)
        {
            window = new ToolWindow(
                factory(),
                ToolWindowTitleStyle.Default,
                showTopmostButton: true,
                showInTaskbar: true,
                width: width,
                height: height)
            {
                // **不设 Owner**：被拥有的窗口永远压在宿主窗口之上，"取消置顶"看起来和置顶一样
                // （用户实测反馈）。不拥有 + 手动居中：未置顶时是普通窗口，置顶时才浮在最前。
                DisplayTitle = FindString(titleKey),
            };
            window.Closed += (_, _) => Windows.Remove(kind);
            Windows[kind] = window;
            CenterOverHost(window, anchor);
        }

        window.Show();
        window.Activate();
    }

    /// <summary>居中到宿主主窗口（不设 Owner，所以 WindowStartupLocation.CenterOwner 不适用）。</summary>
    private static void CenterOverHost(Window window, FrameworkElement anchor)
    {
        var host = Window.GetWindow(anchor);
        if (host is null)
        {
            return;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        var workArea = SystemParameters.WorkArea;
        var left = host.Left + (host.ActualWidth - window.Width) / 2;
        var top = host.Top + (host.ActualHeight - window.Height) / 2;
        window.Left = Math.Clamp(left, workArea.Left, Math.Max(workArea.Left, workArea.Right - window.Width));
        window.Top = Math.Clamp(top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - window.Height));
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
