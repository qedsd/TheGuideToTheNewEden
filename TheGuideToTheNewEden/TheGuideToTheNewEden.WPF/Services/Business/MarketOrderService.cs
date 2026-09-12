using System.IO;
using EVEStandard.Enumerations;
using EVEStandard.Models;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.Services.Settings;
using EsiMarketOrder = EVEStandard.Models.MarketOrder;
using MarketOrder = TheGuideToTheNewEden.Core.Models.Market.Order;

namespace TheGuideToTheNewEden.WPF.Services.Business;

/// <summary>
/// 市场订单与历史统计的统一入口。数据来源分三类：
/// <list type="bullet">
///   <item><b>公开市场接口（匿名）</b>：星域订单（指定物品 / 整星域）、星系订单、历史统计；</item>
///   <item><b>建筑订单（需角色授权）</b>：<c>esi-markets.structure_markets.v1</c> 且角色对该建筑有市场访问权；</item>
///   <item><b>本地 SDE</b>：为订单补齐物品类型、位置名、星系与所属星域。</item>
/// </list>
/// 缓存（TTL 取市场设置）：
/// <list type="bullet">
///   <item><c>Configs/RegionOrders/{regionId}.json</c>：整星域订单（仅星域接口结果，不含建筑合并结果）；</item>
///   <item><c>Configs/StructureOrders/{structureId}.json</c>：单个建筑的全量订单；</item>
///   <item><c>Configs/HistoryOrders/{regionId}/{typeId}.json</c>：单物品历史统计。</item>
/// </list>
/// 约定：分页取数统一走 <see cref="FetchOrderPagesAsync"/>（第 1 页取总页数，其余页并发拉取）；
/// 缓存读写统一走 <see cref="ReadJsonFileAsync{T}"/> / <see cref="WriteJsonFileAsync{T}"/> / 新鲜度判断。
/// </summary>
public sealed class MarketOrderService
{
    /// <summary>默认市场星域：伏尔戈（The Forge）。</summary>
    public const int DefaultMarketRegion = 10000002;

    /// <summary>PLEX 等全球物品所在星域。</summary>
    public const int GlobalMarketRegion = 19000001;

    public const int PlexTypeId = 44992;

    private static MarketOrderService? _current;

    public static MarketOrderService Current => _current ??= new MarketOrderService();

    private readonly EVEStandard.EVEStandardAPI _esiClient;

    public MarketOrderService()
    {
        var dataSource = GameServerSelectorService.Value == GameServerType.Tranquility
            ? DataSource.Tranquility
            : DataSource.Serenity;
        _esiClient = new EVEStandard.EVEStandardAPI(
            "TheGuideToTheNewEden",
            dataSource,
            CompatibilityDate.v2025_12_16,
            TimeSpan.FromSeconds(30));
    }

    /// <summary>订单缓存有效期（分钟）。</summary>
    private static int OrderDuration => Math.Max(1, MarketOrderSettingService.OrderDurationValue);

    /// <summary>历史统计缓存有效期（分钟）。</summary>
    private static int HistoryDuration => Math.Max(1, MarketOrderSettingService.HistoryDurationValue);

    /// <summary>批量拉取历史时的并发线程数（至少 1）。</summary>
    private static int MaxThread => Math.Max(1, MarketOrderSettingService.ThreadValue);

    /// <summary>
    /// 并发翻页的线程数。取设置里的线程数，但**上限夹到 16**：同一路由（同星域/同建筑）并发过高
    /// 容易被 ESI/Cloudflare 限流，反而更慢；需要更大并发时可自行调大设置值但不会超过 16。
    /// </summary>
    private static int PageConcurrency => Math.Clamp(MarketOrderSettingService.ThreadValue, 1, 16);

    /// <summary>PLEX 等全球物品统一走全球星域。</summary>
    private static long NormalizeRegion(long typeId, long regionId)
        => typeId == PlexTypeId ? GlobalMarketRegion : regionId;

    // ==================================================================
    //  一、订单查询
    // ==================================================================

    /// <summary>
    /// 获取某星域内指定物品的全部订单（星域接口本身已含空间站与建筑卖单）。
    /// </summary>
    public async Task<List<MarketOrder>?> GetRegionOrdersAsync(long typeId, long regionId, CancellationToken cancellationToken = default)
    {
        regionId = NormalizeRegion(typeId, regionId);
        try
        {
            var orders = await FetchOrderPagesAsync(
                page => ListRegionOrdersPageAsync(regionId, typeId, page),
                cancellationToken);

            if (orders is { Count: > 0 })
            {
                await SetOrderInfoAsync(orders, cancellationToken);
            }

            return orders;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return null;
        }
    }

    /// <summary>获取某建筑（结构）内指定物品的订单（内部拉全量后按 TypeId 过滤）。</summary>
    public async Task<List<MarketOrder>?> GetStructureTypeOrdersAsync(long structureId, long invTypeId, CancellationToken cancellationToken = default)
    {
        var orders = await GetStructureOrdersAsync(structureId, cancellationToken);
        return orders?.Where(p => p.TypeId == invTypeId).ToList();
    }

    /// <summary>
    /// 获取某建筑（结构）内的<b>全部</b>订单。鉴权优先用登记该建筑的角色，否则用默认角色；
    /// 需要 <c>esi-markets.structure_markets.v1</c> 权限且该角色对该建筑有市场访问权，失败返回 null。
    /// 结果按 <c>Configs/StructureOrders/{structureId}.json</c> 缓存（TTL 同订单设置）。
    /// </summary>
    public async Task<List<MarketOrder>?> GetStructureOrdersAsync(long structureId, CancellationToken cancellationToken = default)
    {
        try
        {
            var structure = StructureService.GetStructure(structureId);
            var filePath = Path.Combine(MarketOrderSettingService.StructureOrderFolder, $"{structureId}.json");
            List<MarketOrder>? orders = null;

            // 建筑缓存按文件内 UpdateTime 判 TTL（与 WinUI 版写入的字段兼容）
            var cached = await ReadJsonFileAsync<StructureOrderCache>(filePath);
            if (cached is { Orders.Count: > 0 } && IsRecent(cached.UpdateTime, OrderDuration))
            {
                orders = cached.Orders;
                RefreshRemainTime(orders);
            }

            if (orders is null)
            {
                var character = (structure is { CharacterId: > 0 } ? CharacterStore.Get(structure.CharacterId) : null)
                    ?? await CharacterStore.GetDefaultAsync();
                if (character is null)
                {
                    return null;
                }

                var context = new CharacterContext(character);
                if (!await context.EnsureTokenValidAsync())
                {
                    return null;
                }

                orders = await FetchOrderPagesAsync(
                    page => ListStructureOrdersPageAsync(context.Auth, structureId, page),
                    cancellationToken);

                if (orders is null)
                {
                    return null;
                }

                // 建筑订单不含星系：统一用该建筑所在星系（随后由 SetSystemInfo 映射到星域）
                if (structure is not null)
                {
                    foreach (var order in orders)
                    {
                        order.SystemId = structure.SolarSystemId;
                    }
                }

                // 失败（空响应）时不写缓存，避免留下一个空文件
                if (orders.Count > 0)
                {
                    await WriteJsonFileAsync(filePath, new StructureOrderCache
                    {
                        StructureId = structureId,
                        UpdateTime = DateTime.Now,
                        Orders = orders,
                    });
                }
            }

            await EnrichOrdersAsync(orders, cancellationToken);
            return orders;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return null;
        }
    }

    /// <summary>
    /// 获取星域订单（含建筑订单）。星域接口的买单已包含建筑买单，故与建筑订单按
    /// <c>OrderId</c> 去重时优先保留星域接口的（其刷新更快）。
    /// </summary>
    public async Task<List<MarketOrder>?> GetAllRegionOrdersAsync(long regionId, bool skipStructure, CancellationToken cancellationToken = default, Action<int, int>? pageCallback = null)
    {
        var regions = await GetCachedRegionOrdersAsync(regionId, skipStructure, cancellationToken, pageCallback);

        List<MarketOrder>? structures = null;
        if (!skipStructure)
        {
            structures = await GetStructureOrdersOfRegionAsync(regionId, cancellationToken);
        }

        if (regions is not { Count: > 0 })
        {
            return structures is { Count: > 0 } ? structures : null;
        }

        if (structures is { Count: > 0 })
        {
            var seen = regions.Select(p => p.OrderId).ToHashSet();
            regions.AddRange(structures.Where(p => !seen.Contains(p.OrderId)));
        }

        return regions;
    }

    /// <summary>获取某星系的订单（拉取所在星域后按 SystemId 过滤）。</summary>
    public async Task<List<MarketOrder>?> GetSolarSystemOrdersAsync(int mapSolarSystemId, bool skipStructure, CancellationToken cancellationToken = default, Action<int, int>? pageCallback = null)
    {
        var system = await Core.Services.DB.MapSolarSystemService.QueryAsync(mapSolarSystemId);
        if (system is null)
        {
            return null;
        }

        var orders = await GetAllRegionOrdersAsync(system.RegionID, skipStructure, cancellationToken, pageCallback);
        return orders?.Where(p => p.SystemId == mapSolarSystemId).ToList();
    }

    // ==================================================================
    //  二、整星域订单（缓存 + 建筑合并）
    // ==================================================================

    /// <summary>
    /// 获取某星域内的<b>全部</b>物品订单（<c>typeId == null</c> 的星域接口），命中
    /// <c>Configs/RegionOrders/{regionId}.json</c> 且未过期时直接读缓存。缓存只存星域接口结果，
    /// 不含建筑合并结果（合并由调用方按需进行）。
    /// </summary>
    private async Task<List<MarketOrder>?> GetCachedRegionOrdersAsync(long regionId, bool skipStructure, CancellationToken cancellationToken, Action<int, int>? pageCallback)
    {
        try
        {
            var filePath = Path.Combine(MarketOrderSettingService.RegionOrderFolder, $"{regionId}.json");
            List<MarketOrder>? orders = null;

            // 先判文件新鲜度再反序列化：整星域缓存可达百 MB，过期文件不必读进内存
            if (IsFileRecent(filePath, OrderDuration)
                && await ReadJsonFileAsync<RegionOrderCache>(filePath) is { Orders.Count: > 0 } cached)
            {
                orders = cached.Orders;
                RefreshRemainTime(orders);
            }
            else
            {
                orders = await FetchOrderPagesAsync(
                    page => ListRegionOrdersPageAsync(regionId, null, page),
                    cancellationToken,
                    pageCallback);

                if (orders is { Count: > 0 })
                {
                    await WriteJsonFileAsync(filePath, new RegionOrderCache
                    {
                        RegionId = regionId,
                        UpdateTime = DateTime.Now,
                        Orders = orders,
                    });
                }
            }

            if (orders is { Count: > 0 })
            {
                await SetOrderInfoAsync(orders, cancellationToken, skipStructure);
            }

            return orders;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return null;
        }
    }

    /// <summary>获取某星域下<b>本地已知建筑</b>的全部订单（逐个建筑走结构订单缓存）。</summary>
    private async Task<List<MarketOrder>?> GetStructureOrdersOfRegionAsync(long regionId, CancellationToken cancellationToken)
    {
        var structures = StructureService.GetStructuresOfRegion(regionId);
        if (structures is not { Count: > 0 })
        {
            return null;
        }

        var orders = new List<MarketOrder>();
        foreach (var structure in structures)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            var list = await GetStructureOrdersAsync(structure.Id, cancellationToken);
            if (list is { Count: > 0 })
            {
                orders.AddRange(list);
            }
        }

        return orders;
    }

    // ==================================================================
    //  三、历史统计
    // ==================================================================

    /// <summary>
    /// 获取某星域指定物品的历史统计。命中 <c>Configs/HistoryOrders/{regionId}/{typeId}.json</c>
    /// 且未超过有效期时直接读缓存，否则请求 ESI 并回写。<paramref name="forceRefresh"/> 为 true 时跳过读缓存。
    /// </summary>
    public Task<List<MarketRegionHistory>?> GetHistoryAsync(int typeId, int regionId, bool forceRefresh = false)
        => GetHistoryInternalAsync(typeId, regionId, forceRefresh, logErrors: true);

    /// <summary>
    /// 多线程批量获取历史统计。返回 <c>typeId → 统计</c>；无历史或失败的物品不在结果中。
    /// <paramref name="progress"/> 参数为 <c>(已完成数, 总数)</c>。批量场景下单条失败不写日志（避免刷屏）。
    /// </summary>
    public async Task<Dictionary<int, List<Statistic>>> GetHistoryBatchAsync(IEnumerable<int> typeIds, int regionId, CancellationToken cancellationToken = default, Action<int, int>? progress = null)
    {
        var ids = typeIds.Where(p => p > 0).Distinct().ToList();
        if (ids.Count == 0 || regionId <= 0)
        {
            return [];
        }

        var total = ids.Count;
        var done = 0;
        var results = await Core.Helpers.ThreadHelper.RunAsync(ids, MaxThread, async id =>
        {
            var raw = await GetHistoryInternalAsync(id, regionId, forceRefresh: false, logErrors: false);
            var finished = Interlocked.Increment(ref done);
            progress?.Invoke(finished, total);
            return (Id: id, Data: raw is { Count: > 0 } ? raw.Select(p => new Statistic(p, id)).ToList() : null);
        }, cancellationToken);

        var dic = new Dictionary<int, List<Statistic>>(ids.Count);
        foreach (var (id, data) in results)
        {
            if (data is { Count: > 0 })
            {
                dic[id] = data;
            }
        }

        return dic;
    }

    /// <summary>单个物品历史统计的取数实现（缓存 + ESI），供公开版与批量版共用。</summary>
    private async Task<List<MarketRegionHistory>?> GetHistoryInternalAsync(int typeId, int regionId, bool forceRefresh, bool logErrors)
    {
        try
        {
            regionId = (int)NormalizeRegion(typeId, regionId);
            var folder = Path.Combine(MarketOrderSettingService.HistoryOrderFolder, regionId.ToString());
            var localFile = Path.Combine(folder, $"{typeId}.json");

            if (!forceRefresh && IsFileRecent(localFile, HistoryDuration)
                && await ReadJsonFileAsync<List<MarketRegionHistory>>(localFile) is { Count: > 0 } cached)
            {
                return cached;
            }

            var resp = await _esiClient.Market.ListHistoricalMarketStatisticsInRegionAsync(regionId, typeId);
            if (resp?.Model is null)
            {
                return null;
            }

            await WriteJsonFileAsync(localFile, resp.Model);
            return resp.Model;
        }
        catch (Exception ex)
        {
            // ESI 限流 / 无历史均属预期
            if (logErrors)
            {
                Log.Error(ex);
            }

            return null;
        }
    }

    // ==================================================================
    //  四、分页取数与缓存基础设施
    // ==================================================================

    /// <summary>
    /// 订单接口的通用分页取数。总页数（ESI 的 <c>X-Pages</c>）只在响应里，因此先串行取第 1 页拿到页数，
    /// 其余页按设置的线程数<b>并发</b>取，最后按页序展平。四种订单来源（星域指定物品 / 整星域 / 建筑）共用。
    /// <para>
    /// 失败判定：单页请求失败会重试一次，重试后仍失败则<b>整体返回 null</b>（避免把残缺结果写进缓存）；
    /// 单页返回空响应（抓取期间 <c>X-Pages</c> 变小、该页已不存在）按空页跳过，不算失败。取消返回 null。
    /// </para>
    /// </summary>
    private static async Task<List<MarketOrder>?> FetchOrderPagesAsync(
        Func<int, Task<(List<EsiMarketOrder>? Model, int MaxPages)>> fetchPage,
        CancellationToken cancellationToken,
        Action<int, int>? pageCallback = null)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            // 第 1 页必须先取：总页数只在响应头里
            var (firstPage, maxPages) = await fetchPage(1);
            if (firstPage is null)
            {
                return null; // 响应异常，交由调用方按失败处理
            }

            maxPages = Math.Max(1, maxPages);
            pageCallback?.Invoke(1, maxPages);
            if (firstPage.Count == 0 || maxPages <= 1)
            {
                return firstPage.Select(p => new MarketOrder(p)).ToList();
            }

            var pages = new List<EsiMarketOrder>?[maxPages];
            pages[0] = firstPage;

            var completed = 1;
            var failed = 0;
            var remaining = Enumerable.Range(2, maxPages - 1).ToList();
            await Core.Helpers.ThreadHelper.RunAsync(remaining, PageConcurrency, async page =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var model = await FetchPageWithRetryAsync(fetchPage, page, cancellationToken);
                if (model is null)
                {
                    Interlocked.Increment(ref failed);
                    return;
                }

                pages[page - 1] = model;
                pageCallback?.Invoke(Interlocked.Increment(ref completed), maxPages);
            });

            if (cancellationToken.IsCancellationRequested || Volatile.Read(ref failed) > 0)
            {
                return null;
            }

            var orders = new List<MarketOrder>();
            foreach (var page in pages)
            {
                if (page is { Count: > 0 })
                {
                    orders.AddRange(page.Select(p => new MarketOrder(p)));
                }
            }

            return orders;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return null;
        }
    }

    /// <summary>取单页并带一次重试（并发抓取时偶发的限流/超时通常重试即可）。</summary>
    private static async Task<List<EsiMarketOrder>?> FetchPageWithRetryAsync(
        Func<int, Task<(List<EsiMarketOrder>? Model, int MaxPages)>> fetchPage,
        int page,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            if (attempt > 0)
            {
                try
                {
                    await Task.Delay(500, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }

            try
            {
                var (model, _) = await fetchPage(page);
                return model;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                if (attempt == 1)
                {
                    Log.Error(ex); // 重试后仍失败：记日志，由调用方判定整体失败
                }
            }
        }

        return null;
    }

    /// <summary>星域订单接口的单页投影（统一成 (订单列表, 总页数) 供分页循环使用）。</summary>
    private Task<(List<EsiMarketOrder>? Model, int MaxPages)> ListRegionOrdersPageAsync(long regionId, long? typeId, int page)
    {
        return Fetch();

        async Task<(List<EsiMarketOrder>? Model, int MaxPages)> Fetch()
        {
            var resp = await _esiClient.Market.ListOrdersInRegionAsync(regionId, typeId, page);
            return (resp?.Model, resp?.MaxPages ?? 0);
        }
    }

    /// <summary>建筑订单接口的单页投影（需角色授权）。</summary>
    private Task<(List<EsiMarketOrder>? Model, int MaxPages)> ListStructureOrdersPageAsync(EVEStandard.Models.API.AuthDTO auth, long structureId, int page)
    {
        return Fetch();

        async Task<(List<EsiMarketOrder>? Model, int MaxPages)> Fetch()
        {
            var resp = await _esiClient.Market.ListOrdersInStructureAsync(auth, structureId, page);
            return (resp?.Model, resp?.MaxPages ?? 0);
        }
    }

    /// <summary>缓存文件是否仍在有效期内（按文件最后写入时间）。</summary>
    private static bool IsFileRecent(string path, int ttlMinutes)
    {
        try
        {
            return File.Exists(path)
                && (DateTime.Now - new FileInfo(path).LastWriteTime).TotalMinutes < ttlMinutes;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return false;
        }
    }

    /// <summary>时间戳是否仍在有效期内（用于缓存文件内记录的更新时间的场景）。</summary>
    private static bool IsRecent(DateTime timestamp, int ttlMinutes)
        => (DateTime.Now - timestamp).TotalMinutes < ttlMinutes;

    /// <summary>读取 JSON 缓存文件；文件不存在或解析失败返回 null。</summary>
    private static async Task<T?> ReadJsonFileAsync<T>(string path) where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return await Task.Run(() => JsonConvert.DeserializeObject<T>(File.ReadAllText(path)));
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return null;
        }
    }

    /// <summary>写入 JSON 缓存文件（自动创建目录）。</summary>
    private static async Task WriteJsonFileAsync<T>(string path, T value)
    {
        try
        {
            await Task.Run(() =>
            {
                var folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                File.WriteAllText(path, JsonConvert.SerializeObject(value));
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    /// <summary>重算剩余时间（缓存里的订单可能已放置很久）。</summary>
    private static void RefreshRemainTime(List<MarketOrder> orders)
    {
        var now = DateTime.Now;
        foreach (var order in orders)
        {
            order.RemainTimeSpan = order.Issued.AddDays(order.Duration) - now;
        }
    }

    /// <summary>建筑订单缓存文件内容（对齐 WinUI 版的 StructureOrder）。</summary>
    private sealed class StructureOrderCache
    {
        public long StructureId { get; set; }

        public DateTime UpdateTime { get; set; }

        public List<MarketOrder> Orders { get; set; } = [];
    }

    /// <summary>星域全部订单缓存文件内容（对齐 WinUI 版的 RegionOrder）。</summary>
    private sealed class RegionOrderCache
    {
        public long RegionId { get; set; }

        public DateTime UpdateTime { get; set; }

        public List<MarketOrder> Orders { get; set; } = [];
    }

    // ==================================================================
    //  五、订单信息富化
    // ==================================================================

    /// <summary>
    /// 为订单补充物品类型 / 位置名 / 星系与所属星域（供个人、军团订单等复用）。
    /// </summary>
    public static Task EnrichOrdersAsync(List<MarketOrder> orders, CancellationToken cancellationToken = default)
        => orders is { Count: > 0 }
            ? SetOrderInfoAsync(orders, cancellationToken)
            : Task.CompletedTask;

    private static async Task SetOrderInfoAsync(List<MarketOrder> orders, CancellationToken cancellationToken, bool skipStructure = false)
    {
        await SetTypeInfoAsync(orders);
        await SetLocationInfoAsync(orders, cancellationToken, skipStructure);
        await SetSystemInfoAsync(orders);
    }

    /// <summary>物品类型（本地 SDE）。</summary>
    private static async Task SetTypeInfoAsync(List<MarketOrder> orders)
    {
        var ids = orders.Select(p => p.TypeId).Distinct().ToList();
        var types = await Core.Services.DB.InvTypeService.QueryTypesAsync(ids);
        var dic = types.ToDictionary(p => (long)p.TypeID);

        foreach (var order in orders)
        {
            order.InvType = dic.TryGetValue(order.TypeId, out var type)
                ? type
                : new Core.DBModels.InvType { TypeName = order.TypeId.ToString(), TypeID = (int)order.TypeId };
        }
    }

    /// <summary>
    /// 位置名：空间站走本地 SDE；建筑走本地结构列表，解析不到回落原始 ID。
    /// <paramref name="skipStructure"/> 为 true 时完全跳过建筑（倒货整星域查询时建筑量大且解析慢）。
    /// </summary>
    private static async Task SetLocationInfoAsync(List<MarketOrder> orders, CancellationToken cancellationToken, bool skipStructure)
    {
        var stationOrders = orders.Where(p => p.IsStation).ToList();
        if (stationOrders.Count > 0)
        {
            var stations = await Core.Services.DB.StaStationService.QueryAsync(stationOrders.Select(p => (int)p.LocationId).ToList());
            var dic = stations.ToDictionary(p => p.StationID);
            foreach (var order in stationOrders)
            {
                if (dic.TryGetValue((int)order.LocationId, out var station))
                {
                    order.LocationName = station.StationName;
                    order.SystemId = station.SolarSystemID; // 个人订单可能只有 LocationId
                }
                else
                {
                    order.LocationName = order.LocationId.ToString();
                }
            }
        }

        if (skipStructure)
        {
            return;
        }

        var structureNameCache = new Dictionary<long, string>();
        foreach (var order in orders.Where(p => !p.IsStation))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (structureNameCache.TryGetValue(order.LocationId, out var cachedName))
            {
                order.LocationName = cachedName;
                continue;
            }

            var structure = StructureService.GetStructure(order.LocationId);
            if (structure is not null)
            {
                order.LocationName = structure.Name;
                order.SystemId = structure.SolarSystemId; // 建筑订单/星域订单都可能缺 SystemId
            }
            else
            {
                order.LocationName = order.LocationId.ToString();
            }

            structureNameCache[order.LocationId] = order.LocationName;
        }
    }

    /// <summary>星系与所属星域（本地 SDE）。系统 0（位置未知）会被跳过，避免无意义的查询。</summary>
    private static async Task SetSystemInfoAsync(List<MarketOrder> orders)
    {
        var ids = orders.Select(p => (int)p.SystemId).Where(p => p > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var systems = await Core.Services.DB.MapSolarSystemService.QueryAsync(ids);
        var dic = systems.ToDictionary(p => p.SolarSystemID);
        foreach (var order in orders)
        {
            if (dic.TryGetValue((int)order.SystemId, out var system))
            {
                order.SolarSystem = system;
                order.RegionId = system.RegionID;
            }
        }
    }
}
