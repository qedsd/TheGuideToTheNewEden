using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WPF.Services.Characters;

// =====================================================================
//  阶段 117：AI 助手所需的"角色 ESI 直读"端点
//
//  这些数据应用界面本来没有页面消费（资产 / 蓝本 / 声望 / 联系人 / LP / 日历 / 勋章），
//  因此直接以"服务 + DTO"的形式提供；DTO 一律与 EVEStandard 模型解耦（沿用本项目服务层约定），
//  名称富化统一走本地 SDE / 静态库 / IdName 库，**不额外打 ESI**（唯一例外是资产的容器名，且允许失败）。
// =====================================================================

/// <summary>一条资产。</summary>
public sealed class CharacterAssetRow
{
    public long ItemId { get; set; }

    public long TypeId { get; set; }

    /// <summary>物品名（本地 SDE，查不到回退 TypeId 文本）。</summary>
    public string? TypeName { get; set; }

    public long Quantity { get; set; }

    /// <summary>所在位置 ID（空间站 / 结构 / 舰船或容器物品 ID）。</summary>
    public long LocationId { get; set; }

    /// <summary>位置类型：station / solar_system / item / other。</summary>
    public string? LocationType { get; set; }

    /// <summary>位置标记（Hangar / ShipHangar / Deliveries …）。</summary>
    public string? LocationFlag { get; set; }

    /// <summary>该物品自身是否是**容器类物品**（舰船 / 货柜等，其 ItemId 会作为别的资产的 LocationId 出现）。</summary>
    public bool IsContainerSource { get; set; }

    /// <summary>容器名（ESI 资产命名接口解析；取不到为 null）。</summary>
    public string? ContainerName { get; set; }

    /// <summary>是否为蓝图拷贝（仅资产接口会给这个标记）。</summary>
    public bool? IsBlueprintCopy { get; set; }
}

/// <summary>按"一个地点（或一个容器）"聚合后的资产分组。</summary>
public sealed class CharacterAssetGroup
{
    /// <summary>分组键：地点 ID，或容器物品 ID。</summary>
    public long LocationId { get; set; }

    /// <summary>显示名（站名 / 结构名 / 容器名 / 舰船名；解析不到回退 ID 文本）。</summary>
    public string? LocationName { get; set; }

    /// <summary>位置标记（取该分组第一行）。</summary>
    public string? LocationFlag { get; set; }

    /// <summary>该分组下的资产条目数（不同物品行数）。</summary>
    public int ItemCount { get; set; }

    /// <summary>该分组下的数量合计。</summary>
    public long TotalQuantity { get; set; }

    /// <summary>该分组下数量最多的前若干物品。</summary>
    public List<CharacterAssetRow> TopItems { get; set; } = [];
}

/// <summary>资产汇总：原始行 + 已解析位置名的分组（工具按需要选用，避免在工具里重复做名称解析）。</summary>
public sealed class CharacterAssetSummary
{
    public List<CharacterAssetRow> Assets { get; set; } = [];

    public List<CharacterAssetGroup> Groups { get; set; } = [];
}

/// <summary>一条蓝图。</summary>
public sealed class CharacterBlueprintRow
{
    public long ItemId { get; set; }

    public long TypeId { get; set; }

    /// <summary>蓝图名（本地 SDE，查不到回退 TypeId 文本）。</summary>
    public string? TypeName { get; set; }

    /// <summary>产物名（静态库 industryActivityProducts；查不到为 null）。</summary>
    public string? ProductName { get; set; }

    /// <summary>材料效率（0-10）。</summary>
    public long MaterialEfficiency { get; set; }

    /// <summary>时间效率（0-20）。</summary>
    public long TimeEfficiency { get; set; }

    /// <summary>剩余流程数（-1 = 原图无限制）。</summary>
    public long Runs { get; set; }

    /// <summary>是否拷贝（<c>Runs &gt;= 0</c> 视为拷贝；-1 为原图）。</summary>
    public bool IsCopy => Runs >= 0;

    public long LocationId { get; set; }

    public string? LocationName { get; set; }

    public string? LocationFlag { get; set; }
}

/// <summary>一条声望。</summary>
public sealed class CharacterStandingRow
{
    public long FromId { get; set; }

    /// <summary>agent / npc_corp / faction。</summary>
    public string? FromType { get; set; }

    /// <summary>实体名（本地 IdName 库；查不到回退 ID 文本）。</summary>
    public string? FromName { get; set; }

    /// <summary>-10 .. 10。</summary>
    public float Standing { get; set; }
}

/// <summary>一条联系人。</summary>
public sealed class CharacterContactRow
{
    public long ContactId { get; set; }

    /// <summary>character / corporation / alliance / faction。</summary>
    public string? ContactType { get; set; }

    /// <summary>联系人名（本地 IdName 库；查不到回退 ID 文本）。</summary>
    public string? ContactName { get; set; }

    public bool IsBlocked { get; set; }

    public bool IsWatched { get; set; }

    public float Standing { get; set; }

    /// <summary>标签名列表（由 labelIds 映射；无标签为空）。</summary>
    public List<string> Labels { get; set; } = [];

    /// <summary>原始标签 ID（映射标签名用；不直接展示给模型）。</summary>
    public List<long> LabelIds { get; set; } = [];
}

/// <summary>某军团的 LP。</summary>
public sealed class CharacterLoyaltyRow
{
    public long CorporationId { get; set; }

    public string? CorporationName { get; set; }

    public long Points { get; set; }
}

/// <summary>一条日历事件摘要。</summary>
public sealed class CharacterCalendarRow
{
    public long EventId { get; set; }

    public DateTime EventDate { get; set; }

    public string? Title { get; set; }

    /// <summary>accepted / declined / tentative / not_responded。</summary>
    public string? Response { get; set; }

    public long Importance { get; set; }
}

/// <summary>一枚勋章。</summary>
public sealed class CharacterMedalRow
{
    public long MedalId { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public long CorporationId { get; set; }

    public string? CorporationName { get; set; }

    public long IssuerId { get; set; }

    public string? IssuerName { get; set; }

    public DateTime Date { get; set; }

    public string? Status { get; set; }

    public string? Reason { get; set; }
}

/// <summary>
/// 阶段 117：资产 / 蓝本 / 声望 / 联系人 / LP / 日历 / 勋章七个端点的只读服务。
/// <para>
/// 约定（与既有角色服务一致）：**不抛异常**——失败记日志并返回 <c>null</c>；分页端点全部取完
/// （有页数上限护栏）；名称富化只用本地库，不额外打 ESI。
/// </para>
/// </summary>
public static class CharacterEsiExtraService
{
    /// <summary>单个端点一次拉取的最大页数（防止"资产上万"时把时间与限流额度吃光）。</summary>
    public const int MaxPages = 40;

    /// <summary>缓存 TTL：资产/蓝本变动慢，其余变动快，分开设置。</summary>
    private static readonly TimeSpan AssetTtl = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan MediumTtl = TimeSpan.FromMinutes(5);

    /// <summary>资产聚合时每个分组保留的物品行数。</summary>
    private const int TopItemsPerGroup = 6;

    /// <summary>资产容器名一次查询的 ID 上限（ESI 该接口本身也有条数上限）。</summary>
    private const int MaxContainerNameIds = 1000;

    // ================= 资产 =================

    /// <summary>全部资产（分页取完）并按地点/容器聚合。返回 null = 调用失败。</summary>
    public static async Task<List<CharacterAssetGroup>?> GetAssetGroupsAsync(CharacterContext context, bool forceRefresh = false)
    {
        var assets = await GetAssetsAsync(context, forceRefresh).ConfigureAwait(false);
        return assets is null ? null : await BuildGroupsAsync(context, assets).ConfigureAwait(false);
    }

    /// <summary>
    /// 资产汇总（原始行 + 分组）。给 AI 工具用：按物品汇总需要完整列表，按地点展示需要已解析好名字的分组，
    /// 一次取数同时给出两者，省掉第二遍名称解析。
    /// </summary>
    public static async Task<CharacterAssetSummary?> GetAssetSummaryAsync(CharacterContext context, bool forceRefresh = false)
    {
        var assets = await GetAssetsAsync(context, forceRefresh).ConfigureAwait(false);
        if (assets is null)
        {
            return null;
        }

        return new CharacterAssetSummary
        {
            Assets = assets,
            Groups = await BuildGroupsAsync(context, assets).ConfigureAwait(false),
        };
    }

    /// <summary>全部资产原始行（分页取完、已富化物品名）。</summary>
    public static async Task<List<CharacterAssetRow>?> GetAssetsAsync(CharacterContext context, bool forceRefresh = false)
    {
        const string cacheKey = "esi.assets";

        if (!forceRefresh
            && CharacterCache.TryGet<List<CharacterAssetRow>>(context.CharacterId, cacheKey, out var cached)
            && cached is { Count: > 0 })
        {
            return cached;
        }

        if (!await context.EnsureTokenValidAsync().ConfigureAwait(false))
        {
            return null;
        }

        var rows = new List<CharacterAssetRow>();
        var ok = await ForEachPageAsync(async page =>
        {
            var dto = await context.Api.Assets.GetCharacterAssetsAsync(context.Auth, page).ConfigureAwait(false);
            var model = dto?.Model;
            if (model is null)
            {
                return 0;
            }

            foreach (var asset in model)
            {
                rows.Add(new CharacterAssetRow
                {
                    ItemId = asset.ItemId,
                    TypeId = asset.TypeId,
                    Quantity = asset.Quantity,
                    LocationId = asset.LocationId,
                    LocationType = asset.LocationType,
                    LocationFlag = asset.LocationFlag,
                    IsBlueprintCopy = asset.IsBlueprintCopy,
                });
            }

            return model.Count;
        }).ConfigureAwait(false);

        if (!ok)
        {
            return null;
        }

        // 资产条目多、重复率高，批量查一次物品名（QueryTypes 内部按 id 去重）
        var nameMap = BuildTypeNameMap(rows.Select(p => p.TypeId));
        foreach (var row in rows)
        {
            row.TypeName = nameMap.GetValueOrDefault(row.TypeId) ?? row.TypeId.ToString();
        }

        CharacterCache.Set(context.CharacterId, cacheKey, rows, AssetTtl);
        return rows;
    }

    /// <summary>
    /// 把资产按"地点（或容器）"聚合：容器类资产的 ItemId 会作为其它资产的 LocationId 出现，
    /// 这类分组用容器/舰船名而不是冷冰冰的 ID。
    /// </summary>
    private static async Task<List<CharacterAssetGroup>> BuildGroupsAsync(CharacterContext context, List<CharacterAssetRow> assets)
    {
        var byItemId = new Dictionary<long, CharacterAssetRow>();
        foreach (var row in assets)
        {
            byItemId[row.ItemId] = row;
        }

        // 哪些物品被别的东西"当作位置"引用 → 这些是容器/舰船
        foreach (var row in assets)
        {
            if (row.LocationId != row.ItemId && byItemId.TryGetValue(row.LocationId, out var container))
            {
                container.IsContainerSource = true;
            }
        }

        // 容器名：ESI 批量命名（条数有限，超出部分保持 null → 回退物品名或 ID）
        var containerIds = assets.Where(p => p.IsContainerSource)
            .Select(p => p.ItemId)
            .Distinct()
            .Take(MaxContainerNameIds)
            .ToList();
        if (containerIds.Count > 0)
        {
            var names = await TryGetAssetNamesAsync(context, containerIds).ConfigureAwait(false);
            if (names is { Count: > 0 })
            {
                foreach (var row in assets)
                {
                    if (names.TryGetValue(row.ItemId, out var name))
                    {
                        row.ContainerName = name;
                    }
                }
            }
        }

        // 普通地点（空间站 / 星系）走名称解析；结构 ID 只在本地列表里找（不额外打 ESI）
        var plainLocationIds = assets
            .Where(p => p.LocationType is "station" or "solar_system")
            .Select(p => p.LocationId)
            .Where(p => p > 0)
            .Distinct()
            .ToList();

        var locationNames = plainLocationIds.Count > 0
            ? await LocationNameResolver.ResolveAsync(plainLocationIds).ConfigureAwait(false)
            : [];

        var groups = new Dictionary<long, CharacterAssetGroup>();
        foreach (var row in assets)
        {
            if (!groups.TryGetValue(row.LocationId, out var group))
            {
                group = new CharacterAssetGroup
                {
                    LocationId = row.LocationId,
                    LocationName = ResolveLocationName(row, byItemId, locationNames),
                    LocationFlag = row.LocationFlag,
                };
                groups[row.LocationId] = group;
            }

            group.ItemCount++;
            group.TotalQuantity += row.Quantity;
            group.TopItems.Add(row);
        }

        foreach (var group in groups.Values)
        {
            group.TopItems = group.TopItems
                .OrderByDescending(p => p.Quantity)
                .ThenBy(p => p.TypeName, StringComparer.Ordinal)
                .Take(TopItemsPerGroup)
                .ToList();
        }

        return groups.Values
            .OrderByDescending(p => p.ItemCount)
            .ThenBy(p => p.LocationName, StringComparer.Ordinal)
            .ToList();
    }

    private static string? ResolveLocationName(
        CharacterAssetRow row,
        Dictionary<long, CharacterAssetRow> byItemId,
        Dictionary<long, string> locationNames)
    {
        if (row.LocationId != row.ItemId && byItemId.TryGetValue(row.LocationId, out var container))
        {
            // 位置是另一个物品（舰船 / 货柜 / 移动仓库…）
            var label = string.IsNullOrWhiteSpace(container.ContainerName)
                ? container.TypeName
                : container.ContainerName;
            return string.IsNullOrWhiteSpace(label)
                ? $"物品 {container.ItemId}"
                : $"{label}（物品 {container.ItemId}）";
        }

        if (locationNames.TryGetValue(row.LocationId, out var name) && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return row.LocationId > 0 ? $"未知位置 {row.LocationId}" : null;
    }

    /// <summary>ESI 资产命名（失败/无权时返回 null，调用方回退显示物品名）。</summary>
    private static async Task<Dictionary<long, string>?> TryGetAssetNamesAsync(CharacterContext context, List<long> itemIds)
    {
        try
        {
            var dto = await context.Api.Assets.GetCharacterAssetNamesAsync(context.Auth, itemIds).ConfigureAwait(false);
            var model = dto?.Model;
            if (model is null)
            {
                return null;
            }

            var result = new Dictionary<long, string>();
            foreach (var item in model)
            {
                if (!string.IsNullOrWhiteSpace(item.Name))
                {
                    result[item.ItemId] = item.Name;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"资产容器名解析失败（回退显示物品名）：{ex.Message}");
            return null;
        }
    }

    // ================= 蓝图 =================

    /// <summary>全部蓝图（分页取完）。返回 null = 调用失败。</summary>
    public static async Task<List<CharacterBlueprintRow>?> GetBlueprintsAsync(CharacterContext context, bool forceRefresh = false)
    {
        const string cacheKey = "esi.blueprints";

        if (!forceRefresh
            && CharacterCache.TryGet<List<CharacterBlueprintRow>>(context.CharacterId, cacheKey, out var cached)
            && cached is { Count: > 0 })
        {
            return cached;
        }

        if (!await context.EnsureTokenValidAsync().ConfigureAwait(false))
        {
            return null;
        }

        var rows = new List<CharacterBlueprintRow>();
        var ok = await ForEachPageAsync(async page =>
        {
            var dto = await context.Api.Character.GetBlueprintsAsync(context.Auth, page).ConfigureAwait(false);
            var model = dto?.Model;
            if (model is null)
            {
                return 0;
            }

            foreach (var bp in model)
            {
                rows.Add(new CharacterBlueprintRow
                {
                    ItemId = bp.ItemId,
                    TypeId = bp.TypeId,
                    MaterialEfficiency = bp.MaterialEfficiency,
                    TimeEfficiency = bp.TimeEfficiency,
                    Runs = bp.Runs,
                    LocationId = bp.LocationId,
                    LocationFlag = bp.LocationFlag,
                });
            }

            return model.Count;
        }).ConfigureAwait(false);

        if (!ok)
        {
            return null;
        }

        var nameMap = BuildTypeNameMap(rows.Select(p => p.TypeId));
        var productMap = InvTypeService.QueryBlueprintProducts(rows.Select(p => (int)Math.Min(p.TypeId, int.MaxValue)));
        var productNames = BuildTypeNameMap(productMap.Values.Select(p => (long)p));

        foreach (var row in rows)
        {
            row.TypeName = nameMap.GetValueOrDefault(row.TypeId) ?? row.TypeId.ToString();
            if (productMap.TryGetValue((int)Math.Min(row.TypeId, int.MaxValue), out var productId))
            {
                row.ProductName = productNames.GetValueOrDefault(productId);
            }
        }

        var locationIds = rows.Select(p => p.LocationId).Where(p => p > 0).Distinct().ToList();
        if (locationIds.Count > 0)
        {
            var names = await LocationNameResolver.ResolveAsync(locationIds).ConfigureAwait(false);
            foreach (var row in rows)
            {
                row.LocationName = names.TryGetValue(row.LocationId, out var name)
                    ? name
                    : (row.LocationId > 0 ? row.LocationId.ToString() : null);
            }
        }

        CharacterCache.Set(context.CharacterId, cacheKey, rows, AssetTtl);
        return rows;
    }

    // ================= 声望 =================

    /// <summary>角色声望列表（单次请求）。返回 null = 调用失败。</summary>
    public static async Task<List<CharacterStandingRow>?> GetStandingsAsync(CharacterContext context, bool forceRefresh = false)
    {
        const string cacheKey = "esi.standings";

        if (!forceRefresh
            && CharacterCache.TryGet<List<CharacterStandingRow>>(context.CharacterId, cacheKey, out var cached)
            && cached is { Count: > 0 })
        {
            return cached;
        }

        if (!await context.EnsureTokenValidAsync().ConfigureAwait(false))
        {
            return null;
        }

        try
        {
            var dto = await context.Api.Character.GetStandingsAsync(context.Auth).ConfigureAwait(false);
            var model = dto?.Model ?? [];
            var rows = model.Select(p => new CharacterStandingRow
            {
                FromId = p.FromId,
                FromType = p.FromType,
                Standing = p.StandingValue,
            }).ToList();

            var names = await ResolveEntityNamesAsync(rows.Select(p => p.FromId)).ConfigureAwait(false);
            foreach (var row in rows)
            {
                row.FromName = names.GetValueOrDefault(row.FromId) ?? row.FromId.ToString();
            }

            CharacterCache.Set(context.CharacterId, cacheKey, rows, MediumTtl);
            return rows;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    // ================= 联系人 =================

    /// <summary>角色联系人（分页取完，含标签名）。返回 null = 调用失败。</summary>
    public static async Task<List<CharacterContactRow>?> GetContactsAsync(CharacterContext context, bool forceRefresh = false)
    {
        const string cacheKey = "esi.contacts";

        if (!forceRefresh
            && CharacterCache.TryGet<List<CharacterContactRow>>(context.CharacterId, cacheKey, out var cached)
            && cached is { Count: > 0 })
        {
            return cached;
        }

        if (!await context.EnsureTokenValidAsync().ConfigureAwait(false))
        {
            return null;
        }

        var rows = new List<CharacterContactRow>();
        var ok = await ForEachPageAsync(async page =>
        {
            var dto = await context.Api.Contacts.GetContactsAsync(context.Auth, page).ConfigureAwait(false);
            var model = dto?.Model;
            if (model is null)
            {
                return 0;
            }

            foreach (var contact in model)
            {
                rows.Add(new CharacterContactRow
                {
                    ContactId = contact.ContactId,
                    ContactType = contact.ContactType,
                    IsBlocked = contact.IsBlocked ?? false,
                    IsWatched = contact.IsWatched ?? false,
                    Standing = contact.Standing,
                    // labelIds 就在同一个模型上，直接映射（不必为了标签再拉一遍联系人）
                    Labels = [],
                    LabelIds = contact.LabelIds ?? [],
                });
            }

            return model.Count;
        }).ConfigureAwait(false);

        if (!ok)
        {
            return null;
        }

        // 标签 ID → 名称（单独端点；失败不影响联系人本身，退化成"标签 N"）
        var labelMap = await TryGetContactLabelMapAsync(context).ConfigureAwait(false) ?? [];
        foreach (var row in rows)
        {
            row.Labels = row.LabelIds
                .Select(id => labelMap.TryGetValue(id, out var name) ? name : $"标签 {id}")
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();
        }

        var names = await ResolveEntityNamesAsync(rows.Select(p => p.ContactId)).ConfigureAwait(false);
        foreach (var row in rows)
        {
            row.ContactName = names.GetValueOrDefault(row.ContactId) ?? row.ContactId.ToString();
        }

        CharacterCache.Set(context.CharacterId, cacheKey, rows, MediumTtl);
        return rows;
    }

    /// <summary>标签 ID → 标签名。</summary>
    private static async Task<Dictionary<long, string>?> TryGetContactLabelMapAsync(CharacterContext context)
    {
        try
        {
            var dto = await context.Api.Contacts.GetContactLabelsAsync(context.Auth).ConfigureAwait(false);
            var model = dto?.Model;
            if (model is null)
            {
                return null;
            }

            var result = new Dictionary<long, string>();
            foreach (var label in model)
            {
                if (!string.IsNullOrWhiteSpace(label.LabelName))
                {
                    result[label.LabelId] = label.LabelName;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"联系人标签解析失败（退化为「标签 N」）：{ex.Message}");
            return null;
        }
    }

    // ================= LP =================

    /// <summary>各军团 LP（单次请求）。返回 null = 调用失败。</summary>
    public static async Task<List<CharacterLoyaltyRow>?> GetLoyaltyAsync(CharacterContext context, bool forceRefresh = false)
    {
        const string cacheKey = "esi.loyalty";

        if (!forceRefresh
            && CharacterCache.TryGet<List<CharacterLoyaltyRow>>(context.CharacterId, cacheKey, out var cached)
            && cached is { Count: > 0 })
        {
            return cached;
        }

        if (!await context.EnsureTokenValidAsync().ConfigureAwait(false))
        {
            return null;
        }

        try
        {
            var dto = await context.Api.Loyalty.GetLoyaltyPointsAsync(context.Auth).ConfigureAwait(false);
            var model = dto?.Model ?? [];
            var rows = model.Select(p => new CharacterLoyaltyRow
            {
                CorporationId = p.CorporationId,
                Points = p.Points,
            }).ToList();

            var names = await ResolveEntityNamesAsync(rows.Select(p => p.CorporationId)).ConfigureAwait(false);
            foreach (var row in rows)
            {
                row.CorporationName = names.GetValueOrDefault(row.CorporationId) ?? row.CorporationId.ToString();
            }

            CharacterCache.Set(context.CharacterId, cacheKey, rows, MediumTtl);
            return rows;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    // ================= 日历 =================

    /// <summary>日历事件摘要（单次请求）。返回 null = 调用失败。</summary>
    public static async Task<List<CharacterCalendarRow>?> GetCalendarAsync(CharacterContext context, bool forceRefresh = false)
    {
        const string cacheKey = "esi.calendar";

        if (!forceRefresh
            && CharacterCache.TryGet<List<CharacterCalendarRow>>(context.CharacterId, cacheKey, out var cached)
            && cached is { Count: > 0 })
        {
            return cached;
        }

        if (!await context.EnsureTokenValidAsync().ConfigureAwait(false))
        {
            return null;
        }

        try
        {
            var dto = await context.Api.Calendar.ListCalendarEventSummariesAsync(context.Auth).ConfigureAwait(false);
            var model = dto?.Model ?? [];
            var rows = model.Select(p => new CharacterCalendarRow
            {
                EventId = p.EventId,
                EventDate = p.EventDate,
                Title = p.Title,
                Response = p.EventResponse,
                Importance = p.Importance,
            }).ToList();

            CharacterCache.Set(context.CharacterId, cacheKey, rows, MediumTtl);
            return rows;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    // ================= 勋章 =================

    /// <summary>角色勋章（单次请求）。返回 null = 调用失败。</summary>
    public static async Task<List<CharacterMedalRow>?> GetMedalsAsync(CharacterContext context, bool forceRefresh = false)
    {
        const string cacheKey = "esi.medals";

        if (!forceRefresh
            && CharacterCache.TryGet<List<CharacterMedalRow>>(context.CharacterId, cacheKey, out var cached)
            && cached is { Count: > 0 })
        {
            return cached;
        }

        if (!await context.EnsureTokenValidAsync().ConfigureAwait(false))
        {
            return null;
        }

        try
        {
            var dto = await context.Api.Character.GetMedalsAsync(context.Auth).ConfigureAwait(false);
            var model = dto?.Model ?? [];
            var rows = model.Select(p => new CharacterMedalRow
            {
                MedalId = p.MedalId,
                Title = p.Title,
                Description = p.Description,
                CorporationId = p.CorporationId,
                IssuerId = p.IssuerId,
                Date = p.Date,
                Status = p.Status,
                Reason = p.Reason,
            }).ToList();

            var corpNames = await ResolveEntityNamesAsync(rows.Select(p => p.CorporationId)).ConfigureAwait(false);
            foreach (var row in rows)
            {
                row.CorporationName = corpNames.GetValueOrDefault(row.CorporationId) ?? row.CorporationId.ToString();
            }

            var issuerNames = await ResolveEntityNamesAsync(rows.Select(p => p.IssuerId)).ConfigureAwait(false);
            foreach (var row in rows)
            {
                row.IssuerName = issuerNames.GetValueOrDefault(row.IssuerId) ?? row.IssuerId.ToString();
            }

            CharacterCache.Set(context.CharacterId, cacheKey, rows, MediumTtl);
            return rows;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    // ================= 公共基础设施 =================

    /// <summary>
    /// 逐页取数（第 1 页起）。<paramref name="fetchPage"/> 返回本页条数，**0 视为结束**。
    /// 停页条件：本页 0 条 / 已到 <see cref="MaxPages"/> / 本页条数少于上一页（ESI 末页）。
    /// 返回 false = 中途某页失败（调用方据此返回 null，**不把残缺结果当完整结果**）。
    /// </summary>
    private static async Task<bool> ForEachPageAsync(Func<int, Task<int>> fetchPage)
    {
        var previous = int.MaxValue;
        for (var page = 1; page <= MaxPages; page++)
        {
            int count;
            try
            {
                count = await fetchPage(page).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                return false;
            }

            if (count <= 0)
            {
                return true;
            }

            // ESI 的分页没有可靠总页数（ESIModelDTO.MaxPages 常为 0），用"本页是否比上一页少"兜底
            if (page > 1 && count < previous)
            {
                return true;
            }

            previous = count;
        }

        Core.Log.Warn($"分页取数达到上限 {MaxPages} 页，后续内容未取（结果可能不完整）");
        return true;
    }

    /// <summary>批量取物品名：TypeId → 物品名（本地主库；查不到的不进字典）。</summary>
    private static Dictionary<long, string> BuildTypeNameMap(IEnumerable<long> typeIds)
    {
        var ids = typeIds.Where(p => p > 0 && p <= int.MaxValue).Select(p => (int)p).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        try
        {
            return InvTypeService.QueryTypes(ids)
                .Where(p => !string.IsNullOrWhiteSpace(p.TypeName))
                .GroupBy(p => (long)p.TypeID)
                .ToDictionary(g => g.Key, g => g.First().TypeName!);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return [];
        }
    }

    /// <summary>批量取实体名（角色/军团/联盟/势力走本地 IdName 库；异步，避免 UI 线程死锁）。</summary>
    private static async Task<Dictionary<long, string>> ResolveEntityNamesAsync(IEnumerable<long> ids)
    {
        var result = new Dictionary<long, string>();
        var list = ids.Where(p => p > 0 && p <= int.MaxValue).Select(p => (int)p).Distinct().ToList();
        if (list.Count == 0)
        {
            return result;
        }

        try
        {
            foreach (var item in await IDNameService.GetByIdsAsync(list).ConfigureAwait(false) ?? [])
            {
                if (item.Id > 0 && !string.IsNullOrEmpty(item.Name))
                {
                    result[item.Id] = item.Name;
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        return result;
    }
}
