using System;
using System.Collections.Generic;
using System.Linq;
using TheGuideToTheNewEden.Core.Models.Planetary;

namespace TheGuideToTheNewEden.Core.Services.Planetary
{
    /// <summary>
    /// 8 种可殖民行星的固定组成。SDE 的 planetSchematics 只有配方，没有 "哪种行星产哪些 P0"，
    /// 所以这部分作为项目自己的领域表；P0 名称在运行时解析成 SDE TypeID。
    /// </summary>
    public sealed class PlanetTypeCatalog
    {
        private static readonly PlanetTypeDefinition[] Definitions =
        {
            new PlanetTypeDefinition(11, "Temperate", true,
                new[] { "Aqueous Liquids", "Autotrophs", "Carbon Compounds", "Complex Organisms", "Microorganisms" },
                new PlanetFacilityTypeIds { CommandCenter = 2254, Launchpad = 2256, BasicFactory = 2481, AdvancedFactory = 2480, HighTechFactory = 2482, Storage = 2562, Extractor = 3068 }),
            new PlanetTypeDefinition(12, "Ice", false,
                new[] { "Aqueous Liquids", "Heavy Metals", "Microorganisms", "Noble Gas", "Planktic Colonies" },
                new PlanetFacilityTypeIds { CommandCenter = 2533, Launchpad = 2552, BasicFactory = 2493, AdvancedFactory = 2491, Storage = 2257, Extractor = 3061 }),
            new PlanetTypeDefinition(13, "Gas", false,
                new[] { "Aqueous Liquids", "Base Metals", "Ionic Solutions", "Noble Gas", "Reactive Gas" },
                new PlanetFacilityTypeIds { CommandCenter = 2534, Launchpad = 2543, BasicFactory = 2492, AdvancedFactory = 2494, Storage = 2536, Extractor = 3060 }),
            new PlanetTypeDefinition(2014, "Oceanic", false,
                new[] { "Aqueous Liquids", "Carbon Compounds", "Complex Organisms", "Microorganisms", "Planktic Colonies" },
                new PlanetFacilityTypeIds { CommandCenter = 2525, Launchpad = 2542, BasicFactory = 2490, AdvancedFactory = 2485, Storage = 2535, Extractor = 3063 }),
            new PlanetTypeDefinition(2015, "Lava", false,
                new[] { "Base Metals", "Felsic Magma", "Heavy Metals", "Non-CS Crystals", "Suspended Plasma" },
                new PlanetFacilityTypeIds { CommandCenter = 2549, Launchpad = 2555, BasicFactory = 2469, AdvancedFactory = 2470, Storage = 2558, Extractor = 3062 }),
            new PlanetTypeDefinition(2016, "Barren", true,
                new[] { "Aqueous Liquids", "Base Metals", "Carbon Compounds", "Microorganisms", "Noble Metals" },
                new PlanetFacilityTypeIds { CommandCenter = 2524, Launchpad = 2544, BasicFactory = 2473, AdvancedFactory = 2474, HighTechFactory = 2475, Storage = 2541, Extractor = 2848 }),
            new PlanetTypeDefinition(2017, "Storm", false,
                new[] { "Aqueous Liquids", "Base Metals", "Ionic Solutions", "Noble Gas", "Suspended Plasma" },
                new PlanetFacilityTypeIds { CommandCenter = 2550, Launchpad = 2557, BasicFactory = 2483, AdvancedFactory = 2484, Storage = 2561, Extractor = 3067 }),
            new PlanetTypeDefinition(2063, "Plasma", false,
                new[] { "Base Metals", "Heavy Metals", "Noble Metals", "Non-CS Crystals", "Suspended Plasma" },
                new PlanetFacilityTypeIds { CommandCenter = 2551, Launchpad = 2556, BasicFactory = 2471, AdvancedFactory = 2472, Storage = 2560, Extractor = 3064 }),
        };

        private readonly Dictionary<int, PlanetTypeProfile> _byTypeId;
        private readonly Dictionary<int, List<PlanetTypeProfile>> _byP0TypeId;

        public PlanetTypeCatalog(PlanetaryRecipeCatalog recipes)
        {
            var p0ByName = recipes.Products
                .Where(p => p.Tier == PlanetaryProductTier.P0)
                .GroupBy(p => p.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(p => p.Key, p => p.First().TypeId, StringComparer.OrdinalIgnoreCase);

            _byTypeId = new Dictionary<int, PlanetTypeProfile>();
            _byP0TypeId = new Dictionary<int, List<PlanetTypeProfile>>();

            foreach (var definition in Definitions)
            {
                var profile = new PlanetTypeProfile
                {
                    TypeId = definition.TypeId,
                    Name = definition.Name,
                    IsP4Capable = definition.IsP4Capable,
                    Facilities = definition.Facilities,
                };
                foreach (var name in definition.P0Names)
                {
                    if (!p0ByName.TryGetValue(name, out var p0TypeId))
                    {
                        continue;
                    }
                    if (!profile.P0TypeIds.Contains(p0TypeId))
                    {
                        profile.P0TypeIds.Add(p0TypeId);
                    }
                    if (!_byP0TypeId.TryGetValue(p0TypeId, out var list))
                    {
                        list = new List<PlanetTypeProfile>();
                        _byP0TypeId[p0TypeId] = list;
                    }
                    list.Add(profile);
                }
                _byTypeId[profile.TypeId] = profile;
            }
        }

        public IReadOnlyCollection<PlanetTypeProfile> All => _byTypeId.Values;

        public PlanetTypeProfile GetByTypeId(int typeId)
        {
            return _byTypeId.TryGetValue(typeId, out var profile) ? profile : null;
        }

        public IReadOnlyList<PlanetTypeProfile> GetByP0(int p0TypeId)
        {
            return _byP0TypeId.TryGetValue(p0TypeId, out var list)
                ? list
                : new List<PlanetTypeProfile>();
        }

        public IReadOnlyList<PlanetTypeProfile> GetP4Capable()
        {
            return _byTypeId.Values.Where(p => p.IsP4Capable).ToList();
        }

        private sealed class PlanetTypeDefinition
        {
            public PlanetTypeDefinition(int typeId, string name, bool isP4Capable, string[] p0Names, PlanetFacilityTypeIds facilities)
            {
                TypeId = typeId;
                Name = name;
                IsP4Capable = isP4Capable;
                P0Names = p0Names;
                Facilities = facilities;
            }

            public int TypeId { get; }
            public string Name { get; }
            public bool IsP4Capable { get; }
            public string[] P0Names { get; }
            public PlanetFacilityTypeIds Facilities { get; }
        }
    }
}
