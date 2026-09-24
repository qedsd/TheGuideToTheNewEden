using System.IO;

namespace TheGuideToTheNewEden.WPF.Services.Map;

/// <summary>一次主权装载的结果：数据（失败时为空或旧缓存）+ 成败标记。</summary>
public readonly record struct SovLoadResult(IReadOnlyList<SovInfo> Infos, bool Success);

/// <summary>一个联盟在星图上的主权数据（分组号用于"多个联盟同色"的分组展示）。</summary>
public sealed class SovInfo
{
    public long AllianceId { get; set; }
    public string AllianceName { get; set; } = string.Empty;
    public HashSet<int> SystemIds { get; set; } = [];

    /// <summary>分组号（&lt;1 表示未分配，由 <see cref="SovService.AssignGroups"/> 补齐）。</summary>
    public long GroupId { get; set; }

    public int Count => SystemIds.Count;
}

/// <summary>
/// 星图主权（SOV）数据：ESI <c>Sovereignty.ListSovereigntyOfSystemsAsync</c> 的联盟聚合 + 联盟名解析，
/// 分组号持久化在 <c>Configs/SOVGroup.json</c>（**与 WinUI 版同路径同格式**：每行 <c>联盟ID 分组ID</c>）。
/// 结果在内存里缓存 <see cref="CacheMinutes"/> 分钟。
/// </summary>
public static class SovService
{
    private static readonly string GroupFilePath = Path.Combine(SettingsService.DataPath, "Configs", "SOVGroup.json");
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

    /// <summary>取星系的主权分组号（未加载或无主权返回 0）。</summary>
    public static long GetGroupId(int systemId) => GetSovInfo(systemId)?.GroupId ?? 0;

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

            AssignGroups(result);
            return new SovLoadResult(result, success);
        }
    }

    /// <summary>把分组号补齐（读 SOVGroup.json；未登记的分组按当前最大分组 +1 递增）。</summary>
    public static void AssignGroups(List<SovInfo>? infos)
    {
        if (infos is null || infos.Count == 0)
        {
            return;
        }

        var groups = ReadGroups();
        var next = groups.Count > 0 ? groups.Values.Max() + 1 : 1;
        foreach (var info in infos)
        {
            if (groups.TryGetValue(info.AllianceId, out var groupId) && groupId > 0)
            {
                info.GroupId = groupId;
            }
            else
            {
                info.GroupId = next++;
                groups[info.AllianceId] = info.GroupId;
            }
        }

        WriteGroups(groups);
    }

    /// <summary>把所有分组重置为 1..N（按系统数降序，与 WinUI 的"重置分组"一致）。</summary>
    public static void ResetGroupsToDefault(List<SovInfo> infos)
    {
        if (infos.Count == 0)
        {
            return;
        }

        var groups = new Dictionary<long, long>();
        long groupId = 1;
        foreach (var info in infos.OrderByDescending(p => p.Count))
        {
            info.GroupId = groupId;
            groups[info.AllianceId] = groupId;
            groupId++;
        }

        WriteGroups(groups);
    }

    /// <summary>把当前分组号落盘（分组编辑窗"确认"时调用）。</summary>
    public static void SaveGroups(IEnumerable<SovInfo> infos)
    {
        var groups = new Dictionary<long, long>();
        foreach (var info in infos)
        {
            if (info.GroupId > 0)
            {
                groups[info.AllianceId] = info.GroupId;
            }
        }

        WriteGroups(groups);
    }

    /// <summary>读分组文件（每行 <c>联盟ID 分组ID</c>，空格分隔；文件不存在返回空表）。</summary>
    public static Dictionary<long, long> ReadGroups()
    {
        var result = new Dictionary<long, long>();
        try
        {
            if (!File.Exists(GroupFilePath))
            {
                return result;
            }

            foreach (var line in File.ReadAllLines(GroupFilePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && long.TryParse(parts[0], out var allianceId) && long.TryParse(parts[1], out var groupId))
                {
                    result[allianceId] = groupId;
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        return result;
    }

    private static void WriteGroups(Dictionary<long, long> groups)
    {
        try
        {
            var folder = Path.GetDirectoryName(GroupFilePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllLines(GroupFilePath, groups.OrderBy(p => p.Value).Select(p => $"{p.Key} {p.Value}"));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }
}
