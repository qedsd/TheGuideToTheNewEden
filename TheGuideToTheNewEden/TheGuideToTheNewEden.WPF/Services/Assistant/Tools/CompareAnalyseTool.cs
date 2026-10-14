using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.Services.Business;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 比价分析（重型：多市场 × 多物品取数，需用户确认后执行）：返回最优市场分布 + 集中度建议。
/// side 决定物品归属组：sell=对比"在哪里卖出到手最大"（默认），buy=对比"在哪里买入最便宜"。
/// </summary>
public sealed class CompareAnalyseTool : ITool
{
    public string Name => "compare_analyse";

    public string DisplayNameKey => "AssistantTool_compare_analyse";

    public string Description =>
        "运行多市场比价分析：多个市场 × 多个物品，找出每个物品的最优卖出/买入市场与价格差异、市场集中度建议。" +
        "按当前比价设置的市场列表执行，取数涉及多个市场，需要用户确认。" +
        "参数 items 是要比价的物品名数组（游戏内名称）；side 选 sell（在哪里卖出到手最大，默认）或 buy（在哪里买入最便宜）；" +
        "top 控制返回条数。返回的价格与价差一律以工具数据为准，禁止自行计算。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["items"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" }, ["description"] = "要比价的物品名数组" },
            ["side"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "sell", "buy" }, ["description"] = "卖出组/买入组（默认 sell）" },
            ["top"] = new JsonObject { ["type"] = "integer", ["description"] = "返回条数（默认 10，上限 20）" },
        },
        ["required"] = new JsonArray { "items" },
    };

    public bool RequiresConfirmation => true;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var itemNames = ReadStringArray(args, "items");
        if (itemNames.Count == 0)
        {
            return new ToolResult("参数 items 不能为空（至少一个物品名）。");
        }

        var side = ToolArgs.ReadString(args, "side") ?? "sell";
        var group = side == "buy" ? CompareItemGroup.Buy : CompareItemGroup.Sell;
        var top = Math.Clamp(ToolArgs.ReadInt(args, "top", 10), 1, 20);

        var inputs = new List<CompareMarketInputItem>();
        var missing = new List<string>();
        foreach (var name in itemNames)
        {
            var invType = ToolNameResolver.ResolveItem(name);
            if (invType is null)
            {
                missing.Add(name);
                continue;
            }

            inputs.Add(new CompareMarketInputItem { TypeId = invType.TypeID, Name = invType.TypeName, Amount = 1, Group = group });
        }

        if (inputs.Count == 0)
        {
            return new ToolResult($"没有解析出任何物品（未找到：{string.Join("、", missing)}）。");
        }

        var setting = CompareMarketSettingService.Load();
        if ((setting.Markets ?? []).Count == 0)
        {
            return new ToolResult("比价设置里没有市场，请先在「商业 → 比价」页配置市场列表。");
        }

        var result = await CompareMarketService.RunAsync(setting, inputs, ct,
            (progress) => ctx.Progress?.Invoke($"取数 {progress.MarketName} {progress.Done}/{progress.Total}"));
        if (result is null || !result.HasRows)
        {
            return new ToolResult("比价无结果（可能已取消或取数全部失败）。");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"比价（{(group == CompareItemGroup.Sell ? "卖出组" : "买入组")}）· {inputs.Count} 物品 × {result.Markets.Count} 市场");

        foreach (var row in result.Rows.OrderByDescending(r => BestDiff(r)).Take(top))
        {
            var best = BestCell(row, out var bestIndex);
            if (bestIndex < 0 || bestIndex >= result.Markets.Count)
            {
                continue;
            }

            var marketName = result.Markets[bestIndex].Name ?? "?";
            var second = SecondBestDiff(row);
            builder.AppendLine($"{row.Name}：最优 {marketName}（{ToolArgs.FormatIsk(best)}）" +
                (second is { } d ? $" · 与次优差 {d:F1}%" : string.Empty));
        }

        if (result.NoQuoteCount > 0)
        {
            builder.AppendLine($"无报价 {result.NoQuoteCount} 件");
        }

        AppendConcentration(builder, "卖出", result.SellConcentration);
        AppendConcentration(builder, "买入", result.BuyConcentration);
        if (result.Failures is { Count: > 0 })
        {
            builder.AppendLine("取数失败市场：" + string.Join("、", result.Failures.Select(f => f.MarketName)));
        }

        if (missing.Count > 0)
        {
            builder.AppendLine($"未找到物品：{string.Join("、", missing)}");
        }

        return new ToolResult(builder.ToString().TrimEnd(), ParamSummary: $"{inputs.Count} 物品 × {result.Markets.Count} 市场（{group}）");
    }

    private static List<string> ReadStringArray(JsonElement args, string name)
    {
        var list = new List<string>();
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            {
                list.Add(item.GetString()!);
            }
        }

        return list;
    }

    /// <summary>行内最优格子的参考价（sell 取卖出参考价最大，buy 取买入参考价最小）。</summary>
    private static double BestCell(CompareMarketRow row, out int index)
    {
        index = -1;
        var best = 0.0;
        for (var i = 0; i < row.Cells.Count; i++)
        {
            var cell = row.Cells[i];
            if (cell.MarketFailed)
            {
                continue;
            }

            var value = row.Group == CompareItemGroup.Sell ? cell.SellPrice : cell.BuyPrice;
            if (index < 0 || (row.Group == CompareItemGroup.Sell ? value > best : value < best))
            {
                best = value;
                index = i;
            }
        }

        return best;
    }

    /// <summary>行内最优与次优的差%（按组口径）。</summary>
    private static double? SecondBestDiff(CompareMarketRow row)
    {
        var values = row.Cells.Where(c => !c.MarketFailed)
            .Select(c => row.Group == CompareItemGroup.Sell ? c.SellPrice : c.BuyPrice)
            .Where(v => v > 0)
            .OrderByDescending(v => v)
            .ToList();
        if (values.Count < 2)
        {
            return null;
        }

        var first = values[0];
        var second = values[1];
        return second <= 0 ? null : Math.Abs(first - second) / second * 100;
    }

    private static double BestDiff(CompareMarketRow row) => SecondBestDiff(row) ?? 0;

    private static void AppendConcentration(StringBuilder builder, string label, ConcentrationResult? concentration)
    {
        if (concentration is null || !concentration.HasResult)
        {
            return;
        }

        builder.AppendLine($"{label}集中建议：{concentration.MarketName} · 覆盖 {concentration.CoveredCount}/{concentration.TotalCount} 件" +
            $" · 分散可差 {concentration.GainPercent:F1}%（{ToolArgs.FormatIsk(concentration.Gain)}）");
    }
}
