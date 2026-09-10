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

    public string? ClientName { get; set; }

    public long LocationId { get; set; }
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
                ClientName = p.ClientId > 0 ? IDNameService.GetById((int)p.ClientId)?.Name : null,
                LocationId = p.LocationId,
            }).ToList();

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
            return model.Select(p => new WalletTransactionRow
            {
                TransactionId = p.TransactionId,
                Date = p.Date,
                TypeId = p.TypeId,
                TypeName = InvTypeService.QueryType(p.TypeId)?.TypeName,
                UnitPrice = p.UnitPrice,
                Quantity = (int)p.Quantity,
                TotalPrice = p.UnitPrice * p.Quantity,
                IsBuy = p.IsBuy,
                LocationId = p.LocationId,
            }).ToList();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }
}