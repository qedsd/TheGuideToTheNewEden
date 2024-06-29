using ESI.NET;
using ESI.NET.Models.SSO;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.Core.Models.Universe;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Services;
using Octokit;
using ESI.NET.Models.Character;
using WinUICommunity;
using Newtonsoft.Json.Linq;

namespace TheGuideToTheNewEden.WinUI.Services
{
    public class StructureService
    {
        private static readonly string MarketStrutureFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "MarketStructures.json");
        private static readonly string AutoStructureFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "Structures.json");
        private static ObservableCollection<Structure> MarketStructures { get; set; }
        private static Dictionary<long, Structure> AutoStructures { get; set; }
        public static void Init()
        {
            if (File.Exists(AutoStructureFilePath))
            {
                string json = File.ReadAllText(AutoStructureFilePath);
                var list = JsonConvert.DeserializeObject<List<Structure>>(json);
                if(list.NotNullOrEmpty())
                    AutoStructures = list.Where(p=>p != null).ToDictionary(p=>p.Id);
            }
            AutoStructures ??= new Dictionary<long, Structure>();

            if(File.Exists(MarketStrutureFilePath))
            {
                string json = File.ReadAllText(MarketStrutureFilePath);
                var list = JsonConvert.DeserializeObject<List<Structure>>(json);
                if (list.NotNullOrEmpty())
                    MarketStructures = list.Where(p => p != null).ToObservableCollection();
            }
            MarketStructures ??= new ObservableCollection<Structure>();
        }

        private static void SaveAutoStructure()
        {
            string json = JsonConvert.SerializeObject(AutoStructures.Values);
            string folder = Path.GetDirectoryName(AutoStructureFilePath);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            File.WriteAllText(AutoStructureFilePath, json);
        }

        public static Structure GetStructure(long id)
        {
            if(AutoStructures.TryGetValue(id,out var value))
            {
                return value;
            }
            else
            {
                return MarketStructures.FirstOrDefault(p => p.Id == id);
            }
        }
        public static List<Structure> GetStructure(List<long> ids)
        {
            var autos = AutoStructures.Values.Where(p => ids.Contains(p.Id)).ToList();
            var market = MarketStructures.Where(p=> ids.Contains(p.Id)).ToList();
            if(autos.NotNullOrEmpty() || market.NotNullOrEmpty())
            {
                List<Structure> structures = new List<Structure>();
                if(autos.NotNullOrEmpty())
                {
                    structures.AddRange(autos);
                }
                if (market.NotNullOrEmpty())
                {
                    foreach (var s in market)
                    {
                        if (structures.FirstOrDefault(p => p.Id == s.Id) == null)
                        {
                            structures.Add(s);
                        }
                    }
                }
                return structures;
            }
            else
            {
                return null;
            }
        }
        public static async Task<Structure> QueryStructureAsync(long id, int characterID = -1)
        {
            var local = GetStructure(id);
            if (local != null)
            {
                return local;
            }
            else
            {
                if(characterID > 0)
                {
                    try
                    {
                        var esi = await GetStructureByESI(id, characterID);
                        if(esi != null)
                        {
                            AutoStructures.Add(id, esi);
                            SaveAutoStructure();
                        }
                        return esi;
                    }
                    catch (Exception ex)
                    {
                        Core.Log.Error(ex);
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
        }
        public static async Task<Structure> QueryStructureAsync(long id, EsiClient esiClient)
        {
            var local = GetStructure(id);
            if(local != null)
            {
                return local;
            }
            else
            {
                var esi = await GetStructureByESI(id, esiClient);
                if(esi != null)
                {
                    AutoStructures.Add(id, esi);
                    SaveAutoStructure();
                }
                return esi;
            }
        }
        public static async Task<List<Structure>> QueryStructureAsync(List<long> ids, int characterID = -1)
        {
            var locals = GetStructure(ids);
            if(locals.Any())
            {
                var notInLocals = ids.Except(locals.Select(p=>p.Id).ToList()).ToList();
                if(notInLocals.Any())
                {
                    var esis = await GetStructureByESI(notInLocals, characterID);
                    if(esis.NotNullOrEmpty())
                    {
                        var notNull = esis.Where(p=>p != null).ToList();
                        if(notNull.NotNullOrEmpty())
                        {
                            locals.AddRange(notNull);
                            foreach (var item in notNull)
                            {
                                AutoStructures.Add(item.Id, item);
                            }
                            SaveAutoStructure();
                        }
                    }
                }
                return locals;
            }
            else
            {
                var esis = await GetStructureByESI(ids, characterID);
                if(esis.NotNullOrEmpty())
                {
                    foreach (var item in esis)
                    {
                        if(item != null)
                        {
                            AutoStructures.Add(item.Id, item);
                        }
                    }
                    SaveAutoStructure();
                }
                return esis;
            }
        }

        public static List<Structure> GetStructuresOfRegion(int regionId)
        {
            var autos = AutoStructures.Values.Where(p => p.RegionId == regionId).ToList();
            var market = MarketStructures.Where(p => p.RegionId == regionId).ToList();
            if (autos.NotNullOrEmpty() || market.NotNullOrEmpty())
            {
                List<Structure> structures = new List<Structure>();
                if (autos.NotNullOrEmpty())
                {
                    structures.AddRange(autos);
                }
                if (market.NotNullOrEmpty())
                {
                    foreach(var s in market)
                    {
                        if(structures.FirstOrDefault(p=>p.Id == s.Id) == null)
                        {
                            structures.Add(s);
                        }
                    }
                }
                return structures;
            }
            else
            {
                return null;
            }
        }
        public static ObservableCollection<Structure> GetMarketStrutures()
        {
            return MarketStructures;
        }
        public static void SaveMarketStrutures()
        {
            string json = JsonConvert.SerializeObject(MarketStructures);
            string folder = Path.GetDirectoryName(MarketStrutureFilePath);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            File.WriteAllText(MarketStrutureFilePath, json);
        }

        private static async Task<Core.Models.Universe.Structure> GetStructureByESI(long id, int characterID)
        {
            EsiClient esiClient = ESIService.GetDefaultEsi();
            esiClient.SetCharacterData(Services.CharacterService.CharacterOauths.FirstOrDefault(p => p.CharacterID == characterID));
            var structure = await GetStructureByESI(id, esiClient);
            if(structure != null)
            {
                structure.CharacterId = characterID;
                var system = await Core.Services.DB.MapSolarSystemService.QueryAsync(structure.SolarSystemId);
                if (system != null)
                {
                    structure.RegionId = system.RegionID;
                    structure.SolarSystemName = system.SolarSystemName;
                    var region = await Core.Services.DB.MapRegionService.QueryAsync(structure.RegionId);
                    if (region != null)
                    {
                        structure.RegionName = region.RegionName;
                    }
                }
            }
            return structure;
        }
        private static async Task<List<Core.Models.Universe.Structure>> GetStructureByESI(List<long> ids, int characterID = -1)
        {
            EsiClient esiClient;
            if (characterID > -1)
            {
                esiClient = ESIService.GetDefaultEsi();
                esiClient.SetCharacterData(Services.CharacterService.CharacterOauths.FirstOrDefault(p => p.CharacterID == characterID));
            }
            else
            {
                esiClient = ESIService.Current.EsiClient;
            }
            List<Core.Models.Universe.Structure> structures = new List<Structure>();
            foreach(var id in ids)
            {
                var structure = await GetStructureByESI(id, esiClient);
                if (structure != null)
                {
                    structure.CharacterId = characterID;
                    structures.Add(structure);
                }
            }
            if (structures.Any())
            {
                var systems = await Core.Services.DB.MapSolarSystemService.QueryAsync(structures.Select(p => p.SolarSystemId).Distinct().ToList());
                if (systems.NotNullOrEmpty())
                {
                    var dic = systems.ToDictionary(p => p.SolarSystemID);
                    foreach (var d in structures)
                    {
                        if (dic.TryGetValue(d.SolarSystemId, out var value))
                        {
                            d.RegionId = value.RegionID;
                            d.SolarSystemName = value.SolarSystemName;
                        }
                    }
                    var regions = await Core.Services.DB.MapRegionService.QueryAsync(structures.Select(p => p.RegionId).Distinct().ToList());
                    if (regions.NotNullOrEmpty())
                    {
                        var dic2 = regions.ToDictionary(p => p.RegionID);
                        foreach (var d in structures)
                        {
                            if (dic2.TryGetValue(d.RegionId, out var value))
                            {
                                d.RegionName = value.RegionName;
                            }
                        }
                    }
                }
            }
            return structures;
        }

        private static async Task<Core.Models.Universe.Structure> GetStructureByESI(long id, EsiClient inputESIClient = null)
        {
            EsiClient esiClient = inputESIClient ?? ESIService.Current.EsiClient;
            var resp = await esiClient.Universe.Structure(id);
            if (resp != null && resp.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var system = await Core.Services.DB.MapSolarSystemService.QueryAsync(resp.Data.SolarSystemId);
                if (system != null)
                {
                    var region = await Core.Services.DB.MapRegionService.QueryAsync(system.RegionID);
                    if (region != null)
                    {
                        return new Structure()
                        {
                            Id = id,
                            Name = resp.Data.Name,
                            SolarSystemId = resp.Data.SolarSystemId,
                            SolarSystemName = system.SolarSystemName,
                            RegionId = region.RegionID,
                            RegionName = region.RegionName
                        };
                    }
                }
                return new Structure()
                {
                    Id = id,
                    Name = resp.Data.Name,
                    SolarSystemId = resp.Data.SolarSystemId
                };
            }
            else
            {
                return null;
            }
        }
    }
}
