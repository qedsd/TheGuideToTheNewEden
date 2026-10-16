using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>角色工业任务：蓝图/产物、状态、剩余时间、所在设施、成功概率与成本。</summary>
public sealed class CharacterIndustryTool : ITool
{
    public string Name => "character_industry";

    public string DisplayNameKey => "AssistantTool_character_industry";

    public string Description =>
        "查询某个角色正在进行的工业任务（制造/研发等）：蓝图、产物、状态、进度、开始与完成时间、剩余时间、设施、成功概率、成本。" +
        "角色全名（省略 = 默认角色）；条数默认 10、上限 30。" +
        "适合「我有哪些活还在跑」「什么时候能造完」。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["characterName"] = new JsonObject { ["type"] = "string", ["description"] = "角色全名（省略 = 默认角色）" },
            ["top"] = new JsonObject { ["type"] = "integer", ["description"] = "条数（默认 10，上限 30）" },
        },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var (characterCtx, error) = await CharacterToolHelper.ResolveAsync(ctx, ToolArgs.ReadString(args, "characterName"), ct);
        if (error is not null)
        {
            return new ToolResult(error);
        }

        var top = Math.Clamp(ToolArgs.ReadInt(args, "top", 10), 1, 30);
        var jobs = await CharacterIndustryService.GetAsync(characterCtx!, false);
        if (jobs is null)
        {
            return new ToolResult("工业任务获取失败（ESI 不可用、缺少工业权限或限流）。");
        }

        if (jobs.Count == 0)
        {
            return new ToolResult("该角色当前没有工业任务。");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"工业任务 {jobs.Count} 条，显示前 {Math.Min(top, jobs.Count)} 条：");
        foreach (var job in jobs.Take(top))
        {
            var end = job.EndDate is { } finishTime ? $"｜完成 {finishTime:MM-dd HH:mm}" : string.Empty;
            var remaining = job.EndDate is { } finish
                ? $"｜剩余 {FormatRemaining(finish - DateTime.Now)}"
                : string.Empty;
            var location = string.IsNullOrWhiteSpace(job.LocationName) ? string.Empty : $"｜{job.LocationName}";
            builder.AppendLine(
                $"- [{job.StatusText}] {job.BlueprintName} → {job.ProductName}×{job.Runs}｜{job.Probability:P0}{end}{remaining}{location}｜成本 {ToolArgs.FormatIsk(job.Cost)}");
        }

        return new ToolResult(builder.ToString().TrimEnd(), ParamSummary: $"{jobs.Count} 条工业任务");
    }

    private static string FormatRemaining(TimeSpan span)
    {
        if (span <= TimeSpan.Zero)
        {
            return "已完成待取";
        }

        return span.TotalDays >= 1
            ? $"{(int)span.TotalDays} 天 {span.Hours} 小时"
            : $"{span.Hours} 小时 {span.Minutes} 分";
    }
}
