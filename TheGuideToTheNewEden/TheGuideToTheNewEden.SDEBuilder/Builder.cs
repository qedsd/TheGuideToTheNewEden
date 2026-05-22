using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EVEStandard.Enumerations;
using Newtonsoft.Json;
using SqlSugar;
using TheGuideToTheNewEden.SDEBuilder.DeserializeModels;
using TheGuideToTheNewEden.SDEBuilder.Helpers;


namespace TheGuideToTheNewEden.SDEBuilder
{
    public static class Builder
    {
        private static EVEStandard.EVEStandardAPI _esiClient;
        public static async Task StartBuilder(string[] sdeFiles, LanguageEnum language, string outputFile, bool getPackagedVolume)
        {
            var db = CreateDB(outputFile);
            try
            {
                var fileDatas = await Task.Run(() => ReadAllFiles(sdeFiles, language));
                if (fileDatas.Any())
                {
                    var categories = GetCategories(fileDatas, language);
                    if (categories != null)
                    {
                        db.CodeFirst.InitTables(typeof(DBModels.Categories));
                        await db.Insertable(categories).ExecuteCommandAsync();
                    }

                    var groups = GetGroups(fileDatas, language);
                    if(groups != null)
                    {
                        db.CodeFirst.InitTables(typeof(DBModels.Groups));
                        await db.Insertable(groups).ExecuteCommandAsync();
                    }

                    var mapRegions = GetMapRegions(fileDatas, language);
                    if (mapRegions != null)
                    {
                        db.CodeFirst.InitTables(typeof(DBModels.MapRegions));
                        await db.Insertable(mapRegions).ExecuteCommandAsync();
                    }

                    var mapSolarSystems = GetMapSolarSystems(fileDatas, language);
                    if (mapSolarSystems != null)
                    {
                        db.CodeFirst.InitTables(typeof(DBModels.MapSolarSystems));
                        await db.Insertable(mapSolarSystems).ExecuteCommandAsync();
                    }

                    var marketGroups = GetMarketGroups(fileDatas, language);
                    if (marketGroups != null)
                    {
                        db.CodeFirst.InitTables(typeof(DBModels.MarketGroups));
                        await db.Insertable(marketGroups).ExecuteCommandAsync();
                    }

                    var types = await GetTypes(fileDatas, language, getPackagedVolume);
                    if (types != null)
                    {
                        db.CodeFirst.InitTables(typeof(DBModels.Types));
                        await db.Insertable(types).ExecuteCommandAsync();
                    }

                    var planetResources = GetPlanetResources(fileDatas, language);
                    if (planetResources != null)
                    {
                        db.CodeFirst.InitTables(typeof(DBModels.PlanetResources));
                        await db.Insertable(planetResources).ExecuteCommandAsync();
                    }

                    var mapSolarSystemJumps = GetMapSolarSystemJumps(fileDatas);
                    if (mapSolarSystemJumps != null)
                    {
                        db.CodeFirst.InitTables(typeof(DBModels.MapSolarSystemJumps));
                        await db.Insertable(mapSolarSystemJumps).ExecuteCommandAsync();
                    }

                    var mapDenormalizes = GetMapDenormalizes(fileDatas, language);
                    if (mapDenormalizes != null)
                    {
                        db.CodeFirst.InitTables(typeof(DBModels.MapDenormalizes));
                        await db.Insertable(mapDenormalizes).ExecuteCommandAsync();

                        var mapDenormalizesDict = mapDenormalizes.ToDictionary(p => p.ID);
                        var stations = GetStations(fileDatas, language, mapDenormalizesDict);
                        if (stations != null)
                        {
                            db.CodeFirst.InitTables(typeof(DBModels.Stations));
                            await db.Insertable(stations).ExecuteCommandAsync();
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                throw ex;
            }
            finally
            {
                db.Close();
                db.Dispose();
            }
        }

        public static SqlSugarScope CreateDB(string outputFile)
        {
            string dbPath = Path.GetFullPath(outputFile);
            var db = new SqlSugarScope(new ConnectionConfig()
            {
                ConnectionString = @"DataSource=" + dbPath,
                DbType = SqlSugar.DbType.Sqlite,
                IsAutoCloseConnection = true,
                ConfigureExternalServices = new ConfigureExternalServices
                {
                    EntityService = (c, p) =>
                    {
                        // int?  decimal?这种 isnullable=true
                        if (c.PropertyType.IsGenericType && c.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                        {
                            p.IsNullable = true;
                        }
                    }
                }
            });
            if (!System.IO.File.Exists(dbPath))
            {
                db.DbMaintenance.CreateDatabase();
            }
            return db;
        }
        public static Dictionary<string, List<BaseModel>> ReadAllFiles(string[] sdeFiles, LanguageEnum language)
        {
            Dictionary<string, List<BaseModel>> fileDatas = new Dictionary<string, List<BaseModel>>();
            var models = typeof(BaseModel).Assembly.GetTypes().Where(p => p.FullName.Contains(".DeserializeModels.")).ToList();
            foreach (var file in sdeFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                var targetModel = models.FirstOrDefault(p => p.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                if (targetModel != null)
                {
                    List<BaseModel> datas = new List<BaseModel>();
                    fileDatas.Add(Path.GetFileNameWithoutExtension(file), datas);
                    foreach (string line in File.ReadLines(file))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            try
                            {
                                datas.Add(JsonConvert.DeserializeObject(line, targetModel) as BaseModel);
                            }
                            catch (Exception ex)
                            {
                                throw new Exception(ex.ToString());
                            }
                        }
                    }
                }
            }
            return fileDatas;
        }

        public static List<DBModels.Groups> GetGroups(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("groups", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.Groups(p, language)).ToList();
        }
        public static List<DBModels.Categories> GetCategories(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("categories", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.Categories(p, language)).ToList();
        }
        public static List<DBModels.MapRegions> GetMapRegions(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("mapRegions", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.MapRegions(p, language)).ToList();
        }
        public static List<DBModels.MapSolarSystems> GetMapSolarSystems(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("mapSolarSystems", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.MapSolarSystems(p, language)).ToList();
        }
        public static List<DBModels.MarketGroups> GetMarketGroups(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("marketGroups", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.MarketGroups(p, language)).ToList();
        }
        public static async Task<List<DBModels.Types>> GetTypes(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language, bool getPackagedVolume)
        {
            if (!fileDatas.TryGetValue("types", out var datas))
            {
                return null;
            }
            var types = datas.Select(p => new DBModels.Types(p, language)).ToList();
            if (getPackagedVolume)
            {
                var dict = RefDBHelper.QueryType().ToDictionary(p=>p.TypeID);
                List<DBModels.Types> noPackagedVolumeTypes = new List<DBModels.Types>();
                foreach (var type in types)
                {
                    if(dict.TryGetValue(type.Id, out var invType))
                    {
                        type.PackagedVolume = invType.PackagedVolume;
                    }
                    else
                    {
                        noPackagedVolumeTypes.Add(type);
                    }
                }
                if (noPackagedVolumeTypes.Count > 0)
                {
                    if(_esiClient == null)
                    {
                        _esiClient = new EVEStandard.EVEStandardAPI("TheGuideToTheNewEden", DataSource.Tranquility, TimeSpan.FromSeconds(30));
                    }
                    foreach(var type in noPackagedVolumeTypes)
                    {
                        EVEStandard.Models.API.ESIModelDTO<EVEStandard.Models.Type> result = null;
                        for(int i = 0; i < 3; i++)
                        {
                            try
                            {
                                result = await _esiClient.Universe.GetTypeInfoV3Async(type.Id);
                                break;
                            }
                            catch (Exception ex)
                            {
                                if(i == 2)
                                {
                                    throw new Exception($"Get {type.Name}({type.Id}) PackagedVolume failed after 3 retries", ex);
                                }
                                else
                                {
                                    Thread.Sleep(1000);
                                }
                            }
                        }
                        
                        if (result.Model != null)
                        {
                            type.PackagedVolume = (double)result.Model.PackagedVolume;
                        }
                        else
                        {
                            throw new Exception($"Get {type.Name}({type.Id}) PackagedVolume failed");
                        }
                    }
                    RefDBHelper.AddType(noPackagedVolumeTypes.Select(p => new InvType() { TypeID = p.Id, Volume = p.Volume, PackagedVolume = p.PackagedVolume }).ToList());
                }
            }
            return types;
        }
        public static List<DBModels.PlanetResources> GetPlanetResources(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("planetResources", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.PlanetResources(p)).ToList();
        }
        public static List<DBModels.MapSolarSystemJumps> GetMapSolarSystemJumps(Dictionary<string, List<BaseModel>> fileDatas)
        {
            if(!fileDatas.TryGetValue("mapStargates",out var mapStargates) || !fileDatas.TryGetValue("mapSolarSystems", out var mapSolarSystems))
            {
                return null;
            }
            var mapStargatesDict = mapStargates.ToDictionary(p => p.Id);
            var mapSolarSystemsDict = mapSolarSystems.ToDictionary(p => p.Id);
            List<DBModels.MapSolarSystemJumps> jumps = new List<DBModels.MapSolarSystemJumps>();
            foreach(var system in mapSolarSystems)
            {
                var from = system as MapSolarSystems;
                if(from?.StargateIDs != null)
                {
                    foreach (var gateID in from.StargateIDs)
                    {
                        var gate = mapStargatesDict[gateID] as MapStargates;
                        var to = mapSolarSystemsDict[gate.Destination.SolarSystemID] as MapSolarSystems;
                        jumps.Add(new DBModels.MapSolarSystemJumps()
                        {
                            FromSolarSystemID = from.Id,
                            FromConstellationID = from.ConstellationID,
                            FromRegionID = from.RegionID,
                            ToSolarSystemID = to.Id,
                            ToConstellationID = to.ConstellationID,
                            ToRegionID = to.RegionID,
                        });
                    }
                }
            }
            return jumps;
        }
        public static List<DBModels.MapDenormalizes> GetMapDenormalizes(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("mapStars", out var mapStars) 
                || !fileDatas.TryGetValue("mapPlanets", out var mapPlanets)
                || !fileDatas.TryGetValue("mapMoons", out var mapMoons)
                || !fileDatas.TryGetValue("mapAsteroidBelts", out var mapAsteroidBelts)
                || !fileDatas.TryGetValue("mapSolarSystems", out var mapSolarSystems)
                || !fileDatas.TryGetValue("groups", out var groups))
            {
                return null;
            }
            var groupsDict = groups.Select(p=>p as Groups).ToDictionary(p=>p.Id);
            var starGroupName = groupsDict[6].Names.GetValue(language);
            var mapSolarSystemsDict = mapSolarSystems.Select(p=>p as MapSolarSystems).ToDictionary(p=>p.Id);
            List<DBModels.MapDenormalizes> starDatas = new List<DBModels.MapDenormalizes>();
            foreach(var item in mapStars)
            {
                var data = item as MapStars;
                starDatas.Add(new DBModels.MapDenormalizes()
                {
                    ID = data.Id,
                    GroupID = 6,
                    SolarSystemID = data.SolarSystemID,
                    TypeID = data.TypeID,
                    ItemName = $"{mapSolarSystemsDict[data.SolarSystemID].Names.GetValue(language)} - {starGroupName}"
                });
            }
            List<DBModels.MapDenormalizes> planetDatas = new List<DBModels.MapDenormalizes>();
            foreach (var item in mapPlanets)
            {
                var data = item as MapPlanets;
                planetDatas.Add(new DBModels.MapDenormalizes()
                {
                    ID = data.Id,
                    GroupID = 7,
                    SolarSystemID = data.SolarSystemID,
                    TypeID = data.TypeID,
                    ItemName = $"{mapSolarSystemsDict[data.SolarSystemID].Names.GetValue(language)} {RomanConverter.ToRoman(data.CelestialIndex)}"
                });
            }
            var planetDatasDict = planetDatas.ToDictionary(p => p.ID);
            var moonGroupName = groupsDict[8].Names.GetValue(language);
            List<DBModels.MapDenormalizes> moonDatas = new List<DBModels.MapDenormalizes>();
            foreach (var item in mapMoons)
            {
                var data = item as MapMoons;
                moonDatas.Add(new DBModels.MapDenormalizes()
                {
                    ID = data.Id,
                    GroupID = 8,
                    SolarSystemID = data.SolarSystemID,
                    TypeID = data.TypeID,
                    ItemName = $"{planetDatasDict[data.OrbitID].ItemName} - {moonGroupName} {data.OrbitIndex}"
                });
            }
            List<DBModels.MapDenormalizes> beltDatas = new List<DBModels.MapDenormalizes>();
            var beltGroupName = groupsDict[9].Names.GetValue(language);
            foreach (var item in mapAsteroidBelts)
            {
                var data = item as MapAsteroidBelts;
                beltDatas.Add(new DBModels.MapDenormalizes()
                {
                    ID = data.Id,
                    GroupID = 9,
                    SolarSystemID = data.SolarSystemID,
                    TypeID = data.TypeID,
                    ItemName = $"{planetDatasDict[data.OrbitID].ItemName} - {beltGroupName} {data.OrbitIndex}"
                });
            }
            List<DBModels.MapDenormalizes> allDatas = new List<DBModels.MapDenormalizes>();
            allDatas.AddRange(starDatas);
            allDatas.AddRange(planetDatas);
            allDatas.AddRange(moonDatas);
            allDatas.AddRange(beltDatas);
            return allDatas;
        }
        public static List<DBModels.Stations> GetStations(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language, Dictionary<int, DBModels.MapDenormalizes> mapDenormalizesDict)
        {
            if (!fileDatas.TryGetValue("npcStations", out var npcStations)
                || !fileDatas.TryGetValue("npcCorporations", out var npcCorporations)
                || !fileDatas.TryGetValue("stationOperations", out var stationOperations))
            {
                return null;
            }
            var npcCorporationsDict = npcCorporations.Select(p => p as NpcCorporations).ToDictionary(p => p.Id);
            var stationOperationsDict = stationOperations.Select(p => p as StationOperations).ToDictionary(p => p.Id);
            List<DBModels.Stations> datas = new List<DBModels.Stations>();
            foreach (var item in npcStations)
            {
                var data = item as NpcStations;
                datas.Add(new DBModels.Stations()
                {
                    Id = data.Id,
                    SolarSystemID = data.SolarSystemID,
                    StationName = $"{mapDenormalizesDict[data.OrbitID].ItemName} - {npcCorporationsDict[data.OwnerID].Names.GetValue(language)} {stationOperationsDict[data.OperationID].OperationName.GetValue(language)}"
                });
            }
            return datas;
        }
    }
}
