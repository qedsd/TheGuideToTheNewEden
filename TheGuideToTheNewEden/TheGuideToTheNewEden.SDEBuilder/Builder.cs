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

                    var mapConstellations = GetMapConstellations(fileDatas, language);
                    if (mapConstellations != null)
                    {
                        db.CodeFirst.InitTables(typeof(DBModels.MapConstellations));
                        await db.Insertable(mapConstellations).ExecuteCommandAsync();
                    }

                    var blueprints = GetBlueprints(fileDatas);
                    if (blueprints.Item1 != null)
                    {
                        db.CodeFirst.InitTables(typeof(DBModels.Blueprints));
                        db.CodeFirst.InitTables(typeof(DBModels.BlueprintActivity));
                        db.CodeFirst.InitTables(typeof(DBModels.BlueprintMaterial));
                        db.CodeFirst.InitTables(typeof(DBModels.BlueprintProduct));
                        db.CodeFirst.InitTables(typeof(DBModels.BlueprintSkill));
                        await db.Insertable(blueprints.Item1).ExecuteCommandAsync();
                        await db.Insertable(blueprints.Item2).ExecuteCommandAsync();
                        await db.Insertable(blueprints.Item3).ExecuteCommandAsync();
                        await db.Insertable(blueprints.Item4).ExecuteCommandAsync();
                        await db.Insertable(blueprints.Item5).ExecuteCommandAsync();
                    }

                    var typeMaterials = GetTypeMaterials(fileDatas);
                    if (typeMaterials != null)
                    {
                        db.CodeFirst.InitTables(typeof(DBModels.TypeMaterials));
                        await db.Insertable(typeMaterials).ExecuteCommandAsync();
                    }

                    var typeBonus = GetTypeBonus(fileDatas, language);
                    if (typeBonus != null)
                    {
                        db.CodeFirst.InitTables(typeof(DBModels.TypeBonus));
                        await db.Insertable(typeBonus).ExecuteCommandAsync();
                    }

                    var dogmaAttributeCategories = GetDogmaAttributeCategories(fileDatas, language);
                    if (dogmaAttributeCategories != null)
                    {
                        db.CodeFirst.InitTables(typeof(DBModels.DogmaAttributeCategories));
                        await db.Insertable(dogmaAttributeCategories).ExecuteCommandAsync();
                    }

                    var dogmaAttributes = GetDogmaAttributes(fileDatas, language);
                    if (dogmaAttributes != null)
                    {
                        db.CodeFirst.InitTables(typeof(DBModels.DogmaAttributes));
                        await db.Insertable(dogmaAttributes).ExecuteCommandAsync();
                    }

                    var dogmaEffects = GetDogmaEffects(fileDatas, language);
                    if (dogmaEffects != null)
                    {
                        db.CodeFirst.InitTables(typeof(DBModels.DogmaEffects));
                        await db.Insertable(dogmaEffects).ExecuteCommandAsync();
                    }

                    var dogmaUnits = GetDogmaUnits(fileDatas, language);
                    if (dogmaUnits != null)
                    {
                        db.CodeFirst.InitTables(typeof(DBModels.DogmaUnits));
                        await db.Insertable(dogmaUnits).ExecuteCommandAsync();
                    }

                    var typeDogma = GetTypeDogma(fileDatas);
                    if (typeDogma != null)
                    {
                        db.CodeFirst.InitTables(typeof(DBModels.TypeDogma));
                        await db.Insertable(typeDogma).ExecuteCommandAsync();
                    }

                    var planetSchematics = GetPlanetSchematics(fileDatas, language);
                    if (planetSchematics.Item1 != null)
                    {
                        db.CodeFirst.InitTables(typeof(DBModels.PlanetSchematic));
                        db.CodeFirst.InitTables(typeof(DBModels.PlanetSchematicTypeMap));
                        await db.Insertable(planetSchematics.Item1).ExecuteCommandAsync();
                        await db.Insertable(planetSchematics.Item2).ExecuteCommandAsync();
                    }
                    db.Ado.CommitTran();
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
        public static List<DBModels.MapConstellations> GetMapConstellations(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("mapConstellations", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.MapConstellations(p, language)).ToList();
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
                        _esiClient = new EVEStandard.EVEStandardAPI("TheGuideToTheNewEden", DataSource.Tranquility, CompatibilityDate.v2025_12_16, TimeSpan.FromSeconds(30));
                    }
                    foreach(var type in noPackagedVolumeTypes)
                    {
                        EVEStandard.Models.API.ESIModelDTO<EVEStandard.Models.Type> result = null;
                        for(int i = 0; i < 3; i++)
                        {
                            try
                            {
                                result = await _esiClient.Universe.GetTypeInfoAsync(type.Id);
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
        public static (List<DBModels.Blueprints>, List<DBModels.BlueprintActivity>, List<DBModels.BlueprintMaterial>, List<DBModels.BlueprintProduct>, List<DBModels.BlueprintSkill>) GetBlueprints(Dictionary<string, List<BaseModel>> fileDatas)
        {
            if (!fileDatas.TryGetValue("blueprints", out var datas))
            {
                return (null, null, null, null, null);
            }
            List<DBModels.Blueprints> mainList = new List<DBModels.Blueprints>();
            List<DBModels.BlueprintActivity> activityList = new List<DBModels.BlueprintActivity>();
            List<DBModels.BlueprintMaterial> materialList = new List<DBModels.BlueprintMaterial>();
            List<DBModels.BlueprintProduct> productList = new List<DBModels.BlueprintProduct>();
            List<DBModels.BlueprintSkill> skillList = new List<DBModels.BlueprintSkill>();

            Dictionary<string, int> activityNameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "manufacturing", 1 },
                { "research_time", 3 },
                { "research_material", 4 },
                { "copying", 5 },
                { "invention", 8 },
                { "reaction", 11 }
            };

            foreach (var item in datas)
            {
                var bp = item as Blueprints;
                if (bp == null) continue;

                mainList.Add(new DBModels.Blueprints()
                {
                    Id = bp.Id,
                    BlueprintTypeID = bp.BlueprintTypeID,
                    MaxProductionLimit = bp.MaxProductionLimit
                });

                var activities = bp.Activities;
                if (activities == null) continue;

                void ProcessActivity(string activityName, ActivityRequire activity)
                {
                    if (activity == null) return;
                    if (!activityNameToId.TryGetValue(activityName, out var activityId)) return;

                    activityList.Add(new DBModels.BlueprintActivity()
                    {
                        BlueprintTypeID = bp.BlueprintTypeID,
                        ActivityID = activityId,
                        Time = activity.Time
                    });

                    if (activity.Materials != null)
                    {
                        foreach (var mat in activity.Materials)
                        {
                            materialList.Add(new DBModels.BlueprintMaterial()
                            {
                                BlueprintTypeID = bp.BlueprintTypeID,
                                ActivityID = activityId,
                                MaterialTypeID = mat.TypeID,
                                Quantity = mat.Quantity
                            });
                        }
                    }

                    if (activity.Products != null)
                    {
                        foreach (var prod in activity.Products)
                        {
                            productList.Add(new DBModels.BlueprintProduct()
                            {
                                BlueprintTypeID = bp.BlueprintTypeID,
                                ActivityID = activityId,
                                ProductTypeID = prod.TypeID,
                                Quantity = prod.Quantity,
                                Probability = prod.Probability != 1 ? prod.Probability : (double?)null
                            });
                        }
                    }

                    if (activity.Skills != null)
                    {
                        foreach (var skill in activity.Skills)
                        {
                            skillList.Add(new DBModels.BlueprintSkill()
                            {
                                BlueprintTypeID = bp.BlueprintTypeID,
                                ActivityID = activityId,
                                SkillTypeID = skill.TypeID,
                                Level = skill.Level
                            });
                        }
                    }
                }

                ProcessActivity("manufacturing", activities.Manufacturing);
                ProcessActivity("copying", activities.Copying);
                ProcessActivity("research_material", activities.ResearchMaterial);
                ProcessActivity("research_time", activities.ResearchTime);
                ProcessActivity("invention", activities.Invention);
                ProcessActivity("reaction", activities.Reaction);
            }
            return (mainList, activityList, materialList, productList, skillList);
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
        public static List<DBModels.TypeMaterials> GetTypeMaterials(Dictionary<string, List<BaseModel>> fileDatas)
        {
            if (!fileDatas.TryGetValue("typeMaterials", out var datas))
            {
                return null;
            }
            List<DBModels.TypeMaterials> result = new List<DBModels.TypeMaterials>();
            foreach (var item in datas)
            {
                var data = item as TypeMaterials;
                if (data?.Materials != null)
                {
                    foreach (var mat in data.Materials)
                    {
                        result.Add(new DBModels.TypeMaterials()
                        {
                            TypeID = data.Id,
                            MaterialTypeID = mat.MaterialTypeID,
                            Quantity = mat.Quantity
                        });
                    }
                }
            }
            return result;
        }
        public static List<DBModels.TypeBonus> GetTypeBonus(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("typeBonus", out var datas))
            {
                return null;
            }
            List<DBModels.TypeBonus> result = new List<DBModels.TypeBonus>();
            foreach (var item in datas)
            {
                var data = item as DeserializeModels.TypeBonus;
                if (data == null) continue;
                // Process per-type bonuses
                if (data.Types != null)
                {
                    foreach (var typeItem in data.Types)
                    {
                        if (typeItem.Bonuses != null)
                        {
                            foreach (var bonus in typeItem.Bonuses)
                            {
                                result.Add(new DBModels.TypeBonus()
                                {
                                    TypeID = typeItem.TypeID,
                                    SkillTypeID = data.Id,
                                    Bonus = bonus.Bonus,
                                    Importance = bonus.Importance,
                                    UnitID = bonus.UnitID,
                                    BonusText = bonus.BonusText?.GetValue(language),
                                    BonusTextEn = bonus.BonusText?.En
                                });
                            }
                        }
                    }
                }
                // Process role bonuses
                if (data.RoleBonuses != null)
                {
                    foreach (var bonus in data.RoleBonuses)
                    {
                        result.Add(new DBModels.TypeBonus()
                        {
                            TypeID = data.Id,
                            SkillTypeID = data.Id,
                            Bonus = bonus.Bonus,
                            Importance = bonus.Importance,
                            UnitID = bonus.UnitID,
                            BonusText = bonus.BonusText?.GetValue(language),
                            BonusTextEn = bonus.BonusText?.En
                        });
                    }
                }
            }
            return result;
        }
        public static List<DBModels.DogmaAttributeCategories> GetDogmaAttributeCategories(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("dogmaAttributeCategories", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.DogmaAttributeCategories(p, language)).ToList();
        }
        public static List<DBModels.DogmaAttributes> GetDogmaAttributes(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("dogmaAttributes", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.DogmaAttributes(p, language)).ToList();
        }
        public static List<DBModels.DogmaEffects> GetDogmaEffects(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("dogmaEffects", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.DogmaEffects(p, language)).ToList();
        }
        public static List<DBModels.DogmaUnits> GetDogmaUnits(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("dogmaUnits", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.DogmaUnits(p, language)).ToList();
        }
        public static List<DBModels.TypeDogma> GetTypeDogma(Dictionary<string, List<BaseModel>> fileDatas)
        {
            if (!fileDatas.TryGetValue("typeDogma", out var datas))
            {
                return null;
            }
            List<DBModels.TypeDogma> result = new List<DBModels.TypeDogma>();
            foreach (var item in datas)
            {
                var data = item as TypeDogma;
                if (data?.DogmaAttributes != null)
                {
                    foreach (var attr in data.DogmaAttributes)
                    {
                        result.Add(new DBModels.TypeDogma()
                        {
                            TypeID = data.Id,
                            AttributeID = attr.AttributeID,
                            Value = attr.Value
                        });
                    }
                }
            }
            return result;
        }
        public static (List<DBModels.PlanetSchematic>, List<DBModels.PlanetSchematicTypeMap>) GetPlanetSchematics(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("planetSchematics", out var datas))
            {
                return (null, null);
            }
            List<DBModels.PlanetSchematic> schematics = new List<DBModels.PlanetSchematic>();
            List<DBModels.PlanetSchematicTypeMap> typeMaps = new List<DBModels.PlanetSchematicTypeMap>();
            foreach (var item in datas)
            {
                var data = item as PlanetSchematics;
                if (data == null) continue;
                schematics.Add(new DBModels.PlanetSchematic(data, language));
                if (data.Types != null)
                {
                    foreach (var t in data.Types)
                    {
                        typeMaps.Add(new DBModels.PlanetSchematicTypeMap()
                        {
                            SchematicId = data.Id,
                            TypeId = t.TypeID,
                            Quantity = t.Quantity,
                            IsInput = t.IsInput
                        });
                    }
                }
            }
            return (schematics, typeMaps);
        }
    }
}
