using System.Windows;
using TheGuideToTheNewEden.Core.Services;

namespace TheGuideToTheNewEden.WPF.Services.Characters;

public sealed class ContractView
{
    public long ContractId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string TypeText { get; set; } = string.Empty;

    public double Price { get; set; }

    public string IssuerName { get; set; } = string.Empty;

    public DateTime DateIssued { get; set; }

    public DateTime? DateExpired { get; set; }

    public string AcceptorName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string StartLocationName { get; set; } = string.Empty;

    public string EndLocationName { get; set; } = string.Empty;

    public double Volume { get; set; }

    public double Reward { get; set; }

    public double Collateral { get; set; }

    public double Buyout { get; set; }

    public bool ForCorporation { get; set; }

    public int? DaysToComplete { get; set; }
}

/// <summary>角色与军团合同（分页）。</summary>
public static class CharacterContractService
{
    private const string CharacterKey = "contract.character";
    private const string CorporationKey = "contract.corp";

    /// <summary>合同列表变化不频繁，首页缓存 2 分钟（翻页与强制刷新不走缓存）。</summary>
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    public static async Task<PagedResult<ContractView>?> GetAsync(
        CharacterContext context, bool corporation, int page, bool forceRefresh = false)
    {
        var cacheKey = corporation ? CorporationKey : CharacterKey;
        if (page == 1 && !forceRefresh
            && CharacterCache.TryGet<PagedResult<ContractView>>(context.CharacterId, cacheKey, out var cached)
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
            var auth = context.Auth;
            var model = corporation
                ? (await context.Api.Contracts.GetCorporationContractsAsync(auth, context.Character.CorporationID, page)).Model
                : (await context.Api.Contracts.GetContractsAsync(auth, page)).Model;

            var contracts = model ?? [];

            // 合同起止地点：空间站（int 范围）与玩家结构（约 1e12）必须分流解析，
            // 否则结构 ID 会被 (int) 截断成错误名称。
            var locationIds = contracts
                .Select(p => p.StartLocationId ?? 0)
                .Concat(contracts.Select(p => p.EndLocationId ?? 0))
                .Where(p => p > 0)
                .Distinct()
                .ToList();
            var locationNames = await LocationNameResolver.ResolveAsync(locationIds);

            // 发布人/接收人：角色或军团 ID，均在 int 范围内，可直接走 IDNameService（对齐 WinUI 的 ContractInfoHelper）。
            var personIds = contracts
                .Select(p => p.IssuerId)
                .Concat(contracts.Select(p => p.AcceptorId))
                .Where(p => p > 0)
                .Distinct()
                .ToList();
            var personNames = await ResolvePersonNamesAsync(personIds);

            var views = contracts.Select(contract => new ContractView
            {
                ContractId = contract.ContractId,
                Title = string.IsNullOrWhiteSpace(contract.Title) ? "-" : contract.Title,
                TypeText = ResolveType(contract.Type),
                Price = contract.Price ?? 0,
                IssuerName = contract.IssuerId > 0
                    ? personNames.GetValueOrDefault(contract.IssuerId, contract.IssuerId.ToString())
                    : "-",
                DateIssued = contract.DateIssued,
                DateExpired = contract.DateExpired,
                AcceptorName = contract.AcceptorId > 0
                    ? personNames.GetValueOrDefault(contract.AcceptorId, contract.AcceptorId.ToString())
                    : "-",
                Status = contract.Status ?? "-",
                StartLocationName = locationNames.GetValueOrDefault(contract.StartLocationId ?? 0, contract.StartLocationId?.ToString() ?? "-"),
                EndLocationName = locationNames.GetValueOrDefault(contract.EndLocationId ?? 0, contract.EndLocationId?.ToString() ?? "-"),
                Volume = contract.Volume ?? 0,
                Reward = contract.Reward ?? 0,
                Collateral = contract.Collateral ?? 0,
                Buyout = contract.Buyout ?? 0,
                ForCorporation = contract.ForCorporation,
                DaysToComplete = (int?)contract.DaysToComplete,
            }).ToList();

            var result = new PagedResult<ContractView>
            {
                Page = page,
                Items = views,
                HasNextPage = views.Count > 0,
            };

            if (page == 1)
            {
                CharacterCache.Set(context.CharacterId, cacheKey, result, Ttl);
            }

            return result;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    /// <summary>
    /// 解析发布人/接收人名称。ID 只可能是角色/军团/联盟（int 范围），
    /// 超出范围的直接回退为原始 ID 文本，避免被 (int) 截断后解析成别人。
    /// </summary>
    private static async Task<Dictionary<long, string>> ResolvePersonNamesAsync(IEnumerable<long> ids)
    {
        var result = new Dictionary<long, string>();
        var safeIds = ids.Where(p => p is > 0 and <= int.MaxValue).Distinct().ToList();
        if (safeIds.Count == 0)
        {
            return result;
        }

        try
        {
            foreach (var item in await IDNameService.GetByIdsAsync(safeIds) ?? [])
            {
                if (!string.IsNullOrEmpty(item.Name))
                {
                    result[item.Id] = item.Name;
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        return result;
    }

    /// <summary>合同类型的本地化文本（ContractPage_Type_*）。</summary>
    private static string ResolveType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return "-";
        }

        var key = $"ContractPage_Type_{type}";
        return Application.Current?.TryFindResource(key) as string ?? type;
    }
}
