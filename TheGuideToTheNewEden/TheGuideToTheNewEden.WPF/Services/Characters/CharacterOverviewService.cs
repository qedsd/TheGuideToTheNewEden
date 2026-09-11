using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WPF.Services.Characters;

/// <summary>角色总览数据（已与 ESI 模型解耦，便于缓存与绑定）。</summary>
public sealed class CharacterOverview
{
    public string CharacterName { get; set; } = string.Empty;

    public DateTime? Birthday { get; set; }

    public double SecurityStatus { get; set; }

    public long CorporationId { get; set; }

    public string? CorporationName { get; set; }

    public string? CorporationTicker { get; set; }

    public long AllianceId { get; set; }

    public string? AllianceName { get; set; }

    public string? AllianceTicker { get; set; }

    public double WalletBalance { get; set; }

    /// <summary>军团钱包余额合计（需要军团财务权限，取不到时为 null）。</summary>
    public double? CorporationWalletBalance { get; set; }

    public long LoyaltyPoints { get; set; }

    public bool Online { get; set; }

    public DateTime? LastLogin { get; set; }

    /// <summary>最后登出时间（离线时长显示用，与 WinUI 卡片一致）。</summary>
    public DateTime? LastLogout { get; set; }

    public int LoginCount { get; set; }

    public long ShipTypeId { get; set; }

    public string? ShipTypeName { get; set; }

    public long LocationId { get; set; }

    public DateTime UpdatedUtc { get; set; }
}

/// <summary>角色总览：公开信息、在线状态、位置/舰船、军团/联盟、LP、钱包余额。</summary>
public static class CharacterOverviewService
{
    private const string CacheKey = "overview";

    /// <summary>总览变化不频繁，缓存 5 分钟。</summary>
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public static async Task<CharacterOverview?> GetAsync(CharacterContext context, bool forceRefresh = false)
    {
        if (!forceRefresh && CharacterCache.TryGet<CharacterOverview>(context.CharacterId, CacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        if (!await context.EnsureTokenValidAsync())
        {
            return null;
        }

        var auth = context.Auth;
        var overview = new CharacterOverview { CharacterName = context.Name };

        var info = await Fetch(() => context.Api.Character.GetCharacterPublicInfoAsync(context.CharacterId));
        if (info is not null)
        {
            overview.Birthday = info.Birthday;
            overview.SecurityStatus = info.SecurityStatus ?? 0;
            overview.CorporationId = info.CorporationId;
            overview.AllianceId = info.AllianceId ?? 0;
        }

        var online = await Fetch(() => context.Api.Location.GetCharacterOnlineAsync(auth));
        if (online is not null)
        {
            overview.Online = online.Online;
            overview.LastLogin = online.LastLogin;
            overview.LastLogout = online.LastLogout;
        }

        var ship = await Fetch(() => context.Api.Location.GetCurrentShipAsync(auth));
        if (ship is not null)
        {
            overview.ShipTypeId = ship.ShipTypeId;
            overview.ShipTypeName = InvTypeService.QueryType(ship.ShipTypeId)?.TypeName;
        }

        var location = await Fetch(() => context.Api.Location.GetCharacterLocationAsync(auth));
        if (location is not null)
        {
            overview.LocationId = location.SolarSystemId;
        }

        // 忠诚点是"按军团"的一组记录，取总和。
        var loyalties = await Fetch(() => context.Api.Loyalty.GetLoyaltyPointsAsync(auth));
        if (loyalties is not null)
        {
            overview.LoyaltyPoints = loyalties.Sum(p => p.Points);
        }

        try
        {
            overview.WalletBalance = (await context.Api.Wallet.GetCharacterWalletBalanceAsync(auth)).Model;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        if (overview.CorporationId > 0)
        {
            var corp = await Fetch(() => context.Api.Corporation.GetCorporationInfoAsync(overview.CorporationId));
            if (corp is not null)
            {
                overview.CorporationName = corp.Name;
                overview.CorporationTicker = corp.Ticker;
            }

            // 军团钱包需要财务权限；无权限时保持 null，界面不显示该行（与 WinUI 版一致）
            var corpWallets = await Fetch(() => context.Api.Wallet.ReturnCorporationWalletBalanceAsync(auth, overview.CorporationId));
            if (corpWallets is not null)
            {
                overview.CorporationWalletBalance = corpWallets.Sum(p => p.Balance);
            }
        }

        if (overview.AllianceId > 0)
        {
            var alliance = await Fetch(() => context.Api.Alliance.GetAllianceInfoAsync(overview.AllianceId));
            if (alliance is not null)
            {
                overview.AllianceName = alliance.Name;
                overview.AllianceTicker = alliance.Ticker;
            }
        }

        overview.UpdatedUtc = DateTime.UtcNow;
        CharacterCache.Set(context.CharacterId, CacheKey, overview, Ttl);
        return overview;
    }

    /// <summary>单个接口失败不应让整页报错（例如缺少某个 scope）。</summary>
    private static async Task<TModel?> Fetch<TModel>(Func<Task<EVEStandard.Models.API.ESIModelDTO<TModel>>> call)
        where TModel : class
    {
        try
        {
            return (await call()).Model;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }
}