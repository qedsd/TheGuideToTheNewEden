using System.Collections.Concurrent;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.KB;
using ZKB.NET.Models.KillStream;

namespace TheGuideToTheNewEden.WPF.Services.KB;

/// <summary>
/// 击杀流过滤器：把 <see cref="ZKBStreamConfig"/> 的四个维度编译成不可变匹配快照。
///
/// 语义：
/// <list type="bullet">
///   <item>每个维度单独选择 不过滤 / 只保留匹配 / 排除匹配；</item>
///   <item>列表内实体是 OR（位置里星系与星域也是 OR）；</item>
///   <item>维度之间是 AND；</item>
///   <item>不过滤或列表为空时不限制；</item>
///   <item>攻击者维度只要任一名攻击者命中即可。</item>
/// </list>
/// </summary>
public sealed class ZkbStreamFilter
{
    /// <summary>空过滤器（不做任何过滤）。</summary>
    public static readonly ZkbStreamFilter Empty = new();

    private readonly ZkbFilterMode _locationMode;
    private readonly ZkbFilterMode _shipMode;
    private readonly ZkbFilterMode _victimMode;
    private readonly ZkbFilterMode _attackerMode;

    private readonly FilterSet _locationSystems = new();
    private readonly FilterSet _locationRegions = new();
    private readonly FilterSet _shipTypes = new();
    private readonly FilterSet _victimIds = new();
    private readonly FilterSet _attackerIds = new();

    /// <summary>systemId → regionId 缓存（仅当位置过滤包含星域时才会被填充）。</summary>
    private readonly ConcurrentDictionary<int, int> _systemRegionCache = new();

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

        var filter = new ZkbStreamFilter(
            config.LocationMode,
            config.ShipMode,
            config.VictimMode,
            config.AttackerMode);

        AddLocations(config.Locations, filter._locationSystems, filter._locationRegions);
        AddShipTypes(config.Ships, filter._shipTypes);
        AddRoles(config.Victims, filter._victimIds);
        AddRoles(config.Attackers, filter._attackerIds);

        filter._needRegionLookup =
            filter._locationMode != ZkbFilterMode.None && filter._locationRegions.Count > 0;
        return filter;
    }

    private ZkbStreamFilter(
        ZkbFilterMode locationMode,
        ZkbFilterMode shipMode,
        ZkbFilterMode victimMode,
        ZkbFilterMode attackerMode)
    {
        _locationMode = locationMode;
        _shipMode = shipMode;
        _victimMode = victimMode;
        _attackerMode = attackerMode;
    }

    /// <summary>判断一条击杀流消息是否通过过滤。</summary>
    public bool Pass(SKBDetail? detail)
    {
        if (detail?.Victim is null)
        {
            return false;
        }

        if (!PassLocation(detail.SolarSystemId)
            || !PassShip(detail.Victim.ShipTypeId)
            || !PassVictim(detail.Victim.CharacterId, detail.Victim.CorporationId, detail.Victim.AllianceId))
        {
            return false;
        }

        var attackerIds = detail.Attackers?
            .SelectMany(a => new[] { a.CharacterId, a.CorporationId, a.AllianceId })
            ?? Enumerable.Empty<int>();
        return PassAny(_attackerMode, _attackerIds, attackerIds);
    }

    private bool PassShip(int typeId)
    {
        if (_shipMode == ZkbFilterMode.None || _shipTypes.Count == 0)
        {
            return true;
        }

        var hit = _shipTypes.Contains(typeId);
        return _shipMode == ZkbFilterMode.Include ? hit : !hit;
    }

    private bool PassLocation(int systemId)
    {
        if (_locationMode == ZkbFilterMode.None
            || (_locationSystems.Count == 0 && _locationRegions.Count == 0))
        {
            return true;
        }

        var regionId = _needRegionLookup ? ResolveRegion(systemId) : 0;
        var hit = _locationSystems.Contains(systemId) || _locationRegions.Contains(regionId);
        return _locationMode == ZkbFilterMode.Include ? hit : !hit;
    }

    private bool PassVictim(int characterId, int corporationId, int allianceId)
    {
        if (_victimMode == ZkbFilterMode.None || _victimIds.Count == 0)
        {
            return true;
        }

        var hit = _victimIds.Contains(characterId)
                  || _victimIds.Contains(corporationId)
                  || _victimIds.Contains(allianceId);
        return _victimMode == ZkbFilterMode.Include ? hit : !hit;
    }

    private static bool PassAny(ZkbFilterMode mode, FilterSet set, IEnumerable<int> ids)
    {
        if (mode == ZkbFilterMode.None || set.Count == 0)
        {
            return true;
        }

        var hit = false;
        foreach (var id in ids)
        {
            if (set.Contains(id))
            {
                hit = true;
                break;
            }
        }

        return mode == ZkbFilterMode.Include ? hit : !hit;
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

    private static void AddLocations(IEnumerable<IdName>? items, FilterSet systems, FilterSet regions)
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
            }
        }
    }

    private static void AddShipTypes(IEnumerable<IdName>? items, FilterSet types)
    {
        if (items is null)
        {
            return;
        }

        foreach (var item in items)
        {
            if (item.GetCategory() == IdName.CategoryEnum.InventoryType)
            {
                types.Add(item.Id);
            }
        }
    }

    private static void AddRoles(IEnumerable<IdName>? items, FilterSet ids)
    {
        if (items is null)
        {
            return;
        }

        foreach (var item in items)
        {
            if (item.GetCategory() is IdName.CategoryEnum.Character
                or IdName.CategoryEnum.Corporation
                or IdName.CategoryEnum.Alliance)
            {
                ids.Add(item.Id);
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
