using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.Models.Indusrty;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.Industry;

/// <summary>
/// 把工业数据源（公开价格/成本指数 + 角色蓝图/任务/资产）组装成 <see cref="IndustrySolverOptions"/>，
/// 供 <c>IndustrySolver</c> 直接使用。
/// </summary>
public static class IndustryOptionsFactory
{
    /// <summary>
    /// 组装求解参数。
    /// <paramref name="solarSystemId"/> 决定用哪套成本指数（0 = 不计算安装费）；
    /// <paramref name="context"/> 为空时只做数量展开与公开价格（不抵扣角色库存）。
    /// </summary>
    public static async Task<IndustrySolverOptions> BuildAsync(
        CharacterContext? context,
        long solarSystemId,
        double facilityTax = 0.10,
        int defaultMe = 10,
        int defaultTe = 20,
        double spaceModifier = 1.0,
        double rigMeBonus = 0,
        double rigTeBonus = 0,
        bool useAssets = true,
        bool useInProgressJobs = true,
        bool useOwnedBlueprints = true,
        bool forceRefresh = false)
    {
        var options = new IndustrySolverOptions
        {
            DefaultME = defaultMe,
            DefaultTE = defaultTe,
            FacilityTax = facilityTax,
            SpaceModifier = spaceModifier,
            RigMeBonus = rigMeBonus,
            RigTeBonus = rigTeBonus,
        };

        try
        {
            options.AdjustedPrices = await IndustryMarketDataService.Current.GetAdjustedPricesAsync(forceRefresh);
            options.AveragePrices = await IndustryMarketDataService.Current.GetAveragePricesAsync(forceRefresh);

            var costIndices = await IndustryMarketDataService.Current.GetCostIndicesAsync(forceRefresh);
            if (solarSystemId > 0 && costIndices.TryGetValue(solarSystemId, out var costIndex))
            {
                options.CostIndexManufacturing = costIndex.Manufacturing;
                options.CostIndexReaction = costIndex.Reaction;
                options.CostIndexInvention = costIndex.Invention;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        if (context is null)
        {
            return options;
        }

        if (useOwnedBlueprints)
        {
            var owned = await CharacterIndustryDataService.GetOwnedBlueprintsAsync(context, forceRefresh);
            if (owned is not null)
            {
                Merge(options.MEOverrides, CharacterIndustryDataService.BuildMeOverrides(owned));
                Merge(options.TEOverrides, CharacterIndustryDataService.BuildTeOverrides(owned));
            }
        }

        if (useInProgressJobs)
        {
            var jobs = await CharacterIndustryDataService.GetInProgressOutputsAsync(context, forceRefresh);
            if (jobs is not null)
            {
                options.InProgressJobs = jobs;
            }
        }

        if (useAssets)
        {
            var assets = await CharacterIndustryDataService.GetAssetQuantitiesAsync(context, forceRefresh);
            if (assets is not null)
            {
                options.Assets = assets;
            }
        }

        return options;
    }

    private static void Merge(Dictionary<int, int> target, Dictionary<int, int> source)
    {
        foreach (var pair in source)
        {
            if (!target.TryGetValue(pair.Key, out var existing) || pair.Value > existing)
            {
                target[pair.Key] = pair.Value;
            }
        }
    }
}
