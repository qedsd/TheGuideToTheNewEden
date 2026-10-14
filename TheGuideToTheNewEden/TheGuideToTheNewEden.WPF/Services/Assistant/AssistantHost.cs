using TheGuideToTheNewEden.WPF.ViewModels.Assistant;

namespace TheGuideToTheNewEden.WPF.Services.Assistant;

/// <summary>
/// AI 助手 VM 的**共享宿主**：页面与弹窗共用同一个 <see cref="AiAssistantViewModel"/> 实例，
/// 而全局快捷键需要在"助手页从未被打开过"时也能弹出窗口——因此实例不能由页面独占创建。
/// <para>
/// 生命周期仍按阶段 108 的约定：<c>Init</c> / <c>Dispose</c> 由**页面**与**弹窗**在显示/关闭时按需调用
/// （两者都调用是安全的：<c>Init</c> 先退订再订阅、<c>Dispose</c> 只退订）。
/// </para>
/// </summary>
internal static class AssistantHost
{
    private static AiAssistantViewModel? _viewModel;

    /// <summary>共享的助手 VM（首次访问时创建并载入历史）。</summary>
    internal static AiAssistantViewModel ViewModel => _viewModel ??= new AiAssistantViewModel();
}
