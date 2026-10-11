namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 「工具」分组里的页面：**在独立工具窗口里打开**（对齐 WinUI 版 <c>ITool</c> 契约里的
/// <c>GetWindowSize</c> / <c>Close</c>），不占用主窗口的内容区。
/// <para>
/// 主窗口在 <c>Navigating</c> 里拦下这些页面的导航，改由 <c>ToolWindowHost</c> 打开工具窗口。
/// </para>
/// </summary>
public interface IToolPage
{
    /// <summary>打开时的默认宽度（像素）。</summary>
    int PreferredWidth { get; }

    /// <summary>打开时的默认高度（像素）。</summary>
    int PreferredHeight { get; }

    /// <summary>
    /// 该工具是否要**真透明**的窗口外壳（桌面能透出来）。
    /// 为 <c>true</c> 时 <c>ToolWindowHost</c> 用 <c>ToolTransparentWindow</c>（普通 Window +
    /// <c>AllowsTransparency</c> + 半透明背板，配方照 IntelWindow）而不是普通的 <c>ToolWindow</c>。
    /// 目前只有服务器测速要（迷你形态当挂机监控条，背景可调透明度、文字保持实色）。
    /// </summary>
    bool NeedsTransparentWindow => false;


    /// <summary>
    /// 工具窗口关闭时调用：停掉后台工作（键盘钩子、测速循环等）。
    /// 页面实例在窗口关闭后即被丢弃，但**后台工作不会自己停**，所以这个回调是必须的。
    /// </summary>
    void OnToolClosed();
}
