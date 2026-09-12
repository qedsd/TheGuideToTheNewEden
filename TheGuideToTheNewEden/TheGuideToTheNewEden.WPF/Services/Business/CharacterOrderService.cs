using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.Services.Characters;
using MarketOrder = TheGuideToTheNewEden.Core.Models.Market.Order;

namespace TheGuideToTheNewEden.WPF.Services.Business;

/// <summary>
/// 个人 / 军团未结订单，以及与市场参考价的对比（<see cref="StatusOrder"/>）。
/// 订单本身走角色授权（需要 token），参考价复用 <see cref="MarketOrderService"/> 的星域订单。
/// </summary>
public static class CharacterOrderService
{
    /// <summary>个人未结订单（ESI 单次返回，无分页）。</summary>
    public static async Task<List<MarketOrder>?> GetCharacterOrdersAsync(CharacterContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await context.EnsureTokenValidAsync())
            {
                return null;
            }

            var resp = await context.Api.Market.ListOpenOrdersFromCharacterAsync(context.Auth);
            if (resp?.Model is null)
            {
                return null;
            }

            var orders = resp.Model.Select(p => new MarketOrder(p)).ToList();
            await MarketOrderService.EnrichOrdersAsync(orders, cancellationToken);
            return orders;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return null;
        }
    }

    /// <summary>
    /// 军团未结订单（分页）。角色需要军团订单权限，无权限时 ESI 会拒绝 → 返回 null。
    /// </summary>
    public static async Task<List<MarketOrder>?> GetCorpOrdersAsync(CharacterContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await context.EnsureTokenValidAsync())
            {
                return null;
            }

            var orders = new List<MarketOrder>();
            var page = 1;
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }

                var resp = await context.Api.Market.ListOpenOrdersFromCorporationAsync(context.Auth, context.Character.CorporationID, page);
                if (resp?.Model is null || resp.Model.Count == 0)
                {
                    break;
                }

                orders.AddRange(resp.Model.Select(p => new MarketOrder(p)));

                // 注意：WinUI 版这里写成 `ListOpenOrders...(page++)` 再用自增后的 page 与 MaxPages 比较，
                // 会多请求一页（可能触发 "Requested page does not exist!"）；这里按"已到末页"正确判断。
                if (resp.MaxPages <= page)
                {
                    break;
                }

                page++;
            }

            await MarketOrderService.EnrichOrdersAsync(orders, cancellationToken);
            return orders;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return null;
        }
    }

    /// <summary>
    /// 逐单计算与市场参考价的差值。规则与 WinUI 版一致：
    /// 只对空间站订单取参考（结构订单在 WPF 侧解析不到星系/星域，恒为"未知"）；
    /// 参考取同物品同星域的最优对向价——买单看**最高买价**、卖单看**最低卖价**。
    /// 按 (TypeId, RegionId) 分组，每组只拉一次星域订单（WinUI 是逐单拉取）。
    /// </summary>
    public static async Task<List<StatusOrder>> BuildStatusAsync(IEnumerable<MarketOrder> orders, CancellationToken cancellationToken = default)
    {
        var list = orders.ToList();
        var results = new List<StatusOrder>(list.Count);
        var referenceCache = new Dictionary<(long TypeId, long RegionId), List<MarketOrder>>();

        foreach (var order in list)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            List<MarketOrder>? references = null;
            if (order.IsStation && order.RegionId > 0)
            {
                var key = (order.TypeId, order.RegionId);
                if (!referenceCache.TryGetValue(key, out var regionOrders))
                {
                    regionOrders = await MarketOrderService.Current.GetRegionOrdersAsync(order.TypeId, order.RegionId, cancellationToken) ?? [];
                    referenceCache[key] = regionOrders;
                }

                references = order.IsBuyOrder
                    ? regionOrders.Where(p => p.IsBuyOrder).OrderByDescending(p => p.Price).ToList()
                    : regionOrders.Where(p => !p.IsBuyOrder).OrderBy(p => p.Price).ToList();
            }

            results.Add(new StatusOrder(order, references!));
        }

        return results;
    }

    /// <summary>
    /// 在游戏客户端中打开该物品的市场详情（需要 <c>esi-ui.open_window.v1</c> 权限，失败返回 false）。
    /// </summary>
    public static async Task<bool> OpenMarketDetailsAsync(CharacterContext context, long typeId)
    {
        try
        {
            if (!await context.EnsureTokenValidAsync())
            {
                return false;
            }

            await context.Api.UserInterface.OpenMarketDetailsAsync(context.Auth, typeId);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return false;
        }
    }
}
