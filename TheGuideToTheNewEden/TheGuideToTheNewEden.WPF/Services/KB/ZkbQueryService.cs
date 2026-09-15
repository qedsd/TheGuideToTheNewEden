using System.Collections.Concurrent;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Models.KB;
using ZKB.NET;
using ZKB.NET.Models.Killmails;
using ZKB.NET.Models.KillStream;
using ZKB.NET.Models.Statistics;

namespace TheGuideToTheNewEden.WPF.Services.KB;

/// <summary>
/// ZKB 数据访问层：实体统计、击杀列表（分页）、单条/批量 killmail、实体搜索、统计富化。
///
/// 这一层是 WinUI 版所没有的（WinUI 把 ZKB 调用散在 <c>KBNavigationService</c> 与各页 code-behind/VM 里）。
/// 约定与本项目其它服务一致：<b>全部 async + CancellationToken，失败返回 null 并 Log.Error，不向上抛</b>。
///
/// 相对 WinUI 的改进：
/// <list type="bullet">
///   <item>分页换算与"是否有下一页"显式建模，去掉散落的魔法数 <c>Skip((page % 4 - 1) * 50)</c>；</item>
///   <item>批量取 killmail 用<b>有界并发</b>，替代 WinUI 里被注释掉并行后改成串行 <c>foreach + await</c>；</item>
///   <item>实体搜索集中到服务层（WinUI 放在页面 code-behind 的 <c>Task.Run</c> 里）并缓存"舰船分组"集合；</item>
///   <item>统计结果带 2 分钟内存缓存，切页签不再重复请求（可用 <see cref="ClearStatisticCache"/> 或 forceRefresh 绕过）。</item>
/// </list>
/// </summary>
public static class ZkbQueryService
{
    /// <summary>zkillboard kills 接口单页最多返回 200 条。</summary>
    private const int ZkbPageSize = 200;

    /// <summary>界面每页展示条数。</summary>
    private const int UiPageSize = 50;

    /// <summary>
    /// 批量富化的并发度。**必须串行**：Core 的名称/星系查询共用同一个 SQLite 连接且非并发安全
    /// （Core 的 KBHelpers 注释原文："使用一个线程来执行查询KB具体信息，避免ESI查名字时数据库冲突"），
    /// 并发跑会出现部分条目失败（实测 6 并发时 50 条丢 16 条）。
    /// </summary>
    private const int DefaultConcurrency = 1;

    /// <summary>
    /// 名称批量解析的分块大小。ESI <c>/universe/names</c> 单次上限 1000 个 ID，
    /// 取 500 留出余量（同时也避开 SQLite 的 IN 变量数上限）。
    /// </summary>
    private const int ResolveChunkSize = 500;

    private static readonly TimeSpan StatisticTtl = TimeSpan.FromMinutes(2);

    private static readonly ConcurrentDictionary<(EntityType Type, int Id), (EntityStatistic Value, DateTime ExpiresUtc)>
        StatisticCache = new();

    /// <summary>"舰船"大类对应的 invGroups（与 WinUI 的硬编码组一致）。</summary>
    private static readonly Lazy<HashSet<int>> ShipGroupIds = new(() =>
    {
        try
        {
            return Core.Services.DB.InvGroupService
                .QueryGroupIdOfCategory(new List<int> { 3, 6, 22, 23, 65, 87 })
                .ToHashSet();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return [];
        }
    });

    // ==================================================================
    //  实体统计
    // ==================================================================

    /// <summary>取实体统计（带内存缓存，失败返回 null）。</summary>
    public static async Task<EntityStatistic?> GetStatisticAsync(
        EntityType entityType,
        int id,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return null;
        }

        var key = (entityType, id);
        if (!forceRefresh && StatisticCache.TryGetValue(key, out var cached) && cached.ExpiresUtc > DateTime.UtcNow)
        {
            return cached.Value;
        }

        try
        {
            var statistic = await ZKB.NET.ZKB.GetStatisticAsync(entityType, id).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            if (statistic is not null)
            {
                StatisticCache[key] = (statistic, DateTime.UtcNow.Add(StatisticTtl));
            }

            return statistic;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    /// <summary>清空统计内存缓存。</summary>
    public static void ClearStatisticCache() => StatisticCache.Clear();

    // ==================================================================
    //  击杀列表 / killmail
    // ==================================================================

    /// <summary>
    /// 取某个实体的击杀列表（分页）。<paramref name="page"/> 从 1 开始；
    /// <paramref name="modifier"/> 为 null 表示不过滤（对应"所有"）。
    /// </summary>
    public static async Task<KillmailPage?> GetEntityKillmailsAsync(
        EntityType entityType,
        int id,
        int page,
        TypeModifier? modifier = null,
        CancellationToken ct = default)
    {
        if (id <= 0 || page < 1)
        {
            return null;
        }

        try
        {
            var pagesPerZkbPage = ZkbPageSize / UiPageSize;         // 4
            var zkbPage = (page - 1) / pagesPerZkbPage + 1;          // 1,1,1,1,2,2,2,2,…
            var offset = (page - 1) % pagesPerZkbPage * UiPageSize;  // 0,50,100,150,0,…

            var modifiers = new List<ParamModifierData>
            {
                new(ZkbMapping.ToParamModifier(entityType), id.ToString()),
                new(ParamModifier.Page, zkbPage.ToString()),
            };

            var typeModifiers = modifier is null ? Array.Empty<TypeModifier>() : new[] { modifier.Value };

            // 首选：zkillboard 的 /kills/ 响应里已带完整 killmail（attackers/victim/时间/星系），
            // 直接解析后本地富化即可 —— 省掉"每页 N 次 ESI 往返"，也避开 KBHelpers 里
            // DepthClone 跨模型拷贝（EVEStandard 用 snake_case 命名策略、ZKB.NET 用 JsonProperty）
            // 造成 detail 全为默认值的问题（那会让列表看起来"没数据"、且 killID 匹配不上）。
            var details = await GetKillmailDetailsWithRetryAsync(modifiers.ToArray(), typeModifiers, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            if (details is { Count: > 0 })
            {
                var slice = details
                    .OrderByDescending(p => p.KillmailTime)
                    .Skip(offset)
                    .Take(UiPageSize)
                    .ToList();

                var enriched = await EnrichDetailsAsync(slice, ct).ConfigureAwait(false);

                // 该 zkb 页取满 200 条时可能还有后续页；若本页切片没截到尾巴也说明还有内容
                var hasNext = details.Count >= ZkbPageSize || details.Count > offset + UiPageSize;

                Core.Log.Info($"[ZKB] 击杀列表 {entityType}/{id} 第{page}页：抓到 {details.Count} 条，本页富化 {enriched.Count} 条");
                return new KillmailPage { Items = enriched, HasNext = hasNext };
            }

            // 兜底：响应里只有 killmail_id + zkb（没有完整 killmail）→ 走老的 ESI 逐条换取
            Core.Log.Warn($"[ZKB] {entityType}/{id} 第{page}页响应不含完整 killmail，回退 ESI 逐条取");
            var killmaills = await ZKB.NET.ZKB.GetKillmaillAsync(modifiers.ToArray(), typeModifiers).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            if (killmaills is null)
            {
                return null;
            }

            var infos = await Core.Helpers.KBHelpers.CreateKBItemInfoAsync(killmaills).ConfigureAwait(false)
                        ?? new List<KBItemInfo>();

            // 兜底路径经 DepthClone 跨模型拷贝，产出可能是全默认值的"空壳"（KillmailId=0），
            // 这种行在界面上表现为全空，直接丢弃
            infos = infos.Where(p => p.SKBDetail.KillmailId > 0).ToList();

            var items = infos
                .OrderByDescending(p => p.SKBDetail.KillmailTime)
                .Skip(offset)
                .Take(UiPageSize)
                .ToList();

            var legacyHasNext = killmaills.Count >= ZkbPageSize || killmaills.Count > offset + UiPageSize;

            return new KillmailPage { Items = items, HasNext = legacyHasNext };
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    /// <summary>
    /// 把 zkillboard 直接返回的完整 killmail 富化为 <see cref="KBItemInfo"/>（用本地 SDE 补名称/星系/船型），
    /// 有界并发；单条失败只丢该条。
    /// </summary>
    /// <summary>
    /// 把 zkillboard 直接返回的完整 killmail 富化为 <see cref="KBItemInfo"/>（用本地 SDE 补名称/星系/船型）。
    ///
    /// <para>
    /// <b>批量去重是关键</b>：整个战场/军团同一批击杀涉及的角色/军团/联盟/星系/船型高度重叠，
    /// 早期实现是"逐条调 <c>KBHelpers.CreateKBItemInfo</c>"（每条各查一次 SQLite，甚至各发一次 ESI），
    /// 50 条 = 最多 50 次查询且**串行**（Core 的 SQLite 非并发安全，并发度只能是 1）——
    /// 这就是"击杀人数多时卡一会儿"的主因。现在改成：先汇总全部 ID **一次性解析**，
    /// 再用字典本地组装，查询次数与条数不再成正比。
    /// </para>
    /// </summary>
    private static async Task<List<KBItemInfo>> EnrichDetailsAsync(IReadOnlyList<SKBDetail> details, CancellationToken ct)
    {
        if (details.Count == 0)
        {
            return [];
        }

        // 1) 汇总这一页所有 killmail 需要的 ID（角色/军团/联盟 + 星系 + 船型）
        var nameIds = new HashSet<int>();
        var systemIds = new HashSet<int>();
        var typeIds = new HashSet<int>();

        foreach (var detail in details)
        {
            AddNameIds(nameIds, detail.Victim?.CharacterId);
            AddNameIds(nameIds, detail.Victim?.CorporationId);
            AddNameIds(nameIds, detail.Victim?.AllianceId);

            var finalBlow = detail.Attackers?.FirstOrDefault(p => p.FinalBlow);
            if (finalBlow is not null)
            {
                AddNameIds(nameIds, finalBlow.CharacterId);
                AddNameIds(nameIds, finalBlow.CorporationId);
                AddNameIds(nameIds, finalBlow.AllianceId);
            }

            if (detail.SolarSystemId > 0)
            {
                systemIds.Add(detail.SolarSystemId);
            }

            if (detail.Victim?.ShipTypeId > 0)
            {
                typeIds.Add(detail.Victim.ShipTypeId);
            }
        }

        // 2) 一次性解析（去重后通常只有几十~几百个，与页大小无关）。
        //    星域 / 类别要二次收集：regionId 藏在星系里、groupId 藏在船型里，只有查出来才知道，同样只查一次
        var names = await ResolveNamesAsync(nameIds.ToList()).ConfigureAwait(false);
        var systems = await Task.Run(() => QuerySystems(systemIds), ct).ConfigureAwait(false);
        var types = await Task.Run(() => QueryTypes(typeIds), ct).ConfigureAwait(false);
        var regions = await Task.Run(() => QueryRegions(systems.Values.Select(p => p.RegionID)), ct).ConfigureAwait(false);
        var groups = await Task.Run(() => QueryGroups(types.Values.Select(p => p.GroupID)), ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        // 3) 本地组装，不再有任何查询
        var results = new List<KBItemInfo>(details.Count);
        foreach (var detail in details)
        {
            try
            {
                results.Add(BuildFromResolved(detail, names, systems, types, regions, groups));
            }
            catch (Exception ex)
            {
                // 单条组装失败只丢该条
                Core.Log.Error(ex);
            }
        }

        return results;
    }

    private static void AddNameIds(HashSet<int> ids, int? id)
    {
        if (id is > 0)
        {
            ids.Add(id.Value);
        }
    }

    private static Dictionary<int, Core.DBModels.MapSolarSystem> QuerySystems(HashSet<int> systemIds)
    {
        if (systemIds.Count == 0)
        {
            return [];
        }

        try
        {
            return Core.Services.DB.MapSolarSystemService
                .Query(systemIds.ToList())
                .Where(p => p is not null)
                .ToDictionary(p => p.SolarSystemID, p => p);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return [];
        }
    }

    private static Dictionary<int, Core.DBModels.InvType> QueryTypes(HashSet<int> typeIds)
    {
        if (typeIds.Count == 0)
        {
            return [];
        }

        try
        {
            return Core.Services.DB.InvTypeService
                .QueryTypes(typeIds.ToList())
                .Where(p => p is not null)
                .ToDictionary(p => p.TypeID, p => p);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return [];
        }
    }

    private static Dictionary<int, Core.DBModels.MapRegion> QueryRegions(IEnumerable<int> regionIds)
    {
        var ids = regionIds.Where(p => p > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        try
        {
            return Core.Services.DB.MapRegionService
                .Query(ids)
                .Where(p => p is not null)
                .ToDictionary(p => p.RegionID, p => p);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return [];
        }
    }

    private static Dictionary<int, Core.DBModels.InvGroup> QueryGroups(IEnumerable<int> groupIds)
    {
        var ids = groupIds.Where(p => p > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        try
        {
            return Core.Services.DB.InvGroupService
                .QueryGroups(ids)
                .Where(p => p is not null)
                .ToDictionary(p => p.GroupID, p => p);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return [];
        }
    }

    /// <summary>用已解析好的名称/星系/船型/星域/类别字典组装单条 <see cref="KBItemInfo"/>（纯内存操作）。</summary>
    private static KBItemInfo BuildFromResolved(
        SKBDetail detail,
        Dictionary<int, IdName> names,
        Dictionary<int, Core.DBModels.MapSolarSystem> systems,
        Dictionary<int, Core.DBModels.InvType> types,
        Dictionary<int, Core.DBModels.MapRegion> regions,
        Dictionary<int, Core.DBModels.InvGroup> groups)
    {
        var info = new KBItemInfo(detail);

        IdName? Get(int? id) => id is > 0 && names.TryGetValue(id.Value, out var found) ? found : null;

        info.VictimCharacterName = Get(detail.Victim?.CharacterId);
        info.VictimCorporationIdName = Get(detail.Victim?.CorporationId);
        info.VictimAllianceName = Get(detail.Victim?.AllianceId);
        info.VictimFctionName = info.VictimAllianceName ?? info.VictimCorporationIdName;

        var finalBlow = detail.Attackers?.FirstOrDefault(p => p.FinalBlow);
        if (finalBlow is not null)
        {
            info.FinalBlowCharacterName = Get(finalBlow.CharacterId);
            info.FinalBlowCorporationIdName = Get(finalBlow.CorporationId);
            info.FinalBlowAllianceName = Get(finalBlow.AllianceId);
            info.FinalBlowFctionName = info.FinalBlowAllianceName ?? info.FinalBlowCorporationIdName;
        }

        if (detail.SolarSystemId > 0 && systems.TryGetValue(detail.SolarSystemId, out var system))
        {
            info.SolarSystem = system;

            // 星域：由星系的 RegionID 关联（原 KBHelpers 也是这么补的，批量版一度漏掉 → 列表星域空白）
            if (regions.TryGetValue(system.RegionID, out var region))
            {
                info.Region = region;
            }
        }

        if (detail.Victim?.ShipTypeId > 0 && types.TryGetValue(detail.Victim.ShipTypeId, out var type))
        {
            info.Type = type;

            // 类别：由船型的 GroupID 关联（同上，批量版一度漏掉）
            if (groups.TryGetValue(type.GroupID, out var group))
            {
                info.Group = group;
            }
        }

        return info;
    }


    /// <summary>取单条 killmail 的富化信息（失败返回 null）。</summary>
    public static Task<KBItemInfo?> GetKillmailAsync(int killmailId, CancellationToken ct = default)
        => GetKillmailInternalAsync(killmailId, logErrors: true, ct);

    /// <summary>
    /// 批量取 killmail 并按<paramref name="killmailIds"/>的顺序返回（有界并发）。
    /// 单条失败只记日志、不影响整体。
    /// </summary>
    public static async Task<List<KBItemInfo>> GetKillmailsByIdsAsync(
        IReadOnlyList<int> killmailIds,
        int concurrency = DefaultConcurrency,
        CancellationToken ct = default)
    {
        if (killmailIds.Count == 0)
        {
            return [];
        }

        var results = new KBItemInfo?[killmailIds.Count];
        using var gate = new SemaphoreSlim(Math.Clamp(concurrency, 1, 16));

        var tasks = new List<Task>(killmailIds.Count);
        for (var i = 0; i < killmailIds.Count; i++)
        {
            var index = i;
            var killmailId = killmailIds[index];
            tasks.Add(Task.Run(async () =>
            {
                await gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    if (!ct.IsCancellationRequested)
                    {
                        results[index] = await GetKillmailInternalAsync(killmailId, logErrors: false, ct).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    // 取消即返回已取到的部分
                }
                catch (Exception ex)
                {
                    Core.Log.Error(ex);
                }
                finally
                {
                    gate.Release();
                }
            }, CancellationToken.None));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.Where(p => p is not null).Select(p => p!).ToList();
    }

    private static async Task<KBItemInfo?> GetKillmailInternalAsync(int killmailId, bool logErrors, CancellationToken ct)
    {
        if (killmailId <= 0)
        {
            return null;
        }

        try
        {
            var modifiers = new[]
            {
                new ParamModifierData(ParamModifier.KillID, killmailId.ToString()),
            };

            // 与列表路径同理：优先直接用响应里的完整 killmail（省的 ESI 往返 + 避免 DepthClone 拷空）
            var details = await GetKillmailDetailsWithRetryAsync(modifiers, Array.Empty<TypeModifier>(), ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            if (details is { Count: > 0 })
            {
                var detail = details[0];
                return await Task.Run(() => Core.Helpers.KBHelpers.CreateKBItemInfo(detail), ct).ConfigureAwait(false);
            }

            var killmaills = await ZKB.NET.ZKB.GetKillmaillAsync(modifiers).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            if (killmaills is not { Count: > 0 })
            {
                return null;
            }

            var infos = await Core.Helpers.KBHelpers.CreateKBItemInfoAsync(killmaills).ConfigureAwait(false);
            // 兜底路径可能产出 DepthClone 空壳（KillmailId=0），丢弃
            return infos?.FirstOrDefault(p => p.SKBDetail.KillmailId > 0);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            if (logErrors)
            {
                Core.Log.Error(ex);
            }

            return null;
        }
    }

    /// <summary>
    /// 直取完整 killmail；网络/限流等偶发失败时**重试一次**（与本项目"单页失败重试一次"的约定一致）。
    /// 两次都失败时返回 null（调用方走兜底路径），并记录日志——**这里绝不能静默**，
    /// 否则"最贵击杀/详情页"空数据时无从排查（本次排障教训）。
    /// </summary>
    private static async Task<List<SKBDetail>?> GetKillmailDetailsWithRetryAsync(
        ParamModifierData[] modifiers,
        TypeModifier[] typeModifiers,
        CancellationToken ct)
    {
        var descriptor = string.Join("/", modifiers.Select(m => $"{m.Modifier}:{m.Param}"));

        List<SKBDetail>? first = null;
        Exception? firstError = null;
        try
        {
            first = await ZKB.NET.ZKB.GetKillmailDetailsAsync(modifiers, typeModifiers).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            firstError = ex;
            Core.Log.Warn($"[ZKB] kills 直取失败（{descriptor}），重试一次：{ex.Message}");
        }

        if (first is { Count: > 0 })
        {
            return first;
        }

        try
        {
            await Task.Delay(500, ct).ConfigureAwait(false);
            var second = await ZKB.NET.ZKB.GetKillmailDetailsAsync(modifiers, typeModifiers).ConfigureAwait(false);
            if (second is { Count: > 0 })
            {
                return second;
            }

            Core.Log.Warn($"[ZKB] kills 直取两次均为空（{descriptor}），回退 ESI 逐条路径");
            return second;
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"[ZKB] kills 直取重试仍失败（{descriptor}）：{ex.Message}；首次：{firstError?.Message}");
            throw;
        }
    }

    // ==================================================================
    //  实体搜索（用于主导航页的搜索框）
    // ==================================================================

    /// <summary>
    /// 按关键字搜索可作为 ZKB 查询对象的实体：舰船 / 物品组 / 星系 / 星域 + 角色/军团/联盟（本地库 + ESI）。
    /// 结果已过滤为 ZKB 支持的类别（星座、空间站、结构等会被剔除）。
    /// </summary>
    public static Task<List<IdName>> SearchEntitiesAsync(string? keyword, CancellationToken ct = default)
    {
        keyword = keyword?.Trim();
        return string.IsNullOrEmpty(keyword)
            ? Task.FromResult(new List<IdName>())
            : Task.Run(() => SearchEntities(keyword, ct), ct);
    }

    private static List<IdName> SearchEntities(string keyword, CancellationToken ct)
    {
        var result = new List<IdName>();
        var seen = new HashSet<int>();

        void Add(IdName? item)
        {
            if (item is not null && item.Id > 0 && seen.Add(item.Id))
            {
                result.Add(item);
            }
        }

        // 舰船 / 可攻击建筑（物品里属"舰船"大类的那些）
        try
        {
            var typesSearch = Core.Services.DB.InvTypeService.Search(keyword);
            if (typesSearch is { Count: > 0 })
            {
                var types = Core.Services.DB.InvTypeService.QueryTypes(typesSearch.Select(p => p.ID).ToList());
                var shipGroups = ShipGroupIds.Value;
                foreach (var type in types.Where(t => shipGroups.Contains(t.GroupID)))
                {
                    Add(new IdName(type.TypeID, type.TypeName, IdName.CategoryEnum.InventoryType));
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        if (ct.IsCancellationRequested)
        {
            return result;
        }

        // 物品组
        try
        {
            foreach (var group in Core.Services.DB.InvGroupService.Search(keyword) ?? [])
            {
                Add(new IdName(group.ID, group.Name, IdName.CategoryEnum.Group));
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        if (ct.IsCancellationRequested)
        {
            return result;
        }

        // 星系
        try
        {
            foreach (var system in Core.Services.DB.MapSolarSystemService.Search(keyword) ?? [])
            {
                Add(new IdName(system.ID, system.Name, IdName.CategoryEnum.SolarSystem));
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        // 星域
        try
        {
            foreach (var region in Core.Services.DB.MapRegionService.Search(keyword) ?? [])
            {
                Add(new IdName(region.ID, region.Name, IdName.CategoryEnum.Region));
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        if (ct.IsCancellationRequested)
        {
            return result;
        }

        // 角色 / 军团 / 联盟（本地 IdName 库 + ESI，只保留 ZKB 支持的类别）
        try
        {
            foreach (var name in Core.Services.IDNameService.SerachByName(keyword) ?? [])
            {
                if (ZkbMapping.IsSupported(name.GetCategory()))
                {
                    Add(name);
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        return result;
    }

    // ==================================================================
    //  统计富化（把 ZKB 的原始统计变成界面可直接绑定的模型）
    // ==================================================================

    /// <summary>实体基本信息卡（身份 + 归属 + 成员/安全等级等）。</summary>
    public static async Task<EntityBaseInfo> BuildEntityBaseInfoAsync(EntityStatistic statistic, CancellationToken ct = default)
    {
        var category = ZkbMapping.ToCategory(statistic.StatisticType);
        var id = statistic.Id;

        var info = new EntityBaseInfo { Type = category, Name = await ResolveIdNameAsync(id).ConfigureAwait(false) };
        if (info.Name is null)
        {
            // 名称解析失败（例如从未缓存过的玩家）也构造一个占位，保证界面有标题
            info.Name = new IdName(id, id.ToString(), category);
        }

        try
        {
            switch (category)
            {
                case IdName.CategoryEnum.Character:
                    await FillCharacterAsync(info, id).ConfigureAwait(false);
                    break;
                case IdName.CategoryEnum.Corporation:
                    await FillCorporationAsync(info, id).ConfigureAwait(false);
                    break;
                case IdName.CategoryEnum.Alliance:
                    await FillAllianceAsync(info, id).ConfigureAwait(false);
                    break;
                case IdName.CategoryEnum.SolarSystem:
                    await FillSolarSystemAsync(info, id).ConfigureAwait(false);
                    break;
                case IdName.CategoryEnum.InventoryType:
                    await FillShipAsync(info, id).ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        // 与 WinUI 一致：军团/联盟的成员数兜底取 ZKB 统计自带值（联盟的 ESI 信息不含成员数）
        if (info.Members is null
            && category is IdName.CategoryEnum.Corporation or IdName.CategoryEnum.Alliance
            && statistic.Info is not null)
        {
            info.Members = statistic.Info.MemberCount;
        }

        return info;
    }

    private static async Task FillCharacterAsync(EntityBaseInfo info, int characterId)
    {
        info.CharacterName = info.Name;

        var affiliation = await ESIService.Current.EsiClient.Character
            .AffiliationAsync(new List<long> { characterId })
            .ConfigureAwait(false);

        var entry = affiliation?.Model?.FirstOrDefault();
        if (entry is null)
        {
            return;
        }

        info.CorpName = await ResolveIdNameAsync(entry.CorporationId).ConfigureAwait(false);
        if (entry.AllianceId is > 0)
        {
            info.AllianceName = await ResolveIdNameAsync(entry.AllianceId.Value).ConfigureAwait(false);
        }
    }

    private static async Task FillCorporationAsync(EntityBaseInfo info, int corporationId)
    {
        var corp = await FetchEsiAsync(() => ESIService.Current.EsiClient.Corporation
            .GetCorporationInfoAsync(corporationId)).ConfigureAwait(false);

        if (corp is null)
        {
            return;
        }

        info.CEOName = await ResolveIdNameAsync(corp.CeoId).ConfigureAwait(false);
        info.Members = (int)corp.MemberCount;
        if (corp.AllianceId is > 0)
        {
            info.AllianceName = await ResolveIdNameAsync(corp.AllianceId.Value).ConfigureAwait(false);
        }
    }

    private static async Task FillAllianceAsync(EntityBaseInfo info, int allianceId)
    {
        var alliance = await FetchEsiAsync(() => ESIService.Current.EsiClient.Alliance
            .GetAllianceInfoAsync(allianceId)).ConfigureAwait(false);

        if (alliance is null)
        {
            return;
        }

        if (alliance.ExecutorCorporationId is long executorId && executorId > 0)
        {
            info.ExecutorCorpName = await ResolveIdNameAsync(executorId).ConfigureAwait(false);
        }
    }

    private static async Task FillSolarSystemAsync(EntityBaseInfo info, int systemId)
    {
        var system = await Core.Services.DB.MapSolarSystemService.QueryAsync(systemId).ConfigureAwait(false);
        if (system is null)
        {
            return;
        }

        info.Sec = (float)system.Security;

        var region = await Core.Services.DB.MapRegionService.QueryAsync(system.RegionID).ConfigureAwait(false);
        if (region is not null)
        {
            info.RegionName = new IdName(region.RegionID, region.RegionName, IdName.CategoryEnum.Region);
        }
    }

    private static async Task FillShipAsync(EntityBaseInfo info, int typeId)
    {
        var type = await Core.Services.DB.InvTypeService.QueryTypeAsync(typeId).ConfigureAwait(false);
        if (type is null)
        {
            return;
        }

        info.ShipName = new IdName(type.TypeID, type.TypeName, IdName.CategoryEnum.InventoryType);

        var group = await Core.Services.DB.InvGroupService.QueryGroupAsync(type.GroupID).ConfigureAwait(false);
        if (group is not null)
        {
            info.ClassName = new IdName(group.GroupID, group.GroupName, IdName.CategoryEnum.Group);
        }
    }

    /// <summary>「分类统计」页：按舰船组统计击杀/损失（组名来自本地 SDE）。</summary>
    public static async Task<List<GroupDataInfo>> BuildGroupStatistAsync(EntityStatistic statistic, CancellationToken ct = default)
    {
        var groups = statistic.Groups;
        if (groups is not { Count: > 0 })
        {
            return [];
        }

        Dictionary<int, string> names = new();
        try
        {
            var invGroups = await Core.Services.DB.InvGroupService
                .QueryGroupsAsync(groups.Select(p => p.GroupID).Distinct().ToList())
                .ConfigureAwait(false);

            names = invGroups.ToDictionary(p => p.GroupID, p => p.GroupName);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        var list = groups
            .OrderByDescending(p => p.ItemDestroyed)
            .Select(g => new GroupDataInfo(g)
            {
                Name = names.TryGetValue(g.GroupID, out var name) ? name : g.GroupID.ToString(),
            })
            .ToList();

        for (var i = 0; i < list.Count; i++)
        {
            list[i].No = i + 1;
        }

        return list;
    }

    /// <summary>「超期击杀」页：泰坦 / 大航 击杀榜（角色）。</summary>
    public static async Task<List<KillCardItem>> BuildSuperKillsAsync(ZKB.NET.Models.Statistics.SuperData? data, CancellationToken ct = default)
    {
        if (data?.Datas is not { Count: > 0 })
        {
            return [];
        }

        var ordered = data.Datas.OrderByDescending(p => p.Kills).ToList();
        var names = await ResolveNamesAsync(ordered.Select(p => p.Id).ToList()).ConfigureAwait(false);

        var list = new List<KillCardItem>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var kill = ordered[i];
            var name = names.TryGetValue(kill.Id, out var idName) ? idName.Name : kill.Id.ToString();
            list.Add(new KillCardItem
            {
                No = i + 1,
                Name = name,
                EntityId = kill.Id,
                EntityCategory = IdName.CategoryEnum.Character,
                ImageUrl = GameImageHelper.BuildCharacterPortraitUrl(kill.Id, 64),
                Kills = kill.Kills,
            });
        }

        return list;
    }

    /// <summary>「最贵击杀」页：按给定的 killmail ID 列表（保持顺序）取回并展示。ID 顺序即排名顺序。</summary>
    public static async Task<List<KillCardItem>> BuildTopValueAsync(IReadOnlyList<int> killmailIds, CancellationToken ct = default)
    {
        if (killmailIds.Count == 0)
        {
            return [];
        }

        var ids = killmailIds.Where(p => p > 0).Distinct().ToList();
        var infos = await GetKillmailsByIdsAsync(ids, ct: ct).ConfigureAwait(false);
        var byId = infos
            .GroupBy(p => (int)p.SKBDetail.KillmailId)
            .ToDictionary(g => g.Key, g => g.First());

        var list = new List<KillCardItem>(ids.Count);
        var no = 1;
        foreach (var killmailId in ids)
        {
            if (!byId.TryGetValue(killmailId, out var info))
            {
                continue;
            }

            var totalValue = info.SKBDetail.Zkb?.TotalValue ?? 0;
            list.Add(new KillCardItem
            {
                No = no++,
                Name = info.Victim?.Name ?? killmailId.ToString(),
                SubTitle = info.Type?.TypeName,
                ImageUrl = GameImageHelper.BuildTypeImageUrl(info.SKBDetail.Victim.ShipTypeId, 64),
                KillmailId = killmailId,
                Kills = 0,
                Value = totalValue,
                ValueText = IskFormatHelper.Format(totalValue),
            });
        }

        // 不能静默丢：否则"最贵击杀"出现 0 条时完全无从排查
        if (list.Count < ids.Count)
        {
            Core.Log.Warn($"[ZKB] 最贵击杀：{ids.Count} 个 killID 只取回 {list.Count} 条");
        }

        return list;
    }

    /// <summary>「最高击杀」页：按角色/军团/联盟/势力/舰船/星系分组的历史击杀榜。</summary>
    public static async Task<List<KillStatisticGroup>> BuildTopAllTimeAsync(EntityStatistic statistic, CancellationToken ct = default)
    {
        var topAllTime = statistic.TopAllTime;
        if (topAllTime is not { Count: > 0 })
        {
            return [];
        }

        var groups = new List<KillStatisticGroup>(topAllTime.Count);

        foreach (var stat in topAllTime)
        {
            if (stat.Datas is not { Count: > 0 })
            {
                continue;
            }

            var ordered = stat.Datas.OrderByDescending(p => p.Kills).ToList();
            var items = await BuildTopAllTimeItemsAsync(stat.Type, ordered).ConfigureAwait(false);

            groups.Add(new KillStatisticGroup
            {
                Title = FindString(TopAllTimeTitleKey(stat.Type)),
                Items = items,
            });
        }

        return groups;
    }

    private static async Task<List<KillCardItem>> BuildTopAllTimeItemsAsync(string type, List<KillData> ordered)
    {
        var ids = ordered.Select(p => p.Id).ToList();
        var items = new List<KillCardItem>(ordered.Count);

        switch (type)
        {
            case "shipType":
            case "ship":
            {
                var types = await Task.Run(() => Core.Services.DB.InvTypeService.QueryTypes(ids)).ConfigureAwait(false);
                var byId = types.ToDictionary(p => p.TypeID);
                for (var i = 0; i < ordered.Count; i++)
                {
                    var kill = ordered[i];
                    var name = byId.TryGetValue(kill.Id, out var t) ? t.TypeName : kill.Id.ToString();
                    items.Add(new KillCardItem
                    {
                        No = i + 1,
                        Name = name,
                        EntityId = kill.Id,
                        EntityCategory = IdName.CategoryEnum.InventoryType,
                        ImageUrl = GameImageHelper.BuildTypeImageUrl(kill.Id, 64),
                        Kills = kill.Kills,
                    });
                }

                break;
            }

            case "solarSystem":
            case "system":
            {
                var systems = await Core.Services.DB.MapSolarSystemService.QueryAsync(ids).ConfigureAwait(false);
                var byId = systems.ToDictionary(p => p.SolarSystemID);
                for (var i = 0; i < ordered.Count; i++)
                {
                    var kill = ordered[i];
                    var name = byId.TryGetValue(kill.Id, out var s) ? s.SolarSystemName : kill.Id.ToString();
                    items.Add(new KillCardItem
                    {
                        No = i + 1,
                        Name = name,
                        EntityId = kill.Id,
                        EntityCategory = IdName.CategoryEnum.SolarSystem,
                        Kills = kill.Kills,
                    });
                }

                break;
            }

            default:
            {
                var category = type switch
                {
                    "corporation" => IdName.CategoryEnum.Corporation,
                    "alliance" => IdName.CategoryEnum.Alliance,
                    "faction" => IdName.CategoryEnum.Faction,
                    _ => IdName.CategoryEnum.Character,
                };

                var names = await ResolveNamesAsync(ids).ConfigureAwait(false);
                for (var i = 0; i < ordered.Count; i++)
                {
                    var kill = ordered[i];
                    var name = names.TryGetValue(kill.Id, out var idName) ? idName.Name : kill.Id.ToString();
                    items.Add(new KillCardItem
                    {
                        No = i + 1,
                        Name = name,
                        EntityId = kill.Id,
                        EntityCategory = category,
                        ImageUrl = GameImageHelper.BuildEntityImageUrl(category, kill.Id, 64),
                        Kills = kill.Kills,
                    });
                }

                break;
            }
        }

        return items;
    }

    private static string TopAllTimeTitleKey(string type) => type switch
    {
        "corporation" => "StatistTopAllTimePage_Corporation",
        "alliance" => "StatistTopAllTimePage_Alliance",
        "faction" => "StatistTopAllTimePage_Faction",
        "shipType" or "ship" => "StatistTopAllTimePage_Ship",
        "solarSystem" or "system" => "StatistTopAllTimePage_System",
        _ => "StatistTopAllTimePage_Character",
    };

    // ==================================================================
    //  KB 详情富化
    // ==================================================================

    /// <summary>攻击者列表（补角色/军团/联盟名与船型/武器名，按伤害降序，附带伤害占比）。</summary>
    public static async Task<List<AttackerInfo>> BuildAttackerInfosAsync(SKBDetail detail, CancellationToken ct = default)
    {
        var attackers = detail.Attackers;
        if (attackers is not { Count: > 0 })
        {
            return [];
        }

        var nameIds = new HashSet<int>();
        var typeIds = new HashSet<int>();

        foreach (var attacker in attackers)
        {
            nameIds.Add(attacker.CharacterId);
            nameIds.Add(attacker.CorporationId);
            nameIds.Add(attacker.AllianceId);
            nameIds.Add(attacker.FactionId);
            typeIds.Add(attacker.ShipTypeId);
            typeIds.Add(attacker.WeaponTypeId);
        }

        nameIds.Remove(0);
        typeIds.Remove(0);

        var names = await ResolveNamesAsync(nameIds.ToList()).ConfigureAwait(false);

        var types = new Dictionary<int, InvType>();
        if (typeIds.Count > 0)
        {
            var list = await Task.Run(() => Core.Services.DB.InvTypeService.QueryTypes(typeIds.ToList()), ct).ConfigureAwait(false);
            types = list.ToDictionary(p => p.TypeID, p => p);
        }

        var totalDamage = attackers.Sum(p => (long)p.DamageDone);

        var result = new List<AttackerInfo>(attackers.Count);
        foreach (var attacker in attackers)
        {
            var info = new AttackerInfo(attacker)
            {
                CharacterName = names.GetValueOrDefault(attacker.CharacterId),
                CorpName = names.GetValueOrDefault(attacker.CorporationId),
                AllianceName = names.GetValueOrDefault(attacker.AllianceId),
                Ship = types.GetValueOrDefault(attacker.ShipTypeId),
                Weapon = types.GetValueOrDefault(attacker.WeaponTypeId),
                DamageRatio = totalDamage > 0 ? (float)((double)attacker.DamageDone / totalDamage) : 0,
            };
            result.Add(info);
        }

        return result.OrderByDescending(p => p.Attacker.DamageDone).ToList();
    }

    /// <summary>货柜物品（递归展开子物品并补名称）。</summary>
    public static async Task<List<CargoItemInfo>> BuildCargoInfosAsync(SKBDetail detail, CancellationToken ct = default)
    {
        var items = detail.Victim?.Items;
        if (items is not { Count: > 0 })
        {
            return [];
        }

        var typeIds = new HashSet<int>();

        void Collect(IEnumerable<ZKB.NET.Models.Killmails.CargoItem> list)
        {
            foreach (var item in list)
            {
                if (item.ItemTypeId > 0)
                {
                    typeIds.Add(item.ItemTypeId);
                }

                if (item.Items is { Count: > 0 })
                {
                    Collect(item.Items);
                }
            }
        }

        Collect(items);

        var types = new Dictionary<int, InvType>();
        if (typeIds.Count > 0)
        {
            var list = await Task.Run(() => Core.Services.DB.InvTypeService.QueryTypes(typeIds.ToList()), ct).ConfigureAwait(false);
            types = list.ToDictionary(p => p.TypeID, p => p);
        }

        return items.Select(Item).ToList();

        CargoItemInfo Item(ZKB.NET.Models.Killmails.CargoItem cargoItem)
        {
            var info = new CargoItemInfo(cargoItem) { Type = types.GetValueOrDefault(cargoItem.ItemTypeId) };
            if (cargoItem.Items is { Count: > 0 })
            {
                info.SubItems = cargoItem.Items.Select(Item).ToList();
            }

            return info;
        }
    }

    // ==================================================================
    //  基础工具
    // ==================================================================

    /// <summary>解析单个 ID 的名称（本地 IdName 库 + ESI），失败返回 null。id 超出 int 范围（结构 ID）直接返回 null。</summary>
    public static async Task<IdName?> ResolveIdNameAsync(long id)
    {
        if (id <= 0 || id > int.MaxValue)
        {
            return null;
        }

        var intId = (int)id;
        try
        {
            var list = await Core.Services.IDNameService.GetByIdsAsync(new List<int> { intId }).ConfigureAwait(false);
            return list?.FirstOrDefault(p => p.Id == intId);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    /// <summary>
    /// 批量解析 ID → 名称。
    ///
    /// <para>
    /// **按 <see cref="ResolveChunkSize"/> 分批**：大战场单条 killmail 的攻击者可达数百人，
    /// 每人 4 个 ID（角色/军团/联盟/势力）时总量会突破 ESI <c>/universe/names</c> 的 **1000 个 ID 上限**
    /// （同时也在逼近 SQLite 的 IN 变量数上限），整批请求会被直接拒绝、名字全空。分批后逐块合并。
    /// </para>
    /// </summary>
    public static async Task<Dictionary<int, IdName>> ResolveNamesAsync(List<int> ids)
    {
        var valid = ids.Where(p => p > 0).Distinct().ToList();
        if (valid.Count == 0)
        {
            return new Dictionary<int, IdName>();
        }

        var result = new Dictionary<int, IdName>(valid.Count);
        foreach (var chunk in valid.Chunk(ResolveChunkSize))
        {
            try
            {
                var list = await Core.Services.IDNameService.GetByIdsAsync(chunk.ToList()).ConfigureAwait(false);
                if (list is null)
                {
                    continue;
                }

                foreach (var name in list)
                {
                    result[name.Id] = name;
                }
            }
            catch (Exception ex)
            {
                // 单批失败不影响其余批次
                Core.Log.Error(ex);
            }
        }

        return result;
    }

    private static async Task<TModel?> FetchEsiAsync<TModel>(Func<Task<EVEStandard.Models.API.ESIModelDTO<TModel>>> call)
        where TModel : class
    {
        try
        {
            return (await call().ConfigureAwait(false)).Model;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    private static string FindString(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? key;
}
