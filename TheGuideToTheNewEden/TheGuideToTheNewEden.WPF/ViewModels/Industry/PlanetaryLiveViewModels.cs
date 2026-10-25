using System.Collections.Generic;
using System.Linq;

namespace TheGuideToTheNewEden.WPF.ViewModels.Industry;

public sealed class PlanetaryLiveExtractorItem
{
    public string ProductName { get; set; } = string.Empty;
    public string QuantityText { get; set; } = string.Empty;
    public string CycleText { get; set; } = string.Empty;
    public string HeadsText { get; set; } = string.Empty;
    public string RemainingText { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public bool IsStopped { get; set; }
}

public sealed class PlanetaryLiveFactoryItem
{
    public int TypeId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Count { get; set; }
    public double OutputPerDay { get; set; }
    public string OutputText { get; set; } = string.Empty;
    public string CycleText { get; set; } = string.Empty;
    public string InputsText { get; set; } = string.Empty;
}

public sealed class PlanetaryLivePinItem
{
    public long PinId { get; set; }
    public int TypeId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsExtractor { get; set; }
    public bool IsFactory { get; set; }
    public bool IsStorage { get; set; }
}

public sealed class PlanetaryFacilityNode
{
    public long PinId { get; init; }
    public string Role { get; init; } = string.Empty;
    public string TypeName { get; init; } = string.Empty;
    public bool IsExtractor { get; init; }
    public bool IsFactory { get; init; }
    public bool IsStorage { get; init; }
    public int ConnectionCount { get; init; }
    public string Subtitle => string.Equals(TypeName, Role, StringComparison.Ordinal)
        ? $"Pin #{PinId}"
        : TypeName;
    public string MetaText => ConnectionCount > 0
        ? $"Pin #{PinId} · {ConnectionCount} 条连接"
        : $"Pin #{PinId}";
}

public sealed class PlanetaryLiveCapacityPoint
{
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
}

public sealed class PlanetaryLiveCharacterGroup
{
    public PlanetaryLiveCharacterGroup(long characterId, string characterName, List<PlanetaryLivePlanetItem> colonies)
    {
        CharacterId = characterId;
        CharacterName = characterName;
        Colonies = colonies;
    }

    public long CharacterId { get; }
    public string CharacterName { get; }
    public List<PlanetaryLivePlanetItem> Colonies { get; }
    public int ColonyCount => Colonies.Count;
    public int ExtractorCount => Colonies.Sum(p => p.Extractors.Count);
    public int StoppedExtractorCount => Colonies.Sum(p => p.Extractors.Count(p => p.IsStopped));
    public int FactoryCount => Colonies.Sum(p => p.Factories.Sum(p => p.Count));
    public string ColonyCountText => $"{ColonyCount} 个殖民地";
    public string Summary => $"{ColonyCount} 个殖民地 · {ExtractorCount} 个提取器（{StoppedExtractorCount} 停止） · {FactoryCount} 个工厂";
}

public sealed class PlanetaryLivePlanetItem
{
    public long CharacterId { get; set; }
    public long PlanetId { get; set; }
    public string CharacterName { get; set; } = string.Empty;
    public string PlanetName { get; set; } = string.Empty;
    public string PlanetTypeName { get; set; } = string.Empty;
    public string SystemName { get; set; } = string.Empty;
    public int UpgradeLevel { get; set; }
    public long NumPins { get; set; }
    public string LastUpdateText { get; set; } = string.Empty;
    public string StorageText { get; set; } = string.Empty;
    public string ImportsText { get; set; } = string.Empty;
    public string ExportsText { get; set; } = string.Empty;
    public string AlertText { get; set; } = string.Empty;
    public bool HasAlert { get; set; }
    public List<PlanetaryLiveExtractorItem> Extractors { get; set; } = new();
    public List<PlanetaryLiveFactoryItem> Factories { get; set; } = new();
    public List<PlanetaryLivePinItem> Pins { get; set; } = new();
    public List<PlanetaryFacilityNode> Facilities { get; set; } = new();
    public List<PlanetaryLiveCapacityPoint> ExtractorCapacitySeries { get; set; } = new();
    public List<PlanetaryLiveCapacityPoint> FactoryCapacitySeries { get; set; } = new();
    public string ExtractorCapacityHint { get; set; } = string.Empty;
    public string FactoryCapacityHint { get; set; } = string.Empty;
    public bool HasExtractors => Extractors.Count > 0;
    public bool HasFactories => Factories.Count > 0;
    public bool HasPins => Pins.Count > 0;
    public bool HasNoPins => Pins.Count == 0;
    public bool HasFacilities => Facilities.Count > 0;
    public bool HasNoFacilities => Facilities.Count == 0;
    public bool HasExtractorCapacity => ExtractorCapacitySeries.Count > 1;
    public bool HasFactoryCapacity => FactoryCapacitySeries.Count > 1;
    public int FactoryPinCount => Factories.Sum(p => p.Count);
    public string Header => $"{CharacterName} · {PlanetName}";
    public string SubHeader => PlanetTypeName;
    public string Summary
    {
        get
        {
            var parts = new List<string>();
            if (Extractors.Count > 0)
            {
                parts.Add("提取 " + string.Join("、", Extractors.GroupBy(p => p.ProductName)
                    .Select(g => g.Key + (g.Count() > 1 ? "×" + g.Count() : string.Empty))));
            }
            if (Factories.Count > 0)
            {
                parts.Add("工厂 " + string.Join("、", Factories.GroupBy(p => p.ProductName)
                    .Select(g => g.Key + "×" + g.Sum(x => x.Count))));
            }
            return parts.Count == 0 ? "无生产设施" : string.Join(" · ", parts);
        }
    }
}
