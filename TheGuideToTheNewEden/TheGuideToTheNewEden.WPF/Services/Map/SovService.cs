using System.IO;

namespace TheGuideToTheNewEden.WPF.Services.Map;

/// <summary>一次主权装载的结果：数据（失败时为空或旧缓存）+ 成败标记。</summary>
public readonly record struct SovLoadResult(IReadOnlyList<SovInfo> Infos, bool Success);

/// <summary>一个联盟在星图上的主权数据（按联盟聚合；分组/配色由 <see cref="MapSovGroupConfig"/> 在 VM 侧解析）。</summary>
public sealed class SovInfo
{
    public long AllianceId { get; set; }
    public string AllianceName { get; set; } = string.Empty;
    public HashSet<int> SystemIds { get; set; } = [];

    public int Count => SystemIds.Count;
}

/// <summary>
/// 星图主权（SOV）数据：ESI <c>Sovereignty.ListSovereigntyOfSystemsAsync</c> 的联盟聚合 + 联盟名解析。
/// 旧的分组号机制（Configs/SOVGroup.json，联盟ID→分组号）已弃用：
/// 新分组为命名分组（名称+颜色+多联盟成员），配置持久化在 MapSettings.json 的 <c>Sov</c> 节，
/// 由星图页 ViewModel 在装载后解析为节点展示实体。结果在内存里缓存 <see cref="CacheMinutes"/> 分钟。
/// </summary>
public static class SovService
{
    private const int CacheMinutes = 30;

    private static readonly object Locker = new();
    private static List<SovInfo>? _cache;
    private static DateTime _cacheTime;

    /// <summary>当前缓存（未加载过则为空列表）。</summary>
    public static IReadOnlyList<SovInfo> Current
    {
        get
        {
            lock (Locker)
            {
                return _cache ?? [];
            }
        }
    }

    private static Dictionary<int, SovInfo>? _systemIndex;

    /// <summary>
    /// 取星系所属的主权信息（未加载或无主权返回 null）。
    /// 内部维护"星系 → 主权"反查索引：情报列表/星系详情/一跳覆盖都会**按行**调用，
    /// 原来每次线性扫全部联盟 × 系统的做法是 O(星系总数)，现在 O(1)。
    /// </summary>
    public static SovInfo? GetSovInfo(int systemId)
    {
        var infos = Current;
        if (infos.Count == 0)
        {
            return null;
        }

        var index = _systemIndex;
        if (index is null)
        {
            index = new Dictionary<int, SovInfo>();
            foreach (var info in infos)
            {
                foreach (var id in info.SystemIds)
                {
                    index[id] = info;
                }
            }

            _systemIndex = index;
        }

        return index.TryGetValue(systemId, out var found) ? found : null;
    }

    /// <summary>取星系的主权联盟名（未加载或无主权返回空串）。</summary>
    public static string GetSovName(int systemId) => GetSovInfo(systemId)?.AllianceName ?? string.Empty;

    public static bool IsLoaded
    {
        get
        {
            lock (Locker)
            {
                return _cache is not null;
            }
        }
    }

    /// <summary>清掉内存缓存（下次重新拉 ESI）。</summary>
    public static void ClearCache()
    {
        lock (Locker)
        {
            _cache = null;
            _systemIndex = null;
        }
    }

    /// <summary>拉取（或复用缓存）主权数据。失败时返回上一次缓存（可能为空），不抛异常，用 <see cref="SovLoadResult.Success"/> 区分成败。</summary>
    public static async Task<SovLoadResult> LoadAsync(bool forceRefresh = false)
    {
        lock (Locker)
        {
            if (!forceRefresh && _cache is not null && (DateTime.UtcNow - _cacheTime).TotalMinutes < CacheMinutes)
            {
                return new SovLoadResult(_cache, true);
            }
        }

        var result = new List<SovInfo>();
        var success = false;
        try
        {
            // ESI 的 /sovereignty/map 被标记为"兼容日期 2026-05-19 后移除"（建议换 GetSovereigntySystemsAsync）。
            // 本项目 EVEStandard 固定兼容日期 v2025_12_16，接口仍可用；与 WinUI 版保持一致，待整体升级 ESI 版本时一并切换。
#pragma warning disable CS0618
            var resp = await Core.Services.ESIService.GetDefaultESI().Sovereignty.ListSovereigntyOfSystemsAsync();
#pragma warning restore CS0618
            if (resp?.Model is null)
            {
                Core.Log.Error("星图：ListSovereigntyOfSystemsAsync 失败");
            }
            else
            {
                success = true;
                foreach (var group in resp.Model.Where(p => p.AllianceId is > 0).GroupBy(p => p.AllianceId!.Value))
                {
                    var info = new SovInfo
                    {
                        AllianceId = group.Key,
                        SystemIds = [.. group.Select(p => (int)p.SystemId)],
                    };
                    result.Add(info);
                }

                // 联盟名（IDNameService 的主键是 int，联盟 ID 在 int 范围内，安全）
                var ids = result.Select(p => p.AllianceId).ToList();
                if (ids.Count > 0)
                {
                    var names = await Core.Services.IDNameService.GetByIdsAsync(ids);
                    var nameDic = names?.ToDictionary(p => (long)p.Id) ?? [];
                    foreach (var info in result)
                    {
                        info.AllianceName = nameDic.TryGetValue(info.AllianceId, out var name) && !string.IsNullOrEmpty(name.Name)
                            ? name.Name
                            : info.AllianceId.ToString();
                    }
                }

                result = [.. result.OrderByDescending(p => p.Count)];
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        lock (Locker)
        {
            // 只缓存成功结果：失败时旧缓存原样保留（可再试），首次失败也不落 30 分钟的空缓存——
            // 原实现 `_cache is null` 分支会把空结果当"合法数据"存住，半小时内重试全部命中空缓存（实机踩坑）。
            if (success)
            {
                _cache = result;
                _cacheTime = DateTime.UtcNow;
                _systemIndex = null; // 数据换了，反查索引重建
            }

            return new SovLoadResult(result, success);
        }
    }
}
