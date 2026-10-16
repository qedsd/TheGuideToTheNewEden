namespace TheGuideToTheNewEden.WPF.Views.Pages.Settings;

/// <summary>
/// 「设置详情的第三段面包屑」契约（阶段 124）：设置子页可以自己有更多层级（例如「AI」页里的四个分节），
/// 由**外层设置页统一显示**面包屑「设置 › AI › 行为」，而不是子页再自带一条标题栏（那样会出现两条标题）。
/// <para>
/// 用法：子页实现本接口 → 外层 <c>SettingsPage</c> 在打开该分类时挂上事件；子页层级变化时触发
/// <see cref="BreadcrumbChanged"/>，外层读 <see cref="BreadcrumbSubTitle"/> 决定显示/隐藏第三段；
/// 用户点面包屑中间那段（分类名）时，外层回调 <see cref="NavigateBackToParent"/> 让子页回到自己的上一层。
/// </para>
/// </summary>
public interface ISettingsDetailPage
{
    /// <summary>第三段面包屑文本（<c>null</c>/空 = 只显示「设置 › 分类」两段）。</summary>
    string? BreadcrumbSubTitle { get; }

    /// <summary>子页层级变化（进入/退出某一分节）时触发。</summary>
    event EventHandler? BreadcrumbChanged;

    /// <summary>用户点击面包屑里的分类名时调用：子页应回到自己的上一层（例如 AI 页回到分节列表）。</summary>
    void NavigateBackToParent();
}
