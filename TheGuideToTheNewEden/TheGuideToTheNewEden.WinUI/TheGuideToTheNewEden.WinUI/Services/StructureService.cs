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
        private static readonly string FilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "Structures.json");
        private static Dictionary<long, Structure> Structures { get; set; }
        public static void Init()
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                var list = JsonConvert.DeserializeObject<List<Structure>>(json);
                Structures = list.ToDictionary(p=>p.Id);
            }
            Structures ??= new Dictionary<long, Structure>();
        }

        public static void Save()
        {
            string json = JsonConvert.SerializeObject(Structures.Values);
            string folder = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            File.WriteAllText(FilePath, json);
        }

        public static Structure GetStructure(long id)
        {
            if(Structures.TryGetValue(id,out var value))
            {
                return value;
            }
            else
            {
                return null;
            }
        }
        public static async Task<Structure> QueryStructureAsync(long id, int characterID = -1)
        {
            var local = Structures.Values.FirstOrDefault(p => p.Id == id);
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
                        Structures.Add(id, esi);
                        Save();
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
            var local = Structures.Values.FirstOrDefault(p => p.Id == id);
            if(local != null)
            {
                return local;
            }
            else
            {
                var esi = await GetStructureByESI(id, esiClient);
                Structures.Add(id, esi);
                Save();
                return esi;
            }
        }
        public static async Task<List<Structure>> QueryStructureAsync(List<long> ids, int characterID = -1)
        {
            var locals = Structures.Values.Where(p=>ids.Contains(p.Id)).ToList();
            if(locals.Any())
            {
                var notInLocals = ids.Except(locals.Select(p=>p.Id).ToList()).ToList();
                if(notInLocals.Any())
                {
                    var esis = await GetStructureByESI(notInLocals, characterID);
                    if(esis.NotNullOrEmpty())
                    {
                        locals.AddRange(esis);
                        foreach (var item in esis)
                        {
                            Structures.Add(item.Id, item);
                        }
                        Save();
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
                        Structures.Add(item.Id, item);
                    }
                    Save();
                }
                return esis;
            }
        }

        public static List<Structure> GetStructuresOfRegion(int regionId)
        {
            return Structures.Values.Where(p => p.RegionId == regionId).ToList();
        }

        public static List<Structure> GetStructures()
        {
            return Structures.Values.ToList();
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
