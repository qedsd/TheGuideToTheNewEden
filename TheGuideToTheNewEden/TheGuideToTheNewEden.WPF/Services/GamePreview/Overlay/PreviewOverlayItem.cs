using TheGuideToTheNewEden.Core.Models.GamePreviews;

namespace TheGuideToTheNewEden.WPF.Services.GamePreview.Overlay;

/// <summary>
/// 信息行的重要程度。
/// <para>
/// 阶段 355 曾用它给行首圆点着色；阶段 356 按用户要求去掉了圆点（行内只留文字，颜色由用户自选），
/// 因此**当前不参与渲染**，仅作为数据契约保留（将来若要"重要行高亮/排序"可以直接用）。
/// </para>
/// </summary>
public enum PreviewOverlaySeverity
{
    /// <summary>一般信息。</summary>
    Normal,

    /// <summary>提示。</summary>
    Caution,

    /// <summary>需要立刻注意。</summary>
    Critical,
}

/// <summary>
/// 预览窗叠加层的一行信息。<see cref="Text"/> 由 provider 自行本地化好；
/// 叠加窗只按 <see cref="Severity"/> 着色，不再认识任何具体功能。
/// </summary>
/// <param name="Key">稳定标识（同一 provider 内唯一），便于日志排查与后续去重。</param>
/// <param name="Text">整行文字，例如「最近 3 跳 · 2min」。</param>
/// <param name="Severity">数据契约里的重要程度（当前不参与渲染）。</param>
/// <param name="Order">多 provider 混排时的排序号（数值小在上）。</param>
/// <param name="Detail">
/// 悬停时用 **ToolTip 浮层**显示的详细内容（可多行）。为空表示该行没有更多细节。
/// <para>
/// 叠加窗是鼠标穿透的，拿不到鼠标消息，所以悬停由下面的预览窗检测，
/// 再让叠加窗弹出 ToolTip（见 <c>GamePreviewWindow</c> 的 hover 逻辑）。
/// 用浮层而不是往叠加窗里加面板：信息行下面的区域留给以后的功能，不能被详情占位。
/// </para>
/// </param>
public sealed record PreviewOverlayItem(
    string Key,
    string Text,
    PreviewOverlaySeverity Severity,
    int Order,
    string? Detail = null);

/// <summary>
/// 一次叠加内容查询的上下文：provider 据此决定"这个窗口要不要显示、显示什么"。
/// <para>
/// 把设置一起传进来（而不是让 provider 自己去翻全局单例），是为了让每个 provider
/// 都能拥有自己的开关，同时保持叠加层与 <see cref="Views.Windows.GamePreviewWindow"/> 完全通用。
/// </para>
/// </summary>
/// <param name="CharacterName">该预览窗对应角色（可能为 null：窗口标题还没解析出角色名）。</param>
/// <param name="Item">该预览窗的单项设置。</param>
/// <param name="Global">多开全局设置。</param>
public sealed record PreviewOverlayContext(
    string? CharacterName,
    PreviewItem Item,
    PreviewSetting Global);

/// <summary>
/// 预览窗叠加层的**信息提供者**。以后要往叠加层加新内容（日志监控命中、频道监控、星图情报……）时，
/// 只需实现本接口并注册到 <see cref="PreviewOverlayHub"/>，**叠加窗与预览窗的渲染/布局代码一行都不用改**。
/// </summary>
public interface IPreviewOverlayProvider
{
    /// <summary>稳定标识（与设置项/日志对应），如 <c>intel</c>。</summary>
    string Key { get; }

    /// <summary>
    /// 某个角色的叠加内容变了；参数为角色名，<c>null</c> 表示"全部都可能变了"（全局刷新）。
    /// <para>实现方负责在事件里只报告相关角色，避免所有预览窗无谓重排。</para>
    /// </summary>
    event Action<string?>? Changed;

    /// <summary>返回该窗口要显示的信息行（不需要显示时返回空集合）。</summary>
    IReadOnlyList<PreviewOverlayItem> GetItems(PreviewOverlayContext context);
}
