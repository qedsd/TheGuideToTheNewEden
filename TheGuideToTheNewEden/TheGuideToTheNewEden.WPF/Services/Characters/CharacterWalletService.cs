using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WPF.Services.Characters;

/// <summary>钱包流水行（已与 ESI 模型解耦）。</summary>
public sealed class WalletJournalRow
{
    public long Id { get; set; }

    public DateTime Date { get; set; }

    public double Amount { get; set; }

    public double Balance { get; set; }

    public string? RefType { get; set; }

    public string? Description { get; set; }

    public string? Reason { get; set; }
}

/// <summary>钱包交易行（已与 ESI 模型解耦）。</summary>
public sealed class WalletTransactionRow
{
    public long TransactionId { get; set; }

    public DateTime Date { get; set; }

    public long TypeId { get; set; }

    public string? TypeName { get; set; }

    public double UnitPrice { get; set; }

    public int Quantity { get; set; }

    public double TotalPrice { get; set; }

    public bool IsBuy { get; set; }

    public long ClientId { get; set; }

    public string? ClientName { get; set; }

    public long LocationId { get; set; }

    /// <summary>地点名称（空间站/结构），解析失败时回退为原始 ID 文本。</summary>
    public string? LocationName { get; set; }
}

/// <summary>
/// 钱包流水与交易。ESI 分页返回，统一包装为 <see cref="PagedResult{T}"/>；
/// 首页进缓存（短 TTL），翻页与强制刷新直接请求。
/// </summary>
public static class CharacterWalletService
{
    private const string JournalKey = "wallet.journal";
    private const string TransactionKey = "wallet.transaction";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    public static async Task<double> GetBalanceAsync(CharacterContext context, bool forceRefresh = false)
    {
        if (!forceRefresh)
        {
            var overview = await CharacterOverviewService.GetAsync(context);
            if (overview is not null)
            {
                return overview.WalletBalance;
            }
        }

        if (!await context.EnsureTokenValidAsync())
        {
            return 0;
        }

        try
        {
            return (await context.Api.Wallet.GetCharacterWalletBalanceAsync(context.Auth)).Model;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return 0;
        }
    }

    public static async Task<PagedResult<WalletJournalRow>?> GetCharacterJournalAsync(
        CharacterContext context, int page, bool forceRefresh = false)
    {
        if (page == 1 && !forceRefresh
            && CharacterCache.TryGet<PagedResult<WalletJournalRow>>(context.CharacterId, JournalKey, out var cached)
            && cached is not null)
        {
            return cached;
        }

        if (!await context.EnsureTokenValidAsync())
        {
            return null;
        }

        try
        {
            var model = (await context.Api.Wallet.GetCharacterWalletJournalAsync(context.Auth, page)).Model ?? [];
            var rows = model.Select(p => new WalletJournalRow
            {
                Id = p.Id,
                Date = p.Date,
                Amount = p.Amount ?? 0,
                Balance = p.Balance ?? 0,
                RefType = p.RefType,
                Description = p.Description,
                Reason = p.Reason,
            }).ToList();

            var result = new PagedResult<WalletJournalRow>
            {
                Page = page,
                Items = rows,
                HasNextPage = rows.Count > 0,
            };

            if (page == 1)
            {
                CharacterCache.Set(context.CharacterId, JournalKey, result, Ttl);
            }

            return result;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    public static async Task<PagedResult<WalletTransactionRow>?> GetCharacterTransactionsAsync(
        CharacterContext context, int page, bool forceRefresh = false)
    {
        if (page == 1 && !forceRefresh
            && CharacterCache.TryGet<PagedResult<WalletTransactionRow>>(context.CharacterId, TransactionKey, out var cached)
            && cached is not null)
        {
            return cached;
        }

        if (!await context.EnsureTokenValidAsync())
        {
            return null;
        }

        try
        {
            var model = (await context.Api.Wallet.GetCharacterWalletTransactionsAsync(context.Auth, 0L)).Model ?? [];
            var rows = model.Select(p => new WalletTransactionRow
            {
                TransactionId = p.TransactionId,
                Date = p.Date,
                TypeId = p.TypeId,
                TypeName = InvTypeService.QueryType(p.TypeId)?.TypeName,
                UnitPrice = p.UnitPrice,
                Quantity = (int)p.Quantity,
                TotalPrice = p.UnitPrice * p.Quantity,
                IsBuy = p.IsBuy,
                ClientId = p.ClientId,
                LocationId = p.LocationId,
            }).ToList();

            await EnrichNamesAsync(rows);

            var result = new PagedResult<WalletTransactionRow>
            {
                Page = page,
                Items = rows,
                HasNextPage = false,
            };

            if (page == 1)
            {
                CharacterCache.Set(context.CharacterId, TransactionKey, result, Ttl);
            }

            return result;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    /// <summary>军团钱包流水。division 为 1-7。</summary>
    public static async Task<IReadOnlyList<WalletJournalRow>?> GetCorporationJournalAsync(
        CharacterContext context, long corporationId, int division, int page)
    {
        if (!await context.EnsureTokenValidAsync())
        {
            return null;
        }

        try
        {
            var model = (await context.Api.Wallet.GetCorporationWalletJournalAsync(context.Auth, corporationId, division, page)).Model ?? [];
            return model.Select(p => new WalletJournalRow
            {
                Id = p.Id,
                Date = p.Date,
                Amount = p.Amount ?? 0,
                Balance = p.Balance ?? 0,
                RefType = p.RefType,
                Description = p.Description,
                Reason = p.Reason,
            }).ToList();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    /// <summary>军团钱包交易。</summary>
    public static async Task<IReadOnlyList<WalletTransactionRow>?> GetCorporationTransactionsAsync(
        CharacterContext context, long corporationId, int division)
    {
        if (!await context.EnsureTokenValidAsync())
        {
            return null;
        }

        try
        {
            var model = (await context.Api.Wallet.GetCorporationWalletTransactionsAsync(context.Auth, corporationId, division, 0L)).Model ?? [];
            var rows = model.Select(p => new WalletTransactionRow
            {
                TransactionId = p.TransactionId,
                Date = p.Date,
                TypeId = p.TypeId,
                TypeName = InvTypeService.QueryType(p.TypeId)?.TypeName,
                UnitPrice = p.UnitPrice,
                Quantity = (int)p.Quantity,
                TotalPrice = p.UnitPrice * p.Quantity,
                IsBuy = p.IsBuy,
                ClientId = p.ClientId,
                LocationId = p.LocationId,
            }).ToList();

            await EnrichNamesAsync(rows);
            return rows;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    /// <summary>
    /// 回填交易的客户名与地点名。
    /// 客户 ID 通常是角色/军团/联盟 ID（int 范围）；超出范围时直接显示原始 ID，
    /// 避免被 <c>(int)</c> 静默截断后解析成别人。地点 ID 按结构域分流解析。
    /// </summary>
    private static async Task EnrichNamesAsync(List<WalletTransactionRow> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        foreach (var row in rows)
        {
            row.ClientName = row.ClientId switch
            {
                <= 0 => null,
                <= int.MaxValue => IDNameService.GetById((int)row.ClientId)?.Name ?? row.ClientId.ToString(),
                _ => row.ClientId.ToString(),
            };
        }

        var locationIds = rows.Select(p => p.LocationId).Where(p => p > 0).Distinct().ToList();
        if (locationIds.Count == 0)
        {
            return;
        }

        var names = await LocationNameResolver.ResolveAsync(locationIds);
        foreach (var row in rows)
        {
            row.LocationName = row.LocationId > 0
                ? names.GetValueOrDefault(row.LocationId, row.LocationId.ToString())
                : "-";
        }
    }
}