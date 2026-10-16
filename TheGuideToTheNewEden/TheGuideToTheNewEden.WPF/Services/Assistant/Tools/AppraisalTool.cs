using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.Services.Business;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>估价一份物品清单（多行粘贴）：总买价/中间价/总卖价/总体积 + 明细（口径走当前估价设置）。</summary>
public sealed class AppraisalTool : ITool
{
    public string Name => "appraisal";

    public string DisplayNameKey => "AssistantTool_appraisal";

    public string Description =>
        "估价一份物品清单（游戏里复制的多行清单，每行「名称 数量」）。返回总买价、中间价、总卖价、总体积与明细。" +
        "可指定星域名（默认 伏尔戈）；清单超过 200 件时请分批。适合「帮我估价这份清单」「这批货值多少钱」。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["text"] = new JsonObject { ["type"] = "string", ["description"] = "多行物品清单（每行 名称 数量）" },
            ["regionName"] = new JsonObject { ["type"] = "string", ["description"] = "估价市场所在星域（默认 伏尔戈）" },
        },
        ["required"] = new JsonArray { "text" },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var text = ToolArgs.ReadString(args, "text");
        if (string.IsNullOrWhiteSpace(text))
        {
            return new ToolResult("参数 text 不能为空（多行物品清单）。");
        }

        var parsed = AppraisalTextParser.Parse(text);
        if (parsed.Count == 0)
        {
            return new ToolResult("没有解析出任何物品，请用游戏内复制格式（每行 名称 数量）。");
        }

        if (parsed.Count > 200)
        {
            return new ToolResult($"清单超过 200 件（{parsed.Count} 件），请分批估价。");
        }

        var setting = AppraisalSettingService.Load();
        var regionName = ToolArgs.ReadString(args, "regionName");
        if (!string.IsNullOrWhiteSpace(regionName))
        {
            var region = ToolNameResolver.ResolveRegion(regionName);
            if (region is null)
            {
                return new ToolResult($"未找到星域「{regionName}」。");
            }

            setting.Location = new MarketLocation(region);
            setting.RegionId = region.RegionID;
        }

        var result = await AppraisalService.GetEstimateAsync(text, setting, ct, (done, total) => ctx.Progress?.Invoke($"估价中 {done}/{total}"));
        if (result is null)
        {
            return new ToolResult("估价失败（ESI 不可用或限流）。");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"{result.MarketName} · {result.Items.Count} 件 · 总体积 {result.TotalVolume:N2} m³");
        builder.AppendLine($"总买价 {ToolArgs.FormatIsk(result.TotalBuyPrice)} · 中间价 {ToolArgs.FormatIsk(result.TotalSplitPrice)} · 总卖价 {ToolArgs.FormatIsk(result.TotalSellPrice)}");
        foreach (var item in result.Items.Take(15))
        {
            builder.AppendLine($"{item.Name} ×{item.Amount:0} = 买 {ToolArgs.FormatIsk(item.BuyPrice)} / 卖 {ToolArgs.FormatIsk(item.SellPrice)}");
        }

        var notes = new List<string>();
        if (result.UnresolvedLines.Count > 0)
        {
            notes.Add($"未识别 {result.UnresolvedLines.Count} 行");
        }

        if (result.NoPriceItems.Count > 0)
        {
            notes.Add($"无报价 {result.NoPriceItems.Count} 件");
        }

        if (notes.Count > 0)
        {
            builder.AppendLine(string.Join(" · ", notes));
        }

        return new ToolResult(builder.ToString().TrimEnd(), ParamSummary: $"{result.Items.Count} 件 @ {result.MarketName}");
    }
}
