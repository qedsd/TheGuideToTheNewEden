using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.Services.Business;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 倒货查利差（只读）：在「商业 → 比价」已配置的市场清单上，查指定物品**现在的**跨市场买卖价差，
/// 给出"在哪买最便宜 / 在哪卖最贵 / 净利与回报率"。
/// <para>
/// 与 <c>compare_analyse</c> 的分工：那个是"完整比价分析"（按比价页自己的口径与清单批量算，需确认、较重）；
/// 本工具是**轻量的一两个物品的现价快照**，市场清单取自比价设置（用户已经挑好的那几个市场）。
/// 取数仍走 <see cref="MarketOrderService"/> 的同物品/星域分页接口（与市场页同一口径），
/// <b>不触发整星域全量抓取</b>（那会拉几万条订单、分钟级）。
/// </para>
/// <para>
/// 口径说明（写进回执，避免模型自行解释）：
/// 买入成本 = 该市场最便宜的 N 件卖单加权均价；卖出收入 = 该市场最高的 N 件买单加权均价 × (1 − 卖出税率)。
/// 税费取比价设置里的 <c>SellTaxPercent</c>（销售税 + 经纪费合计）。
/// </para>
/// </summary>
public sealed class TradeSpreadTool : ITool
{
    /// <summary>单次最多比几个市场（防止模型把 12 个市场的全量物品都拉一遍）。</summary>
    private const int MaxMarkets = 8;

    /// <summary>同时取数的市场数上限（与市场服务自身的翻页并发上限一致）。</summary>
    private const int MaxDegree = 4;

    public string Name => "trade_spread";

    public string DisplayNameKey => "AssistantTool_trade_spread";

    public string Description =>
        "查物品在已配置比价市场清单上的当前跨市场利差：各市场最低卖价与最高买价、" +
        "最优的「A 买 B 卖」组合（含税费净利与回报率）。只读、只查现价，不整星域全量抓取。" +
        "适合「XX 现在能不能倒」「哪里买最便宜哪里卖最贵」；利润以本工具返回为准，不得自行推算。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["items"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" },
                ["description"] = "物品名数组（1–5 件）",
            },
            ["quantity"] = new JsonObject { ["type"] = "integer", ["description"] = "交易件数（默认 1）" },
            ["markets"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" },
                ["description"] = "限定市场（留空 = 设置里全部）",
            },
        },
        ["required"] = new JsonArray { "items" },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var names = ReadStringArray(args, "items");
        if (names.Count == 0)
        {
            return new ToolResult("参数 items 不能为空（至少一个物品名）。");
        }

        if (names.Count > 5)
        {
            return new ToolResult($"一次最多查 5 件物品（给了 {names.Count} 件）。批量比价请用「商业 → 比价」页或 compare_analyse。");
        }

        var quantity = Math.Clamp(ToolArgs.ReadInt(args, "quantity", 1), 1, 100000);

        var setting = CompareMarketSettingService.Load();
        var markets = setting.Markets ?? [];
        if (markets.Count == 0)
        {
            return new ToolResult("比价设置里没有市场，请先在「商业 → 比价」页配置要比的市场（星域/星系/建筑）。");
        }

        var requested = ReadStringArray(args, "markets");
        if (requested.Count > 0)
        {
            var filtered = markets
                .Where(m => requested.Any(r => string.Equals(r?.Trim(), m.Name, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (filtered.Count == 0)
            {
                return new ToolResult($"参数 markets 里的市场（{string.Join("、", requested)}）都不在比价设置里。" +
                    $"当前可用：{string.Join("、", markets.Select(m => m.Name))}。");
            }

            markets = filtered;
        }

        if (markets.Count > MaxMarkets)
        {
            markets = markets.Take(MaxMarkets).ToList();
        }

        var resolved = new List<(string Query, Core.DBModels.InvType Type)>();
        var missing = new List<string>();
        foreach (var name in names)
        {
            var invType = ToolNameResolver.ResolveItem(name);
            if (invType is null)
            {
                missing.Add(name);
                continue;
            }

            if (resolved.All(p => p.Type.TypeID != invType.TypeID))
            {
                resolved.Add((name, invType));
            }
        }

        if (resolved.Count == 0)
        {
            return new ToolResult($"没有解析出任何物品（未找到：{string.Join("、", missing)}）。请用游戏内物品名（中英文均可）。");
        }

        var taxPercent = Math.Clamp(setting.SellTaxPercent, 0, 100);

        // 逐市场取数并**立刻**压成"两侧前 N 件的均价与可成交量"——订单列表很大，不能留在内存里等所有市场取完
        var results = new ConcurrentDictionary<(int MarketIndex, int TypeId), MarketQuote>();
        var failures = new ConcurrentDictionary<int, string>();

        var jobs = new List<(int MarketIndex, int TypeId)>();
        for (var i = 0; i < markets.Count; i++)
        {
            foreach (var (_, type) in resolved)
            {
                jobs.Add((i, type.TypeID));
            }
        }

        var groups = jobs.GroupBy(p => p.MarketIndex).ToList();
        await Parallel.ForEachAsync(groups, new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Min(MaxDegree, groups.Count),
            CancellationToken = ct,
        }, async (group, token) =>
        {
            var market = markets[group.Key];
            foreach (var job in group)
            {
                token.ThrowIfCancellationRequested();
                var quote = await FetchQuoteAsync(market, job.TypeId, quantity, token);
                if (quote is null)
                {
                    failures.TryAdd(group.Key, market.Name ?? "?");
                }
                else
                {
                    results[job] = quote;
                }
            }
        });

        if (ct.IsCancellationRequested)
        {
            return new ToolResult("查询已取消。");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"跨市场利差 · {resolved.Count} 件物品 × {markets.Count} 市场 · 交易量 {quantity:N0} 件 · 卖出税费 {taxPercent:0.#}%");
        builder.AppendLine($"口径：买入 = 最便宜的 {quantity:N0} 件卖单加权均价；卖出 = 最高的 {quantity:N0} 件买单加权均价 × (1 − 税费)。");

        foreach (var (query, type) in resolved)
        {
            builder.AppendLine();
            builder.AppendLine($"【{type.TypeName}】（TypeId {type.TypeID}）");

            var quotes = new List<(MarketLocation Market, MarketQuote Quote)>();
            foreach (var (market, index) in markets.Select((m, i) => (m, i)))
            {
                if (results.TryGetValue((index, type.TypeID), out var quote))
                {
                    quotes.Add((market, quote));
                }
            }

            if (quotes.Count == 0)
            {
                builder.AppendLine("- 所有市场都没取到订单（网络不可用或该物品在这些市场没有挂单）。");
                continue;
            }

            foreach (var (market, quote) in quotes)
            {
                var inner = quote.BestBuy > 0 && quote.BestSell > 0
                    ? $"｜市场内价差 {quote.BestBuy - quote.BestSell:N2}"
                    : string.Empty;
                builder.AppendLine(
                    $"- {market.Name}：最低卖 {Format(quote.BestSell)}（可买 {quote.SellVolume:N0}）" +
                    $"｜最高买 {Format(quote.BestBuy)}（可卖 {quote.BuyVolume:N0}）{inner}");
            }

            // 跨市场最优：在卖价最低的市场买入、在买价最高的市场卖出（两个市场都要有量）
            var taxFactor = 1 - taxPercent / 100.0;
            var buySide = quotes.Where(p => p.Quote.BuyCost > 0).OrderBy(p => p.Quote.BuyUnitPrice).ToList();
            var sellSide = quotes.Where(p => p.Quote.SellRevenue > 0)
                .OrderByDescending(p => p.Quote.SellUnitPrice * taxFactor)
                .ToList();
            if (buySide.Count == 0 || sellSide.Count == 0)
            {
                builder.AppendLine("- 没有可用的跨市场组合（某个方向缺报价：既要有卖单可买，也要有买单可卖）。");
                continue;
            }

            var (buyMarket, buyQuote) = buySide[0];
            var (sellMarket, sellQuote) = sellSide[0];
            var cost = buyQuote.BuyCost;
            var grossRevenue = sellQuote.SellRevenue;
            var revenue = grossRevenue * taxFactor;
            var net = revenue - cost;
            var roi = cost > 0 ? net / cost * 100 : 0;

            builder.AppendLine(
                $"→ 最优：在「{buyMarket.Name}」买（{Format(buyQuote.BuyUnitPrice)} × {quantity:N0} = {Format(cost)}）" +
                $"→ 在「{sellMarket.Name}」卖（{Format(sellQuote.SellUnitPrice)} × {quantity:N0} = {Format(grossRevenue)} 税前，" +
                $"税后 {Format(revenue)}）");
            builder.AppendLine($"  净利 {Format(net)} · 回报率 {roi:F2}%");

            if (buyQuote.SellVolume < quantity || sellQuote.BuyVolume < quantity)
            {
                builder.AppendLine($"  ⚠ 量不足：买入侧可成交 {buyQuote.SellVolume:N0} 件、卖出侧可成交 {sellQuote.BuyVolume:N0} 件，" +
                    "上面数值是把不足部分当作能成交算的，实际请以较小的一侧为上限。");
            }
        }

        if (missing.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine($"未找到物品：{string.Join("、", missing)}");
        }

        if (failures.Count > 0)
        {
            builder.AppendLine($"取数失败的市场：{string.Join("、", failures.Values.Distinct())}（建筑市场需要角色授权与访问权）");
        }

        if (markets.Count < (setting.Markets?.Count ?? 0))
        {
            builder.AppendLine($"（比价设置里还有 {(setting.Markets?.Count ?? 0) - markets.Count} 个市场未参与本次对比）");
        }

        return new ToolResult(builder.ToString().TrimEnd(),
            ParamSummary: $"{string.Join("/", resolved.Select(p => p.Type.TypeName))} × {markets.Count} 市场 · {quantity:N0} 件");
    }

    /// <summary>取一个市场里某物品的买卖两侧报价（失败返回 null，由调用方记入"取数失败"）。</summary>
    private static async Task<MarketQuote?> FetchQuoteAsync(MarketLocation market, int typeId, int quantity, CancellationToken ct)
    {
        List<Order>? orders;
        switch (market.Type)
        {
            case MarketLocationType.Region:
                orders = await MarketOrderService.Current.GetRegionOrdersAsync(typeId, market.Id, ct);
                break;
            case MarketLocationType.SolarSystem:
                {
                    var regionId = market.RegionId > 0
                        ? market.RegionId
                        : Core.Services.DB.MapSolarSystemService.Query((int)market.Id)?.RegionID ?? 0;
                    if (regionId <= 0)
                    {
                        return null;
                    }

                    // 星系口径与市场页/估价页完全一致：拉该物品在所属星域的订单再按 SystemId 过滤（不必整星域全量）
                    var all = await MarketOrderService.Current.GetRegionOrdersAsync(typeId, regionId, ct);
                    orders = all?.Where(p => p.SystemId == market.Id).ToList();
                    break;
                }
            default:
                // 建筑市场：整建筑全量（服务内有 TTL 缓存），再按物品过滤
                orders = await MarketOrderService.Current.GetStructureTypeOrdersAsync(market.Id, typeId, ct);
                break;
        }

        if (orders is null)
        {
            return null;
        }

        return MarketQuote.From(orders, quantity);
    }

    /// <summary>一个市场里某物品的两侧报价（只保留"算利差需要的量"，订单列表本身用完即弃）。</summary>
    private sealed class MarketQuote
    {
        public double BestSell { get; private init; }

        public double BestBuy { get; private init; }

        public double BuyUnitPrice { get; private init; }

        public double SellUnitPrice { get; private init; }

        public double BuyCost { get; private init; }

        public double SellRevenue { get; private init; }

        public long SellVolume { get; private init; }

        public long BuyVolume { get; private init; }

        /// <summary>
        /// 由订单列表现算两侧报价。<c>SellRevenue</c> 是**税前**收入（税费由工具主流程按比价设置统一扣）。
        /// </summary>
        public static MarketQuote From(List<Order> orders, int quantity)
        {
            var sells = orders.Where(p => !p.IsBuyOrder).OrderBy(p => p.Price).ToList();
            var buys = orders.Where(p => p.IsBuyOrder).OrderByDescending(p => p.Price).ToList();

            var (sellUnit, sellVolume) = WeightedTop(sells, quantity);
            var (buyUnit, buyVolume) = WeightedTop(buys, quantity);

            return new MarketQuote
            {
                BestSell = sells.Count > 0 ? sells[0].Price : 0,
                BestBuy = buys.Count > 0 ? buys[0].Price : 0,
                BuyUnitPrice = sellUnit,
                SellUnitPrice = buyUnit,
                BuyCost = sellUnit * quantity,
                // 收入 = 卖出单价 × 数量（税费在工具主流程里按设置统一扣，这里先给税前）
                SellRevenue = buyUnit * quantity,
                SellVolume = sellVolume,
                BuyVolume = buyVolume,
            };
        }

        /// <summary>逐档吃 <paramref name="quantity"/> 件的加权均价与实际可成交量。</summary>
        private static (double UnitPrice, long Volume) WeightedTop(List<Order> sorted, int quantity)
        {
            if (sorted.Count == 0)
            {
                return (0, 0);
            }

            var remain = (long)quantity;
            var volume = 0L;
            var amount = 0.0;
            foreach (var order in sorted)
            {
                if (remain <= 0)
                {
                    break;
                }

                var take = Math.Min(remain, order.VolumeRemain);
                volume += take;
                amount += take * order.Price;
                remain -= take;
            }

            return volume == 0 ? (sorted[0].Price, 0) : (amount / volume, volume);
        }
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

    private static string Format(double value) => value <= 0 ? "无" : ToolArgs.FormatIsk(value);
}
