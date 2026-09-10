using System.Windows;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WPF.Services.Characters;

public sealed class IndustryJobView
{
    public string BlueprintName { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string StatusText { get; set; } = string.Empty;

    public string LocationName { get; set; } = string.Empty;

    public int Runs { get; set; }

    public double Probability { get; set; }

    public double Cost { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }
}

/// <summary>角色工业任务。</summary>
public static class CharacterIndustryService
{
    private const string CacheKey = "industry";

    /// <summary>工业任务 5 分钟内缓存。</summary>
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public static async Task<List<IndustryJobView>?> GetAsync(CharacterContext context, bool forceRefresh = false)
    {
        if (!forceRefresh && CharacterCache.TryGet<List<IndustryJobView>>(context.CharacterId, CacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        if (!await context.EnsureTokenValidAsync())
        {
            return null;
        }

        try
        {
            var jobs = (await context.Api.Industry.ListCharacterIndustryJobsAsync(context.Auth, true)).Model ?? [];

            var locationIds = jobs.Select(p => p.FacilityId).Where(p => p > 0).Distinct().ToList();
            var locationNames = await ResolveNamesAsync(locationIds);

            var views = jobs.Select(job => new IndustryJobView
            {
                BlueprintName = InvTypeService.QueryType(job.BlueprintTypeId)?.TypeName ?? job.BlueprintTypeId.ToString(),
                ProductName = job.ProductTypeId.HasValue
                    ? InvTypeService.QueryType(job.ProductTypeId.Value)?.TypeName ?? job.ProductTypeId.Value.ToString()
                    : "-",
                StatusText = ResolveStatus(job.Status, job.EndDate),
                LocationName = locationNames.GetValueOrDefault(job.FacilityId, job.FacilityId.ToString()),
                Runs = (int)job.Runs,
                Probability = job.Probability ?? 0,
                Cost = job.Cost ?? 0,
                StartDate = job.StartDate,
                EndDate = job.EndDate,
            }).ToList();

            CharacterCache.Set(context.CharacterId, CacheKey, views, Ttl);
            return views;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    /// <summary>已到期但状态仍为 active 的任务显示为"已完成"（与 WinUI 版处理一致）。</summary>
    private static string ResolveStatus(string? status, DateTime? endDate)
    {
        if (status == "active" && endDate is { } end && end <= DateTime.UtcNow)
        {
            status = "done";
        }

        var key = $"IndustryPage_Status_{status}";
        return Application.Current?.TryFindResource(key) as string ?? status ?? "-";
    }

    private static async Task<Dictionary<long, string>> ResolveNamesAsync(List<long> ids)
    {
        var result = new Dictionary<long, string>();
        if (ids.Count == 0)
        {
            return result;
        }

        try
        {
            var names = await IDNameService.GetByIdsAsync(ids);
            foreach (var item in names ?? [])
            {
                if (!string.IsNullOrEmpty(item.Name))
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