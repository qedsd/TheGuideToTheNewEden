using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.Industry;

/// <summary>角色持有的蓝图（同一蓝图取 ME/TE 最高的一张）。</summary>
public sealed class OwnedBlueprint
{
    public int BlueprintTypeId { get; set; }
    public int MaterialEfficiency { get; set; }
    public int TimeEfficiency { get; set; }
}

/// <summary>
/// 角色侧工业数据（需要授权）：自有蓝图 ME/TE、在制任务产出、资产库存。
/// 结果写入 <see cref="CharacterCache"/>（磁盘 + 内存，TTL 30 分钟）。
/// </summary>
public static class CharacterIndustryDataService
{
    private const string BlueprintsKey = "industry_blueprints";
    private const string JobOutputsKey = "industry_job_outputs";
    private const string AssetsKey = "industry_assets";

    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 自有蓝图（BlueprintTypeID → 最佳 ME/TE）。需要 <c>esi-characters.read_blueprints.v1</c>。
    /// </summary>
    public static async Task<Dictionary<int, OwnedBlueprint>?> GetOwnedBlueprintsAsync(CharacterContext context, bool forceRefresh = false)
    {
        if (!forceRefresh && CharacterCache.TryGet<Dictionary<int, OwnedBlueprint>>(context.CharacterId, BlueprintsKey, out var cached) && cached is not null)
        {
            return cached;
        }

        if (!await context.EnsureTokenValidAsync())
        {
            return null;
        }

        try
        {
            var result = new Dictionary<int, OwnedBlueprint>();
            int page = 1;
            int maxPages = 1;
            do
            {
                var response = await context.Api.Character.GetBlueprintsAsync(context.Auth, page, null);
                if (response?.Model != null)
                {
                    foreach (var blueprint in response.Model)
                    {
                        if (blueprint.TypeId <= 0) continue;
                        int typeId = (int)blueprint.TypeId;
                        int me = (int)blueprint.MaterialEfficiency;
                        int te = (int)blueprint.TimeEfficiency;
                        if (!result.TryGetValue(typeId, out var existing)
                            || me > existing.MaterialEfficiency
                            || (me == existing.MaterialEfficiency && te > existing.TimeEfficiency))
                        {
                            result[typeId] = new OwnedBlueprint
                            {
                                BlueprintTypeId = typeId,
                                MaterialEfficiency = me,
                                TimeEfficiency = te,
                            };
                        }
                    }
                }
                maxPages = response?.MaxPages ?? 1;
                page++;
            }
            while (page <= maxPages);

            CharacterCache.Set(context.CharacterId, BlueprintsKey, result, Ttl);
            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return null;
        }
    }

    /// <summary>
    /// 在制工业任务的预期产出（产品 TypeID → 件数），用于库存抵扣。
    /// 只统计进行中的任务，产出量 = 每流程产出 × 流程数。需要 <c>esi-industry.read_character_jobs.v1</c>。
    /// </summary>
    public static async Task<Dictionary<int, long>?> GetInProgressOutputsAsync(CharacterContext context, bool forceRefresh = false)
    {
        if (!forceRefresh && CharacterCache.TryGet<Dictionary<int, long>>(context.CharacterId, JobOutputsKey, out var cached) && cached is not null)
        {
            return cached;
        }

        if (!await context.EnsureTokenValidAsync())
        {
            return null;
        }

        try
        {
            var result = new Dictionary<int, long>();
            var jobs = (await context.Api.Industry.ListCharacterIndustryJobsAsync(context.Auth, false, null)).Model;
            if (jobs != null)
            {
                foreach (var job in jobs)
                {
                    if (!job.ProductTypeId.HasValue || job.ProductTypeId.Value <= 0 || job.Runs <= 0) continue;
                    if (!string.Equals(job.Status, "active", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(job.Status, "paused", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    int blueprintTypeId = (int)job.BlueprintTypeId;
                    int activityId = (int)job.ActivityId;
                    int productTypeId = (int)job.ProductTypeId.Value;

                    long perRun = 1;
                    var products = IndustryDbService.GetProducts(blueprintTypeId, activityId);
                    foreach (var product in products)
                    {
                        if (product.ProductTypeID == productTypeId)
                        {
                            perRun = product.Quantity;
                            break;
                        }
                    }
                    if (perRun <= 0) perRun = 1;

                    long quantity = perRun * job.Runs;
                    result[productTypeId] = result.TryGetValue(productTypeId, out var existing) ? existing + quantity : quantity;
                }
            }

            CharacterCache.Set(context.CharacterId, JobOutputsKey, result, Ttl);
            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return null;
        }
    }

    /// <summary>
    /// 角色资产按物品聚合（TypeID → 件数），用于库存抵扣。需要 <c>esi-assets.read_assets.v1</c>。
    /// </summary>
    public static async Task<Dictionary<int, long>?> GetAssetQuantitiesAsync(CharacterContext context, bool forceRefresh = false)
    {
        if (!forceRefresh && CharacterCache.TryGet<Dictionary<int, long>>(context.CharacterId, AssetsKey, out var cached) && cached is not null)
        {
            return cached;
        }

        if (!await context.EnsureTokenValidAsync())
        {
            return null;
        }

        try
        {
            var result = new Dictionary<int, long>();
            int page = 1;
            int maxPages = 1;
            do
            {
                var response = await context.Api.Assets.GetCharacterAssetsAsync(context.Auth, page, null);
                if (response?.Model != null)
                {
                    foreach (var asset in response.Model)
                    {
                        if (asset.TypeId <= 0 || asset.Quantity <= 0) continue;
                        int typeId = (int)asset.TypeId;
                        long quantity = asset.Quantity;
                        result[typeId] = result.TryGetValue(typeId, out var existing) ? existing + quantity : quantity;
                    }
                }
                maxPages = response?.MaxPages ?? 1;
                page++;
            }
            while (page <= maxPages);

            CharacterCache.Set(context.CharacterId, AssetsKey, result, Ttl);
            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return null;
        }
    }

    /// <summary>
    /// 把角色自有蓝图的 ME/TE 映射成求解器需要的"产物 TypeID → 等级"（求解器按产物键控）。
    /// </summary>
    public static Dictionary<int, int> BuildMeOverrides(Dictionary<int, OwnedBlueprint> ownedBlueprints)
    {
        return BuildLevelOverrides(ownedBlueprints, true);
    }

    public static Dictionary<int, int> BuildTeOverrides(Dictionary<int, OwnedBlueprint> ownedBlueprints)
    {
        return BuildLevelOverrides(ownedBlueprints, false);
    }

    private static Dictionary<int, int> BuildLevelOverrides(Dictionary<int, OwnedBlueprint> ownedBlueprints, bool material)
    {
        var result = new Dictionary<int, int>();
        if (ownedBlueprints == null || ownedBlueprints.Count == 0)
        {
            return result;
        }

        // 蓝图 TypeID → 产物 TypeID（制造活动）。
        var blueprintIds = ownedBlueprints.Keys.ToList();
        var productOfBlueprint = InvTypeService.QueryBlueprintProducts(blueprintIds);
        foreach (var pair in ownedBlueprints)
        {
            if (!productOfBlueprint.TryGetValue(pair.Key, out var productTypeId) || productTypeId <= 0)
            {
                continue;
            }
            int level = material ? pair.Value.MaterialEfficiency : pair.Value.TimeEfficiency;
            if (!result.TryGetValue(productTypeId, out var existing) || level > existing)
            {
                result[productTypeId] = level;
            }
        }
        return result;
    }
}
