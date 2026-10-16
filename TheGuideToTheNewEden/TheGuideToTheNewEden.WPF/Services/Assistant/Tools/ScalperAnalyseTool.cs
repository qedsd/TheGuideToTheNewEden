using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MarketOrder = TheGuideToTheNewEden.Core.Models.Market.Order;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.Services.Business;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 倒货分析（重型：全量取数 + 计算，需用户确认后执行）：返回按推荐度排序的 top-N 结果，供 AI 给出买入建议。
/// 管线与 <see cref="ScalperPageViewModel"/> 一致：源/目的市场订单（并发分页）→ 全物品历史 → ScalperCalculator。
/// </summary>
public sealed class ScalperAnalyseTool : ITool
{
    public string Name => "scalper_analyse";

    public string DisplayNameKey => "AssistantTool_scalper_analyse";

    public string Description =>
        "运行倒货分析（只读），按推荐度列出买入候选：净利润、回报率、本金、销量、热力。" +
        "取数分钟级、大量 ESI 调用，需用户确认后执行；" +
        "返回的利润率、净利润、销量一律以工具数据为准，禁止自行计算。" +
        "适合「这价还能不能倒」。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["top"] = new JsonObject { ["type"] = "integer", ["description"] = "返回条数（默认 15，上限 30）" },
            ["minProfitIsk"] = new JsonObject { ["type"] = "number", ["description"] = "最小净利润过滤（可选）" },
            ["minRoi"] = new JsonObject { ["type"] = "number", ["description"] = "最小回报率（百分数，可选）" },
        },
    };

    public bool RequiresConfirmation => true;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var top = Math.Clamp(ToolArgs.ReadInt(args, "top", 15), 1, 30);
        var minProfit = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("minProfitIsk", out var profitNode)
            ? profitNode.GetDouble()
            : 0;
        var minRoi = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("minRoi", out var roiNode)
            ? roiNode.GetDouble()
            : 0;

        var setting = ScalperSettingService.Load();
        var source = setting.SourceMarketLocation;
        var destination = setting.DestinationMarketLocation;
        var marketTypes = (setting.MarketTypes ?? []).Where(t => t > 0).Distinct().ToHashSet();
        if (source is null || destination is null || marketTypes.Count == 0)
        {
            return new ToolResult("倒货设置不完整：请先在「商业 → 倒货」页配置源/目的市场与目标物品。");
        }

        var skipStructure = MarketOrderSettingService.ScalperSikpStructureValue;

        var sourceOrders = await GetOrdersOfLocationAsync(source, skipStructure, "获取源市场订单中", ctx, ct);
        if (sourceOrders is null)
        {
            return new ToolResult("获取源市场订单失败（ESI 不可用或限流）。");
        }

        var destinationOrders = await GetOrdersOfLocationAsync(destination, skipStructure, "获取目的市场订单中", ctx, ct);
        if (destinationOrders is null)
        {
            return new ToolResult("获取目的市场订单失败（ESI 不可用或限流）。");
        }

        var items = ScalperCalculator.BuildItems(sourceOrders, destinationOrders, marketTypes);
        if (items.Count == 0)
        {
            return new ToolResult("没有可分析的物品（订单为空或目标物品未命中）。");
        }

        var typeIds = items.Select(p => p.InvType.TypeID).Distinct().ToList();
        ctx.Progress?.Invoke("获取源市场历史中");
        var sourceHistory = await MarketOrderService.Current.GetHistoryBatchAsync(
            typeIds, source.RegionId, ct, (done, total) => ctx.Progress?.Invoke($"获取源市场历史中 {done}/{total}"));
        ctx.Progress?.Invoke("获取目的市场历史中");
        var destinationHistory = await MarketOrderService.Current.GetHistoryBatchAsync(
            typeIds, destination.RegionId, ct, (done, total) => ctx.Progress?.Invoke($"获取目的市场历史中 {done}/{total}"));

        ScalperCalculator.SetHistory(items, sourceHistory, destinationHistory);
        var results = ScalperCalculator.Calculate(items, setting);

        var filtered = results
            .Where(p => p.NetProfit >= minProfit && p.ROI >= minRoi)
            .OrderByDescending(p => p.Suggestion)
            .Take(top)
            .ToList();
        if (filtered.Count == 0)
        {
            return new ToolResult($"分析完成但没有符合过滤条件（净利润 ≥ {ToolArgs.FormatIsk(minProfit)}、回报率 ≥ {minRoi}%）的候选。");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"倒货分析（{source.Name} → {destination.Name}）· 命中 {results.Count} / 过滤后 {filtered.Count}，前 {top}:");
        foreach (var item in filtered)
        {
            builder.AppendLine(
                $"{item.InvType.TypeName} | 源买 {ToolArgs.FormatIsk(item.BuyPrice)} | 目的卖 {ToolArgs.FormatIsk(item.SellPrice)} | " +
                $"净利润 {ToolArgs.FormatIsk(item.NetProfit)} | 回报率 {item.ROI:F1}% | 本金 {ToolArgs.FormatIsk(item.Principal)} | " +
                $"销量 {item.SourceSales:N0} | 热力 {item.HeatValue} | 推荐度 {item.Suggestion:F1}");
        }

        return new ToolResult(builder.ToString().TrimEnd(), ParamSummary: $"{source.Name} → {destination.Name} · {filtered.Count} 条");
    }

    private static async Task<List<MarketOrder>?> GetOrdersOfLocationAsync(MarketLocation location, bool skipStructure, string prefix, ToolContext ctx, CancellationToken ct)
    {
        switch (location.Type)
        {
            case MarketLocationType.Region:
                return await MarketOrderService.Current.GetAllRegionOrdersAsync(
                    location.Id, skipStructure, ct,
                    (page, total) => ctx.Progress?.Invoke($"{prefix} {page}/{total}"));

            case MarketLocationType.SolarSystem:
                return await MarketOrderService.Current.GetSolarSystemOrdersAsync(
                    (int)location.Id, skipStructure, ct,
                    (page, total) => ctx.Progress?.Invoke($"{prefix} {page}/{total}"));

            case MarketLocationType.Structure:
                return await MarketOrderService.Current.GetStructureOrdersAsync(location.Id, ct);

            default:
                return null;
        }
    }
}
