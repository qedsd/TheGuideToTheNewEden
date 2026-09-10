namespace TheGuideToTheNewEden.WPF.Services.Characters;

/// <summary>
/// 分页结果。替代 WinUI 版在各页各写一份 <c>_xxxLoaded</c> + <c>Pivot_SelectionChanged</c> 的做法。
/// </summary>
public sealed class PagedResult<T>
{
    public required int Page { get; init; }

    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>ESI 分页无法预知总页数，用"本页是否满页"判断是否可能还有下一页。</summary>
    public required bool HasNextPage { get; init; }

    public bool HasPreviousPage => Page > 1;
}