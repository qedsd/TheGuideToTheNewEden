using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Indusrty;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Services.Characters;
using System.Threading;

namespace TheGuideToTheNewEden.WPF.Services.Industry;

/// <summary>
/// 把工业数据源（公开价格/成本指数/市场深度 + 多角色蓝图/任务/资产/技能）组装成
/// <see cref="IndustrySolverOptions"/>，供 <c>IndustrySolver</c> 直接使用。
/// </summary>
public static class IndustryOptionsFactory
{
    /// <summary>
    /// 组装求解参数。
    /// <paramref name="solarSystemId"/> 决定用哪套成本指数（0 = 不计算任务费用）；
    /// <paramref name="contexts"/> 为空时只做数量展开与公开价格（不抵扣角色库存）。
    /// 多个角色时资产/任务相加、蓝图 ME/TE 与技能取最高。
    /// </summary>
    public static async Task<IndustrySolverOptions> BuildAsync(
        IReadOnlyList<CharacterContext>? contexts,
        long solarSystemId,
        long regionId,
        double facilityTax = 0.10,
        int defaultMe = 10,
        int defaultTe = 20,
        double spaceModifier = 1.0,
        double rigMeBonus = 0,
        double rigTeBonus = 0,
        IReadOnlyList<IndustryRigBonus>? rigBonuses = null,
        int? decryptorTypeId = null,
        bool applyMeToReactions = false,
        bool applyRigToReactions = false,
        bool useAssets = true,
        bool useInProgressJobs = true,
        bool useOwnedBlueprints = true,
        bool useCharacterSkills = true,
        bool useMarketDepth = false,
        bool forceRefresh = false,
        int? inventionAttemptsOverride = null,
        double? inventionProbabilityOverride = null,
        int? inventionBpcRunsOverride = null,
        Action<int, int>? marketPageCallback = null,
        CancellationToken cancellationToken = default,
        int structureTypeId = 0)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = new IndustrySolverOptions
        {
            DefaultME = defaultMe,
            DefaultTE = defaultTe,
            FacilityTax = facilityTax,
            SpaceModifier = spaceModifier,
            RigMeBonus = rigMeBonus,
            RigTeBonus = rigTeBonus,
            RigBonuses = rigBonuses?.ToList() ?? new List<IndustryRigBonus>(),
            DecryptorTypeId = decryptorTypeId,
            InventionAttemptsOverride = inventionAttemptsOverride,
            InventionProbabilityOverride = inventionProbabilityOverride,
            InventionBpcRunsOverride = inventionBpcRunsOverride,
            ApplyMeToReactions = applyMeToReactions,
            ApplyRigToReactions = applyRigToReactions,
        };

        if (structureTypeId > 0)
        {
            var structure = IndustryDbService.GetStructureModifiers(structureTypeId);
            if (structure != null)
            {
                foreach (var pair in structure.Activities)
                {
                    // 官方反应 ID 是 9，应用内部旧库仍用 11。
                    int activityId = pair.Key == IndustryActivity.OfficialReaction ? IndustryActivity.Reaction : pair.Key;
                    options.StructureModifiers[activityId] = pair.Value;
                }
            }
        }

        try
        {
            options.AdjustedPrices = await IndustryMarketDataService.Current.GetAdjustedPricesAsync(forceRefresh);
            cancellationToken.ThrowIfCancellationRequested();
            options.AveragePrices = await IndustryMarketDataService.Current.GetAveragePricesAsync(forceRefresh);
            cancellationToken.ThrowIfCancellationRequested();

            var costIndices = await IndustryMarketDataService.Current.GetCostIndicesAsync(forceRefresh);
            cancellationToken.ThrowIfCancellationRequested();
            if (solarSystemId > 0 && costIndices.TryGetValue(solarSystemId, out var costIndex))
            {
                options.CostIndexManufacturing = costIndex.Manufacturing;
                options.CostIndexReaction = costIndex.Reaction;
                options.CostIndexInvention = costIndex.Invention;
            }

            if (useMarketDepth)
            {
                if (regionId <= 0 && solarSystemId > 0)
                {
                    regionId = MapSolarSystemService.Query((int)solarSystemId)?.RegionID ?? 0;
                }
                options.MarketStats = await IndustryMarketDataService.Current.GetMarketStatsAsync(
                    regionId, forceRefresh, marketPageCallback, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        if (contexts == null || contexts.Count == 0)
        {
            options.CharacterProfiles.Add(IndustryCharacterProfileFactory.CreateDefault());
            return options;
        }

        foreach (var context in contexts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (context == null)
            {
                continue;
            }

            try
            {
                if (useOwnedBlueprints)
                {
                    var owned = await CharacterIndustryDataService.GetOwnedBlueprintsAsync(context, forceRefresh);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (owned is not null)
                    {
                        MergeMax(options.MEOverrides, CharacterIndustryDataService.BuildMeOverrides(owned));
                        MergeMax(options.TEOverrides, CharacterIndustryDataService.BuildTeOverrides(owned));
                        var stocks = CharacterIndustryDataService.BuildBlueprintStocks(owned);
                        foreach (var pair in stocks)
                        {
                            if (!options.BlueprintStocks.TryGetValue(pair.Key, out var existing))
                            {
                                options.BlueprintStocks[pair.Key] = pair.Value;
                            }
                            else
                            {
                                existing.HasBpo |= pair.Value.HasBpo;
                                existing.BpcRuns += pair.Value.BpcRuns;
                                existing.BpcCount += pair.Value.BpcCount;
                                existing.BpoMaterialEfficiency = Math.Max(existing.BpoMaterialEfficiency, pair.Value.BpoMaterialEfficiency);
                                existing.BpoTimeEfficiency = Math.Max(existing.BpoTimeEfficiency, pair.Value.BpoTimeEfficiency);
                                existing.BpcMaterialEfficiency = Math.Max(existing.BpcMaterialEfficiency, pair.Value.BpcMaterialEfficiency);
                                existing.BpcTimeEfficiency = Math.Max(existing.BpcTimeEfficiency, pair.Value.BpcTimeEfficiency);
                            }
                        }
                    }
                }

                if (useInProgressJobs)
                {
                    var jobs = await CharacterIndustryDataService.GetInProgressOutputsAsync(context, forceRefresh);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (jobs is not null)
                    {
                        MergeSum(options.InProgressJobs, jobs);
                    }
                }

                if (useAssets)
                {
                    var assets = await CharacterIndustryDataService.GetAssetQuantitiesAsync(context, forceRefresh);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (assets is not null)
                    {
                        MergeSum(options.Assets, assets);
                    }
                }

                if (useCharacterSkills)
                {
                    var skills = await CharacterIndustryDataService.GetSkillLevelsAsync(context, forceRefresh);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (skills is not null)
                    {
                        MergeMax(options.CharacterSkills, skills);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        // 排产 profile 独立读取技能：即使某个角色的资产/蓝图/任务拉取失败，也仍保留该角色的槽位。
        foreach (var context in contexts)
        {
            if (context == null) continue;
            try
            {
                var skills = await CharacterIndustryDataService.GetSkillLevelsAsync(context, forceRefresh);
                options.CharacterProfiles.Add(IndustryCharacterProfileFactory.Create(context, skills));
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                options.CharacterProfiles.Add(IndustryCharacterProfileFactory.Create(context, null));
            }
        }

        if (options.CharacterProfiles.Count == 0)
        {
            options.CharacterProfiles.Add(IndustryCharacterProfileFactory.CreateDefault());
        }

        return options;
    }

    private static void MergeMax(Dictionary<int, int> target, Dictionary<int, int> source)
    {
        if (source == null) return;
        foreach (var pair in source)
        {
            if (!target.TryGetValue(pair.Key, out var existing) || pair.Value > existing)
            {
                target[pair.Key] = pair.Value;
            }
        }
    }

    private static void MergeSum(Dictionary<int, long> target, Dictionary<int, long> source)
    {
        if (source == null) return;
        foreach (var pair in source)
        {
            target[pair.Key] = target.TryGetValue(pair.Key, out var existing) ? existing + pair.Value : pair.Value;
        }
    }
}
