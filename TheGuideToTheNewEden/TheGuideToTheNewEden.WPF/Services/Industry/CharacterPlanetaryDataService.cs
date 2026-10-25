using EVEStandard.Models;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Services.Characters;
using EveColony = EVEStandard.Models.Colony;
using EveColonyLayout = EVEStandard.Models.ColonyLayout;

namespace TheGuideToTheNewEden.WPF.Services.Industry;

public sealed class PlanetaryColonySummary
{
    public long PlanetId { get; set; }
    public int PlanetTypeId { get; set; }
    public string PlanetTypeName { get; set; } = string.Empty;
    public string PlanetName { get; set; } = string.Empty;
    public long SolarSystemId { get; set; }
    public string SystemName { get; set; } = string.Empty;
    public int UpgradeLevel { get; set; }
    public long NumPins { get; set; }
    public DateTime LastUpdate { get; set; }
}

public sealed class PlanetaryColonyLayout
{
    public List<PlanetaryColonyPin> Pins { get; set; } = new();
    public List<PlanetaryColonyLink> Links { get; set; } = new();
}

public sealed class PlanetaryColonyLink
{
    public long SourcePinId { get; set; }
    public long DestinationPinId { get; set; }
    public int Level { get; set; }
}

public sealed class PlanetaryColonyPin
{
    public long PinId { get; set; }
    public int TypeId { get; set; }
    public int SchematicId { get; set; }
    public int ExtractorProductTypeId { get; set; }
    public long QtyPerCycle { get; set; }
    public long CycleTime { get; set; }
    public int HeadCount { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime? ExpiryTime { get; set; }
    public DateTime? InstallTime { get; set; }
    public int FactorySchematicId { get; set; }
    public List<PlanetaryPinContent> Contents { get; set; } = new();
}

public sealed class PlanetaryPinContent
{
    public int TypeId { get; set; }
    public long Amount { get; set; }
}

public sealed class PlanetaryColoniesResult
{
    public List<PlanetaryColonySummary> Colonies { get; set; } = new();
    public bool ScopeMissing { get; set; }
    public string? Error { get; set; }
    public bool Success => !ScopeMissing && string.IsNullOrEmpty(Error);
}

public sealed class PlanetaryColonyLayoutResult
{
    public PlanetaryColonyLayout? Layout { get; set; }
    public bool ScopeMissing { get; set; }
    public string? Error { get; set; }
    public bool Success => Layout != null && !ScopeMissing && string.IsNullOrEmpty(Error);
}

/// <summary>
/// 实时行星殖民（调用 EVEStandard PlanetaryInteraction）。
/// 数据写入 CharacterCache：殖民列表 5 分钟、单殖民布局 2 分钟。
/// </summary>
public static class CharacterPlanetaryDataService
{
    public const string Scope = "esi-planets.manage_planets.v1";
    private const string ColoniesKey = "planetary_colonies";
    private const string LayoutKeyPrefix = "planetary_layout_";
    private static readonly TimeSpan ColoniesTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LayoutTtl = TimeSpan.FromMinutes(2);

    public static bool HasScope(CharacterContext context)
    {
        return context?.Character?.Scopes?.Contains(Scope, StringComparison.OrdinalIgnoreCase) == true;
    }

    public static async Task<PlanetaryColoniesResult> GetColoniesAsync(CharacterContext context, bool forceRefresh = false)
    {
        if (context is null)
        {
            return new PlanetaryColoniesResult { Error = "角色上下文为空" };
        }
        if (!HasScope(context))
        {
            return new PlanetaryColoniesResult { ScopeMissing = true };
        }
        if (!forceRefresh
            && CharacterCache.TryGet<List<PlanetaryColonySummary>>(context.CharacterId, ColoniesKey, out var cached)
            && cached is not null)
        {
            return new PlanetaryColoniesResult { Colonies = cached };
        }
        if (!await context.EnsureTokenValidAsync())
        {
            return new PlanetaryColoniesResult { Error = "角色令牌已失效，请重新授权" };
        }

        try
        {
            var response = await context.Api.PlanetaryInteraction.GetColoniesAsync(context.Auth);
            var colonies = new List<PlanetaryColonySummary>();
            foreach (var colony in response?.Model ?? new List<EveColony>())
            {
                colonies.Add(MapColony(colony));
            }
            CharacterCache.Set(context.CharacterId, ColoniesKey, colonies, ColoniesTtl);
            return new PlanetaryColoniesResult { Colonies = colonies };
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return new PlanetaryColoniesResult { Error = ex.Message };
        }
    }

    public static async Task<PlanetaryColonyLayoutResult> GetLayoutAsync(
        CharacterContext context,
        long planetId,
        bool forceRefresh = false)
    {
        if (context is null)
        {
            return new PlanetaryColonyLayoutResult { Error = "角色上下文为空" };
        }
        if (!HasScope(context))
        {
            return new PlanetaryColonyLayoutResult { ScopeMissing = true };
        }
        var key = LayoutKeyPrefix + planetId;
        if (!forceRefresh
            && CharacterCache.TryGet<PlanetaryColonyLayout>(context.CharacterId, key, out var cached)
            && cached is not null)
        {
            return new PlanetaryColonyLayoutResult { Layout = cached };
        }
        if (!await context.EnsureTokenValidAsync())
        {
            return new PlanetaryColonyLayoutResult { Error = "角色令牌已失效，请重新授权" };
        }

        try
        {
            var response = await context.Api.PlanetaryInteraction.GetColonyLayoutAsync(context.Auth, planetId);
            var model = response?.Model;
            var layout = new PlanetaryColonyLayout();
            foreach (var pin in model?.Pins ?? new List<ColonyPin>())
            {
                var extractor = pin.ExtractorDetails;
                layout.Pins.Add(new PlanetaryColonyPin
                {
                    PinId = pin.PinId,
                    TypeId = (int)pin.TypeId,
                    SchematicId = (int)(pin.SchematicId ?? 0),
                    ExtractorProductTypeId = (int)(extractor?.ProductTypeId ?? 0),
                    QtyPerCycle = extractor?.QtyPerCycle ?? 0,
                    CycleTime = extractor?.CycleTime ?? 0,
                    HeadCount = extractor?.Heads?.Count ?? 0,
                    Latitude = pin.Latitude,
                    Longitude = pin.Longitude,
                    ExpiryTime = pin.ExpiryTime,
                    InstallTime = pin.InstallTime,
                    FactorySchematicId = (int)(pin.FactoryDetails?.SchematicId ?? pin.SchematicId ?? 0),
                    Contents = (pin.Contents ?? new List<ColonyContent>())
                        .Select(p => new PlanetaryPinContent
                        {
                            TypeId = (int)p.TypeId,
                            Amount = p.Amount,
                        })
                        .ToList(),
                });
            }
            foreach (var link in model?.Links ?? new List<ColonyLink>())
            {
                layout.Links.Add(new PlanetaryColonyLink
                {
                    SourcePinId = link.SourcePinId,
                    DestinationPinId = link.DestinationPinId,
                    Level = (int)link.LinkLevel,
                });
            }
            CharacterCache.Set(context.CharacterId, key, layout, LayoutTtl);
            return new PlanetaryColonyLayoutResult { Layout = layout };
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return new PlanetaryColonyLayoutResult { Error = ex.Message };
        }
    }

    private static PlanetaryColonySummary MapColony(EveColony colony)
    {
        var planet = MapDenormalizeService.Query((int)colony.PlanetId);
        var system = MapSolarSystemService.Query((int)colony.SolarSystemId);
        var typeName = string.IsNullOrWhiteSpace(colony.PlanetType)
            ? "Unknown"
            : char.ToUpperInvariant(colony.PlanetType[0]) + colony.PlanetType[1..].ToLowerInvariant();
        return new PlanetaryColonySummary
        {
            PlanetId = colony.PlanetId,
            PlanetTypeId = planet?.TypeID ?? 0,
            PlanetTypeName = typeName,
            PlanetName = planet?.ItemName ?? ("Planet " + colony.PlanetId),
            SolarSystemId = colony.SolarSystemId,
            SystemName = system?.SolarSystemName ?? string.Empty,
            UpgradeLevel = (int)colony.UpgradeLevel,
            NumPins = colony.NumPins,
            LastUpdate = colony.LastUpdate,
        };
    }
}
