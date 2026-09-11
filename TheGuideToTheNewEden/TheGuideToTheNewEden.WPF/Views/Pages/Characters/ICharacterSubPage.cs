namespace TheGuideToTheNewEden.WPF.Views.Pages.Characters;

/// <summary>
/// 角色工作区的子页契约：工作区右上角的"刷新"按钮通过它刷新当前子页，
/// 这样刷新不会重建页面实例（保留页面状态与后台任务）。
/// </summary>
public interface ICharacterSubPage
{
    /// <summary>重新加载数据（forceRefresh=true 时绕过缓存）。</summary>
    Task RefreshAsync(bool forceRefresh = true);
}
