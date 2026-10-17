namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 页面自报「我现在这幅画面该看哪篇说明书」（主窗口 <c>F1</c> 用）。
/// <para>
/// 只给**含子页 / 页内模式**的壳页实现：壳页知道用户当前停在哪个子页签，
/// 而 <see cref="Services.Manual.ManualToolMap"/> 只能按页面类型做静态映射
/// （映射到壳页的父手册，如 <c>characters</c>）。
/// 返回 <c>null</c> 表示"这个状态没有更精确的手册"，调用方回落到静态映射。
/// </para>
/// <para>
/// 普通页面（无子页签）**不要**实现它——在 <see cref="Services.Manual.ManualToolMap"/> 里补一行即可。
/// </para>
/// </summary>
public interface IManualAware
{
    /// <summary>当前画面最贴切的说明书 id（如 <c>characters/mail</c>）；无更精确结果时返回 <c>null</c>。</summary>
    string? ManualId { get; }
}
