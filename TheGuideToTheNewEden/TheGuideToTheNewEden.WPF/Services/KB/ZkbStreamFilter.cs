using System.Collections.Concurrent;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.KB;
using ZKB.NET.Models.KillStream;

namespace TheGuideToTheNewEden.WPF.Services.KB;

/// <summary>
/// 击杀流过滤器：把 <see cref="ZKBStreamConfig"/> 里的 6 组黑白名单编译成不可变的匹配快照。
///
/// 语义（与 WinUI 一致）：
/// <list type="bullet">
///   <item><b>排除项</b>命中即丢弃；</item>
///   <item><b>包含项</b>非空时，必须命中其中之一才保留；为空则放行。</item>
/// </list>
/// 三组分别作用于：通用（星系/星域/被击杀舰船类型）、受害者（角色/军团/联盟）、攻击者（角色/军团/联盟）。
///
/// 相对 WinUI 的修正：① 未配置星域过滤时<b>跳过</b> system→region 的 SQLite 查询
/// （原实现对每条流消息都查一次库）；② 不复制"id &lt;= 0 且包含项为空也算不通过"的边界缺陷。
/// 由于快照不可变，配置变更时应重新 <see cref="FromConfig"/> 并替换（由中枢负责），
/// 从而修复 WinUI"过滤条件在连接期间修改不生效、必须断开重连"的问题。
/// </summary>
public sealed class ZkbStreamFilter
{
    /// <summary>空过滤器（不做任何过滤）。</summary>
    public static readonly ZkbStreamFilter Empty = new();

    private readonly FilterSet _commonSystemExclusions = new();
    private readonly FilterSet _commonSystemInclusions = new();
    private readonly FilterSet _commonRegionExclusions = new();
    private readonly FilterSet _commonRegionInclusions = new();
    private readonly FilterSet _commonTypeExclusions = new();
    private readonly FilterSet _commonTypeInclusions = new();
    private readonly FilterSet _victimCharacterExclusions = new();
    private readonly FilterSet _victimCharacterInclusions = new();
    private readonly FilterSet _victimCorporationExclusions = new();
    private readonly FilterSet _victimCorporationInclusions = new();
    private readonly FilterSet _victimAllianceExclusions = new();
    private readonly FilterSet _victimAllianceInclusions = new();
    private readonly FilterSet _attackerCharacterExclusions = new();
    private readonly FilterSet _attackerCharacterInclusions = new();
    private readonly FilterSet _attackerCorporationExclusions = new();
    private readonly FilterSet _attackerCorporationInclusions = new();
    private readonly FilterSet _attackerAllianceExclusions = new();
    private readonly FilterSet _attackerAllianceInclusions = new();

    /// <summary>systemId → regionId 缓存（仅当配置了星域过滤时才会被填充）。</summary>
    private readonly ConcurrentDictionary<int, int> _systemRegionCache = new();

    /// <summary>是否配置了星域过滤（未配置时跳过 system→region 的库查询）。</summary>
    private bool _needRegionLookup;

    private ZkbStreamFilter()
    {
    }

    /// <summary>从配置快照编译过滤器。</summary>
    public static ZkbStreamFilter FromConfig(ZKBStreamConfig? config)
    {
        if (config is null)
        {
            return Empty;
        }

        config.EnsureRoleFiltersInitialized();

        var filter = new ZkbStreamFilter();

        AddByCat(config.CommonExclusions, filter._commonSystemExclusions, filter._commonRegionExclusions, filter._commonTypeExclusions);
        AddByCat(config.CommonInclusions, filter._commonSystemInclusions, filter._commonRegionInclusions, filter._commonTypeInclusions);
        AddRoles(config.VictimExclusions, filter._victimCharacterExclusions, filter._victimCorporationExclusions, filter._victimAllianceExclusions);
        AddRoles(config.VictimInclusions, filter._victimCharacterInclusions, filter._victimCorporationInclusions, filter._victimAllianceInclusions);
        AddRoles(config.AttackerExclusions, filter._attackerCharacterExclusions, filter._attackerCorporationExclusions, filter._attackerAllianceExclusions);
        AddRoles(config.AttackerInclusions, filter._attackerCharacterInclusions, filter._attackerCorporationInclusions, filter._attackerAllianceInclusions);

        // 只有"星域"参与过滤时才需要 system→region 的库查询（有排除项也需查，用于判断命中）。
        filter._needRegionLookup = filter._commonRegionExclusions.Count > 0 || filter._commonRegionInclusions.Count > 0;
        return filter;
    }

    /// <summary>判断一条击杀流消息是否通过过滤。</summary>
    public bool Pass(SKBDetail? detail)
    {
        if (detail?.Victim is null)
        {
            return false;
        }

        // 通用：星系
        if (!PassGroup(_commonSystemExclusions, _commonSystemInclusions, detail.SolarSystemId))
        {
            return false;
        }

        // 通用：被击杀舰船类型
        if (!PassGroup(_commonTypeExclusions, _commonTypeInclusions, detail.Victim.ShipTypeId))
        {
            return false;
        }

        // 通用：星域（仅在配置了星域过滤时才查库）
        if (_needRegionLookup && !PassGroup(_commonRegionExclusions, _commonRegionInclusions, ResolveRegion(detail.SolarSystemId)))
        {
            return false;
        }

        // 受害者
        if (!PassGroup(_victimCharacterExclusions, _victimCharacterInclusions, detail.Victim.CharacterId)
            || !PassGroup(_victimCorporationExclusions, _victimCorporationInclusions, detail.Victim.CorporationId)
            || !PassGroup(_victimAllianceExclusions, _victimAllianceInclusions, detail.Victim.AllianceId))
        {
            return false;
        }

        // 攻击者（任一命中即算命中）
        if (detail.Attackers is { Count: > 0 })
        {
            if (!PassGroupAny(_attackerCharacterExclusions, _attackerCharacterInclusions, detail.Attackers.Select(a => a.CharacterId))
                || !PassGroupAny(_attackerCorporationExclusions, _attackerCorporationInclusions, detail.Attackers.Select(a => a.CorporationId))
                || !PassGroupAny(_attackerAllianceExclusions, _attackerAllianceInclusions, detail.Attackers.Select(a => a.AllianceId)))
            {
                return false;
            }
        }

        return true;
    }

    private int ResolveRegion(int systemId)
    {
        if (systemId <= 0)
        {
            return 0;
        }

        return _systemRegionCache.GetOrAdd(systemId, static id =>
        {
            try
            {
                return Core.Services.DB.MapSolarSystemService.Query(id)?.RegionID ?? 0;
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                return 0;
            }
        });
    }

    private static bool PassGroup(FilterSet exclusions, FilterSet inclusions, int id)
        => !exclusions.Contains(id) && (inclusions.Count == 0 || inclusions.Contains(id));

    private static bool PassGroupAny(FilterSet exclusions, FilterSet inclusions, IEnumerable<int> ids)
    {
        var list = ids as IReadOnlyCollection<int> ?? ids.ToList();

        foreach (var id in list)
        {
            if (exclusions.Contains(id))
            {
                return false;
            }
        }

        if (inclusions.Count == 0)
        {
            return true;
        }

        foreach (var id in list)
        {
            if (inclusions.Contains(id))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddByCat(IEnumerable<IdName>? items, FilterSet systems, FilterSet regions, FilterSet types)
    {
        if (items is null)
        {
            return;
        }

        foreach (var item in items)
        {
            switch (item.GetCategory())
            {
                case IdName.CategoryEnum.SolarSystem:
                    systems.Add(item.Id);
                    break;
                case IdName.CategoryEnum.Region:
                    regions.Add(item.Id);
                    break;
                case IdName.CategoryEnum.InventoryType:
                    types.Add(item.Id);
                    break;
            }
        }
    }

    private static void AddRoles(IEnumerable<IdName>? items, FilterSet characters, FilterSet corporations, FilterSet alliances)
    {
        if (items is null)
        {
            return;
        }

        foreach (var item in items)
        {
            switch (item.GetCategory())
            {
                case IdName.CategoryEnum.Character:
                    characters.Add(item.Id);
                    break;
                case IdName.CategoryEnum.Corporation:
                    corporations.Add(item.Id);
                    break;
                case IdName.CategoryEnum.Alliance:
                    alliances.Add(item.Id);
                    break;
            }
        }
    }

    /// <summary>不可变的正整数集合。</summary>
    private sealed class FilterSet
    {
        private readonly HashSet<int> _values = [];

        public int Count => _values.Count;

        public void Add(int id)
        {
            if (id > 0)
            {
                _values.Add(id);
            }
        }

        public bool Contains(int id) => id > 0 && _values.Contains(id);
    }
}
