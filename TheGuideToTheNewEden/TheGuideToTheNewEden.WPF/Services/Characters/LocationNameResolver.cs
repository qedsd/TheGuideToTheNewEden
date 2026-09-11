using TheGuideToTheNewEden.Core.Services;

namespace TheGuideToTheNewEden.WPF.Services.Characters;

/// <summary>
/// 地点名称解析：按 ID 可能落在"结构（structure）"域还是普通域，分两条路解析。
/// </summary>
/// <remarks>
/// <see cref="IDNameService"/> 的 ID 是 <see cref="int"/>，而结构 ID 约 1e12，
/// 直接传入会被 <c>(int)</c> 静默截断并解析出**另一个**名称（不报错）。
/// 因此这里按 <see cref="int.MaxValue"/> 分流：范围内的走 <see cref="IDNameService"/>，
/// 超出的走 <see cref="StructureService"/>。查不到的名称不写入结果，由调用方回退显示原始 ID。
/// </remarks>
public static class LocationNameResolver
{
    /// <summary>int 能安全承载的最大值；比它大的 ID 一律按结构 ID 处理。</summary>
    private const long MaxSafeId = int.MaxValue;

    public static async Task<Dictionary<long, string>> ResolveAsync(IEnumerable<long> ids)
    {
        var result = new Dictionary<long, string>();
        var all = ids.Where(p => p > 0).Distinct().ToList();
        if (all.Count == 0)
        {
            return result;
        }

        var safeIds = all.Where(p => p <= MaxSafeId).ToList();
        if (safeIds.Count > 0)
        {
            try
            {
                foreach (var item in await IDNameService.GetByIdsAsync(safeIds) ?? [])
                {
                    if (!string.IsNullOrEmpty(item.Name))
                    {
                        result[item.Id] = item.Name;
                    }
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        }

        // 结构 ID：目前只有 StructureService 的本地列表（市场结构 / 已缓存结构）可用，
        // ESI 查询路径待移植（见 REFACTORING.md「结构（structure）ID 解析约定」）。
        foreach (var id in all.Where(p => p > MaxSafeId))
        {
            var structure = StructureService.GetStructure(id);
            if (structure is not null && !string.IsNullOrWhiteSpace(structure.Name))
            {
                result[id] = structure.Name;
            }
        }

        return result;
    }
}
