using System.IO;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Models.Indusrty;

namespace TheGuideToTheNewEden.WPF.Services.Industry;

/// <summary>已保存计划中的一个目标。</summary>
public sealed class IndustrySavedTarget
{
    public int TypeId { get; set; }
    public long Quantity { get; set; }
}

/// <summary>
/// 已保存的制造计划：目标 + 完整参数快照（<see cref="IndustrySetting"/>）。
/// 载入后按当前价格/角色重新计算，结果不落盘。
/// </summary>
public sealed class IndustrySavedPlan
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public DateTime Updated { get; set; } = DateTime.Now;
    public List<IndustrySavedTarget> Targets { get; set; } = new();
    public IndustrySetting Settings { get; set; } = new();

    /// <summary>保存时的计算结果（含每个物品的参考单价/成本/利润/采购量）；null 表示未保存结果。</summary>
    public IndustryBuildPlan? Result { get; set; }

    /// <summary>结果快照时间（价格属于该时刻）。</summary>
    public DateTime? ResultSavedAt { get; set; }

    /// <summary>结果快照对应的参与角色数。</summary>
    public int CharacterCount { get; set; }
}

/// <summary>已保存计划的持久化：<c>Configs/IndustryPlans.json</c>。</summary>
public static class IndustryPlanService
{
    private static readonly string FilePath = Path.Combine(SettingsService.DataPath, "Configs", "IndustryPlans.json");

    /// <summary>BOM 树可能较深（求解器上限 64 层），放宽 Json.NET 的默认深度限制。</summary>
    private static readonly JsonSerializerSettings JsonSettings = new() { MaxDepth = 256 };

    public static List<IndustrySavedPlan> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new List<IndustrySavedPlan>();
            }

            var plans = JsonConvert.DeserializeObject<List<IndustrySavedPlan>>(File.ReadAllText(FilePath), JsonSettings);
            if (plans is null)
            {
                return new List<IndustrySavedPlan>();
            }

            foreach (var plan in plans)
            {
                if (string.IsNullOrEmpty(plan.Id))
                {
                    plan.Id = Guid.NewGuid().ToString("N");
                }
                plan.Targets ??= new List<IndustrySavedTarget>();
                plan.Settings ??= new IndustrySetting();
                if (plan.Result is not null)
                {
                    plan.Result.Targets ??= new List<IndustryBuildNode>();
                    plan.Result.Summary ??= new IndustryPlanSummary();
                    plan.Result.Summary.BuyQuantities ??= new Dictionary<int, long>();
                    plan.Result.Summary.BuildQuantities ??= new Dictionary<int, long>();
                    plan.Result.Summary.BuildRuns ??= new Dictionary<int, int>();
                    foreach (var target in plan.Result.Targets)
                    {
                        NormalizeNode(target);
                    }

                    if (plan.Result.Schedule is not null)
                    {
                        var schedule = plan.Result.Schedule;
                        schedule.Characters ??= new List<IndustryScheduleCharacter>();
                        schedule.Jobs ??= new List<IndustryScheduleJob>();
                        schedule.CriticalJobKeys ??= new List<string>();
                        foreach (var character in schedule.Characters)
                        {
                            character.Lines ??= new List<IndustryScheduleLine>();
                            foreach (var line in character.Lines)
                            {
                                line.JobKeys ??= new List<string>();
                            }
                        }
                        foreach (var job in schedule.Jobs)
                        {
                            job.Inputs ??= new List<IndustryScheduleMaterial>();
                            job.Outputs ??= new List<IndustryScheduleMaterial>();
                            job.Dependencies ??= new List<string>();
                            job.Dependents ??= new List<string>();
                        }
                    }
                }
            }
            return plans;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return new List<IndustrySavedPlan>();
        }
    }

    private static void NormalizeNode(IndustryBuildNode? node)
    {
        if (node is null) return;
        node.Inputs ??= new List<IndustryBuildNode>();
        foreach (var input in node.Inputs)
        {
            NormalizeNode(input);
        }
    }

    public static void Save(List<IndustrySavedPlan> plans)
    {
        try
        {
            var folder = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(plans, JsonSettings));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }
}
