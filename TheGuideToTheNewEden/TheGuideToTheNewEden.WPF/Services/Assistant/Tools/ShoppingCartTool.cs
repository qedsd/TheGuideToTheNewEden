using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.Services.Business;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 倒货购物车写入（**写操作，需确认**）：把物品加进「商业 → 倒货 → 购物车」（共享
/// <see cref="BusinessService.ShoppingCart"/>），或按物品名/序号移除。
/// <para>
/// 价格由调用方给（通常取自 <c>scalper_analyse</c> / <c>trade_spread</c> / <c>market_orders</c> 的结果）——
/// 本工具**不自己取价**：一是避免"确认卡片上写 A、实际按 B 算"，二是购物车本来就允许用户手改价格。
/// 同一物品重复加入按"数量累加"，不产生重复行（与购物车页的编辑语义一致）。
/// </para>
/// </summary>
public sealed class ShoppingCartTool : ITool
{
    public string Name => "shopping_cart";

    public string DisplayNameKey => "AssistantTool_shopping_cart";

    public string Description =>
        "维护「商业 → 倒货 → 购物车」的内容：action=add（默认）把物品加进购物车，" +
        "action=remove 移除（itemName 给物品名，或用 index 给购物车里的行号，从 1 开始）；" +
        "add 需要 itemName 与 quantity，buyPrice（买入价）/ sellPrice（卖出价）建议一并给" +
        "（取自倒货分析/利差工具的结果，本工具不代为取价；缺省 0 表示待用户自己填），jumps 是两市场跳数（默认 0）。" +
        "写操作需要用户确认；购物车里已有的同一物品会按数量累加。" +
        "适合用户说「把这几样加进购物车」「算一下我选中这些要多少本金」；购物车不做持久化，关软件即清空（要留存请用购物车页的「保存记录」）。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["action"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "add", "remove" }, ["description"] = "add（默认）= 加入；remove = 移除" },
            ["itemName"] = new JsonObject { ["type"] = "string", ["description"] = "物品名（游戏内名称，中英文均可）" },
            ["quantity"] = new JsonObject { ["type"] = "number", ["description"] = "数量（add 时必填，> 0）" },
            ["buyPrice"] = new JsonObject { ["type"] = "number", ["description"] = "买入单价（缺省 0）" },
            ["sellPrice"] = new JsonObject { ["type"] = "number", ["description"] = "卖出单价（缺省 0）" },
            ["jumps"] = new JsonObject { ["type"] = "integer", ["description"] = "源/目的市场跳数（默认 0）" },
            ["index"] = new JsonObject { ["type"] = "integer", ["description"] = "remove 时用：购物车里的行号（从 1 开始）" },
        },
    };

    public bool RequiresConfirmation => true;

    public bool IsAction => false;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var action = (ToolArgs.ReadString(args, "action") ?? "add").Trim().ToLowerInvariant();
        var cart = BusinessService.Current.ShoppingCart;

        if (action == "remove")
        {
            return Task.FromResult(Remove(args, cart));
        }

        return Task.FromResult(Add(args, cart));
    }

    private static ToolResult Add(JsonElement args, System.Collections.ObjectModel.ObservableCollection<ScalperShoppingItem> cart)
    {
        var name = ToolArgs.ReadString(args, "itemName")?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return new ToolResult("参数 itemName（物品名）不能为空。");
        }

        var quantity = ReadDouble(args, "quantity", 0);
        if (quantity <= 0)
        {
            return new ToolResult("参数 quantity（数量）必须大于 0。");
        }

        var invType = ToolNameResolver.ResolveItem(name);
        if (invType is null)
        {
            return new ToolResult($"未找到物品「{name}」（或名称不唯一）。请用完整的游戏内物品名。");
        }

        var buyPrice = ReadDouble(args, "buyPrice", 0);
        var sellPrice = ReadDouble(args, "sellPrice", 0);
        var jumps = Math.Max(0, ToolArgs.ReadInt(args, "jumps", 0));

        var existing = cart.FirstOrDefault(p => p.InvType?.TypeID == invType.TypeID);
        var item = existing ?? new ScalperShoppingItem
        {
            InvType = invType,
            BuyPrice = buyPrice,
            SellPrice = sellPrice,
            Jumps = jumps,
            Quantity = 0,
        };

        if (existing is null)
        {
            cart.Add(item);
        }
        else
        {
            // 已存在：数量累加，价格/跳数按本次给的值更新（0 视为"不改"）
            if (buyPrice > 0)
            {
                item.BuyPrice = buyPrice;
            }

            if (sellPrice > 0)
            {
                item.SellPrice = sellPrice;
            }

            if (jumps > 0)
            {
                item.Jumps = jumps;
            }
        }

        item.Quantity += quantity;

        var builder = new StringBuilder();
        builder.AppendLine($"{(existing is null ? "已加入" : "已累加")}购物车：{invType.TypeName} × {quantity:N0}");
        builder.AppendLine($"买入 {ToolArgs.FormatIsk(item.BuyPrice)}｜卖出 {ToolArgs.FormatIsk(item.SellPrice)}｜" +
            $"净利 {ToolArgs.FormatIsk(item.NetProfit)}｜回报率 {item.ROI:F2}%｜体积 {item.Volume:N2} m³");
        builder.AppendLine($"购物车现有 {cart.Count} 种物品，合计本金 {ToolArgs.FormatIsk(cart.Sum(p => p.BuyPrice * p.Quantity))}、" +
            $"合计净利 {ToolArgs.FormatIsk(cart.Sum(p => p.NetProfit))}。");

        return new ToolResult(builder.ToString().TrimEnd(),
            ParamSummary: $"购物车 +{invType.TypeName} × {quantity:N0}");
    }

    private static ToolResult Remove(JsonElement args, System.Collections.ObjectModel.ObservableCollection<ScalperShoppingItem> cart)
    {
        var name = ToolArgs.ReadString(args, "itemName")?.Trim();
        var index = ToolArgs.ReadInt(args, "index", 0);

        if (string.IsNullOrWhiteSpace(name) && index <= 0)
        {
            return new ToolResult("移除购物车条目需要 itemName（物品名）或 index（行号，从 1 开始）。");
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var invType = ToolNameResolver.ResolveItem(name);
            if (invType is null)
            {
                return new ToolResult($"未找到物品「{name}」（或名称不唯一）。");
            }

            var item = cart.FirstOrDefault(p => p.InvType?.TypeID == invType.TypeID);
            if (item is null)
            {
                return new ToolResult($"购物车里没有「{invType.TypeName}」。");
            }

            cart.Remove(item);
            return new ToolResult($"已从购物车移除：{invType.TypeName}（剩余 {cart.Count} 种）。",
                ParamSummary: $"购物车 -{invType.TypeName}");
        }

        if (index > cart.Count)
        {
            return new ToolResult($"行号 {index} 超出范围（购物车当前 {cart.Count} 行）。");
        }

        var removed = cart[index - 1];
        cart.RemoveAt(index - 1);
        return new ToolResult($"已从购物车移除第 {index} 行：{removed.InvType?.TypeName}（剩余 {cart.Count} 种）。",
            ParamSummary: $"购物车 移除第 {index} 行");
    }

    /// <summary>读数字参数（JSON 里可能是整数也可能是小数）。</summary>
    private static double ReadDouble(JsonElement args, string name, double fallback)
    {
        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Number)
        {
            return value.GetDouble();
        }

        return fallback;
    }
}
