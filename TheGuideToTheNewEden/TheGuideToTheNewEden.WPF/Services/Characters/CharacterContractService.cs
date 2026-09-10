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

    public string Status { get; set; } = string.Empty;

    public string StartLocationName { get; set; } = string.Empty;

    public string EndLocationName { get; set; } = string.Empty;

    public double Volume { get; set; }

    public double Reward { get; set; }

    public double Collateral { get; set; }

    public int? DaysToComplete { get; set; }
}

/// <summary>角色与军团合同（分页）。</summary>
public static class CharacterContractService
{
    public static async Task<PagedResult<ContractView>?> GetAsync(
        CharacterContext context, bool corporation, int page)
    {
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

            var locationIds = contracts
                .Select(p => p.StartLocationId ?? 0)
                .Concat(contracts.Select(p => p.EndLocationId ?? 0))
                .Where(p => p > 0)
                .Distinct()
                .ToList();

            var names = new Dictionary<long, string>();
            try
            {
                foreach (var item in await IDNameService.GetByIdsAsync(locationIds) ?? [])
                {
                    if (!string.IsNullOrEmpty(item.Name))
                    {
                        names[item.Id] = item.Name;
                    }
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }

            var views = contracts.Select(contract => new ContractView
            {
                ContractId = contract.ContractId,
                Title = string.IsNullOrWhiteSpace(contract.Title) ? "-" : contract.Title,
                TypeText = ResolveType(contract.Type),
                Price = contract.Price ?? 0,
                IssuerName = contract.IssuerId.ToString(),
                DateIssued = contract.DateIssued,
                DateExpired = contract.DateExpired,
                Status = contract.Status ?? "-",
                StartLocationName = names.GetValueOrDefault(contract.StartLocationId ?? 0, contract.StartLocationId?.ToString() ?? "-"),
                EndLocationName = names.GetValueOrDefault(contract.EndLocationId ?? 0, contract.EndLocationId?.ToString() ?? "-"),
                Volume = contract.Volume ?? 0,
                Reward = contract.Reward ?? 0,
                Collateral = contract.Collateral ?? 0,
                DaysToComplete = (int?)contract.DaysToComplete,
            }).ToList();

            return new PagedResult<ContractView>
            {
                Page = page,
                Items = views,
                HasNextPage = views.Count > 0,
            };
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
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