namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 工具窗外壳的最小公共面：让页面不必关心自己装在哪种窗口里
/// （普通不透明的 <see cref="ToolWindow"/>，或逐像素透明的 <see cref="ToolTransparentWindow"/>）。
/// </summary>
public interface IToolWindowShell
{
    /// <summary>指定标题栏上显示哪些按钮（未列出的隐藏），语义与 <see cref="ToolWindow.SetVisibleTitleBarButtons"/> 一致。</summary>
    void SetVisibleTitleBarButtons(ToolWindowButtons buttons);
}
