using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Business;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>当前倒货设置（源/目的市场、目标物品数、取数线程）。</summary>
public sealed class ScalperSettingsTool : ITool
{
    public string Name => "scalper_settings";

    public string DisplayNameKey => "AssistantTool_scalper_settings";

    public string Description =>
        "查看当前倒货分析的设置：源市场、目的市场、目标物品数量、取数线程数。适合「倒货现在是什么配置」「现在分析的范围」。";

    public JsonObject? Parameters => null;

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var setting = ScalperSettingService.Load();
        var source = setting.SourceMarketLocation;
        var destination = setting.DestinationMarketLocation;
        var targetCount = setting.MarketTypes?.Length ?? 0;
        var threads = Math.Max(1, MarketOrderSettingService.ThreadValue);

        var builder = new StringBuilder();
        builder.AppendLine($"源市场 {(source?.Name ?? "未设置")} · 目的市场 {(destination?.Name ?? "未设置")}");
        builder.AppendLine($"目标物品 {targetCount} 件 · 取数线程 {threads}");

        if (source is null || destination is null || targetCount == 0)
        {
            builder.AppendLine("（请先在「商业 → 倒货」页配置源/目的市场与目标物品，再让 AI 分析）");
        }

        return Task.FromResult(new ToolResult(builder.ToString().TrimEnd(),
            ParamSummary: $"{(source?.Name ?? "?")} → {(destination?.Name ?? "?")} · {targetCount} 件"));
    }
}
