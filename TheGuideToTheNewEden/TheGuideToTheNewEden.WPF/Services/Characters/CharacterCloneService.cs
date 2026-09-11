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
    /// <summary>基地（当前激活克隆所在地点）名称。</summary>
    public string? HomeLocation { get; set; }

    /// <summary>上一次变更基地的时间。</summary>
    public DateTime? LastStationChange { get; set; }

    /// <summary>克隆数量：与 WinUI 一致，包含当前激活克隆。</summary>
    public int JumpCloneCount { get; set; }

    /// <summary>上一次远克（跳跃克隆）的时间。</summary>
    public DateTime? LastCloneJump { get; set; }

    /// <summary>当前激活克隆：地点取基地，脑插来自 GetActiveImplants。</summary>
    public CloneView? ActiveClone { get; set; }

    /// <summary>跳跃克隆列表（不含当前激活克隆）。</summary>
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

            var jumpClones = clones?.JumpClones ?? [];

            var data = new CharacterCloneData
            {
                // WinUI 显示的是"跳跃克隆数 + 1（当前激活克隆）"。
                JumpCloneCount = jumpClones.Count + 1,
                LastCloneJump = Normalize(clones?.LastCloneJumpDate),
                LastStationChange = Normalize(clones?.LastStationChangeDate),
            };

            // 一次性解析所有位置与植入体名称
            var locationIds = new List<long>();
            if (clones?.HomeLocation?.LocationId > 0)
            {
                locationIds.Add(clones.HomeLocation.LocationId.Value);
            }

            foreach (var clone in jumpClones)
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

            foreach (var clone in jumpClones)
            {
                data.Clones.Add(new CloneView
                {
                    LocationName = locationNames.GetValueOrDefault(clone.LocationId, clone.LocationId.ToString()),
                    CloneName = clone.Name,
                    Implants = (clone.Implants ?? []).Select(id => BuildImplant(id)).ToList(),
                });
            }

            // 当前激活克隆单独成项：地点即基地，名称沿用 WinUI 的 ClonePage_ActiveClone 键。
            data.ActiveClone = new CloneView
            {
                LocationName = data.HomeLocation ?? string.Empty,
                CloneName = Application.Current?.TryFindResource("ClonePage_ActiveClone") as string ?? "Active clone",
                Implants = (implants ?? []).Select(id => BuildImplant(id)).ToList(),
            };

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

    /// <summary>ESI 的 DateTime.MinValue 视为"无记录"。</summary>
    private static DateTime? Normalize(DateTime? value) =>
        value is null || value.Value == DateTime.MinValue ? null : value;

    private static async Task<Dictionary<long, string>> ResolveNamesAsync(List<long> ids)
    {
        // 克隆/家的位置可能是空间站（int 范围）或玩家结构（约 1e12），统一走分流解析。
        return await LocationNameResolver.ResolveAsync(ids);
    }
}
