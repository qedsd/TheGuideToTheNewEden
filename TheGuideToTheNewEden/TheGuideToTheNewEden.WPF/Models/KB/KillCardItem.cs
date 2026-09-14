using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.KB;

namespace TheGuideToTheNewEden.WPF.Models.KB;

/// <summary>
/// 一页击杀列表的返回值：条目 + 是否还有下一页。
/// 用专门的分页模型替代 WinUI 里"用返回值个数推测还有没有下一页"的隐式约定。
/// </summary>
public sealed class KillmailPage
{
    public IReadOnlyList<KBItemInfo> Items { get; init; } = [];

    /// <summary>是否还有下一页（当前页已填满）。</summary>
    public bool HasNext { get; init; }
}

/// <summary>
/// 排名卡片（最贵击杀 / 最高击杀 / 超期击杀 / 分类统计共用）的统一展示模型。
///
/// WinUI 版这几处在不同 VM 里各自拼装 Core 的不同模型（<c>KillDataInfo</c> / KillData），
/// 这里统一成一个不可变展示模型，卡片控件只认这一个类型。
/// </summary>
public sealed class KillCardItem
{
    /// <summary>排名（从 1 开始；0 表示不显示排名）。</summary>
    public int No { get; set; }

    /// <summary>主标题（角色名 / 军团名 / 舰船名 / 星系名）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>副标题（如军团/联盟、舰船与类别）。</summary>
    public string? SubTitle { get; set; }

    /// <summary>头像/徽标/图标地址（为空则只显示文字）。</summary>
    public string? ImageUrl { get; set; }

    /// <summary>击杀数。</summary>
    public int Kills { get; set; }

    /// <summary>已在外部格式化好的估价文本（仅"最贵击杀"使用）。</summary>
    public string? ValueText { get; set; }

    /// <summary>原始估价（用于排序/比较；仅"最贵击杀"使用）。</summary>
    public double Value { get; set; }

    /// <summary>卡片点击后要打开的 killmail ID（&gt;0 表示打开 KB 详情）。</summary>
    public int KillmailId { get; set; }

    /// <summary>卡片点击后要打开的实体 ID（&gt;0 表示打开实体统计页）。</summary>
    public int EntityId { get; set; }

    /// <summary>实体类别（决定图片与跳转类型）。</summary>
    public IdName.CategoryEnum EntityCategory { get; set; }

    /// <summary>是否打开 KB 详情。</summary>
    public bool IsKillmail => KillmailId > 0;

    /// <summary>是否打开实体统计。</summary>
    public bool HasEntity => EntityId > 0;
}

/// <summary>「最高击杀」里的一组（按角色/军团/联盟/势力/舰船/星系分组）。</summary>
public sealed class KillStatisticGroup
{
    /// <summary>分组标题（已本地化）。</summary>
    public string Title { get; init; } = string.Empty;

    public IReadOnlyList<KillCardItem> Items { get; init; } = [];
}
