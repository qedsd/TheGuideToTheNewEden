using System.Windows;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WPF.Services.Characters;

public sealed class CloneImplantView
{
    public long TypeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}

public sealed class CloneView
{
    public string LocationName { get; set; } = string.Empty;

    public string? CloneName { get; set; }

    public List<CloneImplantView> Implants { get; set; } = [];
}

public sealed class CharacterCloneData
{
    public string? HomeLocation { get; set; }

    public DateTime? LastStationChange { get; set; }

    public int JumpCloneCount { get; set; }

    public DateTime? LastCloneJump { get; set; }

    public List<CloneView> Clones { get; set; } = [];

    public DateTime UpdatedUtc { get; set; }
}

/// <summary>克隆：家station、跳跃克隆与植入体。</summary>
public static class CharacterCloneService
{
    private const string CacheKey = "clones";

    /// <summary>克隆信息变化慢，缓存 30 分钟。</summary>
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    public static async Task<CharacterCloneData?> GetAsync(CharacterContext context, bool forceRefresh = false)
    {
        if (!forceRefresh && CharacterCache.TryGet<CharacterCloneData>(context.CharacterId, CacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        if (!await context.EnsureTokenValidAsync())
        {
            return null;
        }

        try
        {
            var auth = context.Auth;
            var clones = (await context.Api.Clones.GetClonesAsync(auth)).Model;
            var implants = (await context.Api.Clones.GetActiveImplantsAsync(auth)).Model;

            var data = new CharacterCloneData
            {
                JumpCloneCount = clones?.JumpClones?.Count ?? 0,
                LastCloneJump = clones?.LastCloneJumpDate,
            };

            // 一次性解析所有位置与植入体名称
            var locationIds = new List<long>();
            if (clones?.HomeLocation?.LocationId > 0)
            {
                locationIds.Add(clones.HomeLocation.LocationId.Value);
            }

            foreach (var clone in clones?.JumpClones ?? [])
            {
                if (clone.LocationId > 0)
                {
                    locationIds.Add(clone.LocationId);
                }
            }

            var locationNames = await ResolveNamesAsync(locationIds);
            data.HomeLocation = clones?.HomeLocation?.LocationId is { } homeId
                ? locationNames.GetValueOrDefault(homeId, homeId.ToString())
                : null;
            data.LastStationChange = null;

            foreach (var clone in clones?.JumpClones ?? [])
            {
                data.Clones.Add(new CloneView
                {
                    LocationName = locationNames.GetValueOrDefault(clone.LocationId, clone.LocationId.ToString()),
                    CloneName = clone.Name,
                    Implants = (clone.Implants ?? []).Select(id => BuildImplant(id)).ToList(),
                });
            }

            // 当前克隆的植入体
            if (implants is { Count: > 0 })
            {
                data.Clones.Insert(0, new CloneView
                {
                    LocationName = Application.Current?.TryFindResource("Characters.Clone.Active") as string ?? "Active clone",
                    Implants = implants.Select(id => BuildImplant(id)).ToList(),
                });
            }

            data.UpdatedUtc = DateTime.UtcNow;
            CharacterCache.Set(context.CharacterId, CacheKey, data, Ttl);
            return data;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    private static CloneImplantView BuildImplant(long typeId)
    {
        var type = InvTypeService.QueryType(typeId);
        return new CloneImplantView
        {
            TypeId = typeId,
            Name = type?.TypeName ?? typeId.ToString(),
            Description = type?.Description,
        };
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
            var names = await IDNameService.GetByIdsAsync(ids.Distinct().ToList());
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