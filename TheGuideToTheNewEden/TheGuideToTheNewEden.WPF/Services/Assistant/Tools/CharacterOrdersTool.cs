using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Business;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>角色（或军团）未结市场订单：状态、参考价差、是否被压单。</summary>
public sealed class CharacterOrdersTool : ITool
{
    public string Name => "character_orders";

    public string DisplayNameKey => "AssistantTool_character_orders";

    public string Description =>
        "查询某个角色（或军团）的未结市场订单，并给出与市场参考价的对比：物品、买卖方向、价格、剩余量与挂单地点、" +
        "当前市场参考价、价差、是否被压单。参数 characterName 用角色全名（省略 = 默认角色）；corporation=true 查军团订单；" +
        "onlyCompressed=true 只看被压单的行；top 控制条数（默认 12，上限 40）。" +
        "适合「我有哪些未结订单」「哪些被压单了」「这个价还能不能卖」。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["characterName"] = new JsonObject { ["type"] = "string", ["description"] = "角色全名（省略 = 默认角色）" },
            ["corporation"] = new JsonObject { ["type"] = "boolean", ["description"] = "true = 查军团订单（默认 false = 个人）" },
            ["onlyCompressed"] = new JsonObject { ["type"] = "boolean", ["description"] = "true = 只返回被压单/未知的行" },
            ["top"] = new JsonObject { ["type"] = "integer", ["description"] = "条数（默认 12，上限 40）" },
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

        var corporation = ToolArgs.ReadBool(args, "corporation", false);
        var onlyCompressed = ToolArgs.ReadBool(args, "onlyCompressed", false);
        var top = Math.Clamp(ToolArgs.ReadInt(args, "top", 12), 1, 40);

        var orders = corporation
            ? await CharacterOrderService.GetCorpOrdersAsync(characterCtx!, ct)
            : await CharacterOrderService.GetCharacterOrdersAsync(characterCtx!, ct);
        if (orders is null)
        {
            return new ToolResult(corporation
                ? "军团订单获取失败（需要军团相关权限，或 ESI 不可用/限流）。"
                : "订单获取失败（ESI 不可用、令牌失效或限流）。");
        }

        if (orders.Count == 0)
        {
            return new ToolResult(corporation ? "该角色所属军团当前没有未结订单。" : "该角色当前没有未结市场订单。");
        }

        // 与市场参考价对比（内部会取星域订单，可能稍慢）
        var statuses = await CharacterOrderService.BuildStatusAsync(orders, ct);
        var rows = onlyCompressed ? statuses.Where(s => s.Normal != true).ToList() : statuses;

        var compressed = statuses.Count(s => s.Normal != true);
        var builder = new StringBuilder();
        builder.AppendLine($"{(corporation ? "军团" : "个人")}未结订单 {statuses.Count} 条（被压单 {compressed} 条），显示前 {Math.Min(top, rows.Count)} 条：");

        foreach (var row in rows.Take(top))
        {
            var order = row.Target;
            var side = order.IsBuyOrder ? "收单" : "卖单";
            var name = order.InvType?.TypeName ?? $"类型 {order.TypeId}";
            var location = string.IsNullOrWhiteSpace(order.LocationName) ? $"LocationId {order.LocationId}" : order.LocationName;
            var state = row.Normal switch
            {
                true => "正常",
                false => "被压单",
                _ => "未知",
            };

            builder.AppendLine(
                $"- {name}｜{side} {ToolArgs.FormatIsk(order.Price)} ISK｜剩余 {order.VolumeRemain}/{order.VolumeTotal}｜{location}｜" +
                $"剩余 {order.RemainTime}｜参考价差 {ToolArgs.FormatIsk(row.Difference)}｜{state}");
        }

        if (rows.Count == 0)
        {
            builder.AppendLine("- （没有被压单的订单）");
        }

        return new ToolResult(builder.ToString().TrimEnd(), ParamSummary: $"{statuses.Count} 条未结订单，{compressed} 条被压单");
    }
}
