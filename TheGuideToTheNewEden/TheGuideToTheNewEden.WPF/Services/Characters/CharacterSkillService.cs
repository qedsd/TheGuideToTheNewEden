using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WPF.Services.Characters;

public sealed class SkillView
{
    public long SkillId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Level { get; set; }

    public long SkillPoints { get; set; }
}

public sealed class SkillGroupView
{
    public string GroupName { get; set; } = string.Empty;

    public List<SkillView> Skills { get; set; } = [];
}

public sealed class SkillQueueView
{
    public long SkillId { get; set; }

    public string SkillName { get; set; } = string.Empty;

    public int FinishedLevel { get; set; }

    public DateTime? Start { get; set; }

    public DateTime? Finish { get; set; }

    public bool IsRunning { get; set; }
}

public sealed class CharacterSkillData
{
    public long TotalSkillPoints { get; set; }

    public long UnallocatedSkillPoints { get; set; }

    public List<SkillGroupView> Groups { get; set; } = [];

    public List<SkillQueueView> Queue { get; set; } = [];

    public DateTime UpdatedUtc { get; set; }
}

/// <summary>角色技能与技能队列。技能名称/分组来自本地数据库，无需额外 ESI 请求。</summary>
public static class CharacterSkillService
{
    private const string CacheKey = "skills";

    /// <summary>技能变化慢，缓存 30 分钟。</summary>
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    public static async Task<CharacterSkillData?> GetAsync(CharacterContext context, bool forceRefresh = false)
    {
        if (!forceRefresh && CharacterCache.TryGet<CharacterSkillData>(context.CharacterId, CacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        if (!await context.EnsureTokenValidAsync())
        {
            return null;
        }

        var data = new CharacterSkillData();

        try
        {
            var auth = context.Auth;
            var skills = (await context.Api.Skills.GetCharacterSkillsAsync(auth)).Model;
            var queue = (await context.Api.Skills.GetCharacterSkillQueueAsync(auth)).Model;

            var trained = skills?.Skills?
                .GroupBy(p => p.SkillId)
                .ToDictionary(g => g.Key, g => g.First())
                ?? [];

            data.TotalSkillPoints = skills?.TotalSp ?? 0;
            data.UnallocatedSkillPoints = skills?.UnallocatedSp ?? 0;

            // 技能分组与名称来自本地静态数据库
            var groups = await InvGroupService.QuerySkillGroupsAsync();
            if (groups is not null)
            {
                foreach (var group in groups)
                {
                    var view = new SkillGroupView { GroupName = group.GroupName };
                    foreach (var skillId in group.SkillIds ?? [])
                    {
                        if (!trained.TryGetValue(skillId, out var skill))
                        {
                            continue;
                        }

                        view.Skills.Add(new SkillView
                        {
                            SkillId = skillId,
                            Name = InvTypeService.QueryType(skillId)?.TypeName ?? skillId.ToString(),
                            Level = (int)skill.TrainedSkillLevel,
                            SkillPoints = skill.SkillpointsInSkill,
                        });
                    }

                    if (view.Skills.Count > 0)
                    {
                        view.Skills.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
                        data.Groups.Add(view);
                    }
                }
            }

            if (queue is not null)
            {
                foreach (var item in queue.OrderBy(p => p.QueuePosition))
                {
                    data.Queue.Add(new SkillQueueView
                    {
                        SkillId = item.SkillId,
                        SkillName = InvTypeService.QueryType(item.SkillId)?.TypeName ?? item.SkillId.ToString(),
                        FinishedLevel = (int)item.FinishedLevel,
                        Start = item.StartDate,
                        Finish = item.FinishDate,
                        IsRunning = item.FinishDate.HasValue && item.StartDate.HasValue
                                     && item.StartDate <= DateTime.UtcNow && item.FinishDate > DateTime.UtcNow,
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }

        data.UpdatedUtc = DateTime.UtcNow;
        CharacterCache.Set(context.CharacterId, CacheKey, data, Ttl);
        return data;
    }
}