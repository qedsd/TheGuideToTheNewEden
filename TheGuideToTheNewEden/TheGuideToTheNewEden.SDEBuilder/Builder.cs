using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public static async Task StartBuilder(string[] sdeFiles, LanguageEnum language, string outputFile, bool getPackagedVolume)
        {
            var db = CreateDB(outputFile);
            try
            {
                var fileDatas = await Task.Run(() => ReadAllFiles(sdeFiles, language));
                if (fileDatas.Any())
                {
                    await WriteAllTablesAsync(db, fileDatas, language, getPackagedVolume, sdeFiles, outputFile);
                }
            }
            finally
            {
                db.Close();
                db.Dispose();
            }
        }

        /// <summary>
        /// 单库构建与 NewEden main 库共用的落库管线。
        /// 所有表只在这里维护一次，避免两条构建路径的表集合再次漂移。
        /// </summary>
        public static async Task WriteAllTablesAsync(SqlSugarScope db, Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language, bool getPackagedVolume, string[] sdeFiles, string outputFile)
        {
            await WriteTableAsync(db, GetCategories(fileDatas, language));
            await WriteTableAsync(db, GetGroups(fileDatas, language));
            await WriteTableAsync(db, GetMapRegions(fileDatas, language));
            await WriteTableAsync(db, GetMapSolarSystems(fileDatas, language));
            await WriteTableAsync(db, GetMapConstellations(fileDatas, language));
            await WriteTableAsync(db, GetMarketGroups(fileDatas, language));
            await WriteTableAsync(db, await GetTypes(fileDatas, language, getPackagedVolume));
            await WriteTableAsync(db, GetPlanetResources(fileDatas, language));
            await WriteTableAsync(db, GetSovereigntyUpgrades(fileDatas), true);
            await WriteTableAsync(db, GetMapSecondarySuns(fileDatas));
            await WriteTableAsync(db, GetMapSolarSystemJumps(fileDatas));

            var mapDenormalizes = GetMapDenormalizes(fileDatas, language);
            await WriteTableAsync(db, mapDenormalizes);
            if (mapDenormalizes != null)
            {
                var mapDenormalizesDict = mapDenormalizes.ToDictionary(p => p.ID);
                await WriteTableAsync(db, GetStations(fileDatas, language, mapDenormalizesDict));
            }

            var blueprints = GetBlueprints(fileDatas);
            await WriteTableAsync(db, blueprints.Item1);
            await WriteTableAsync(db, blueprints.Item2, true);
            await WriteTableAsync(db, blueprints.Item3, true);
            await WriteTableAsync(db, blueprints.Item4, true);
            await WriteTableAsync(db, blueprints.Item5, true);

            await WriteTableAsync(db, GetTypeMaterials(fileDatas), true);
            await WriteTableAsync(db, GetTypeBonus(fileDatas, language), true);
            await WriteTableAsync(db, GetDogmaAttributeCategories(fileDatas, language), true);
            await WriteTableAsync(db, GetDogmaAttributes(fileDatas, language), true);
            await WriteTableAsync(db, GetDogmaEffects(fileDatas, language), true);
            await WriteTableAsync(db, GetDogmaUnits(fileDatas, language), true);
            await WriteTableAsync(db, GetTypeDogma(fileDatas), true);

            var planetSchematics = GetPlanetSchematics(fileDatas, language);
            await WriteTableAsync(db, planetSchematics.Item1);
            await WriteTableAsync(db, planetSchematics.Item2, true);

            await WriteTableAsync(db, GetFactions(fileDatas, language));
            await WriteTableAsync(db, GetRaces(fileDatas, language));
            await WriteTableAsync(db, GetMetaGroups(fileDatas, language));
            await WriteTableAsync(db, GetStationServices(fileDatas, language));
            await WriteTableAsync(db, GetNpcCorporationDivisions(fileDatas, language));
            await WriteTableAsync(db, GetArchetypes(fileDatas, language));
            await WriteTableAsync(db, GetCompressibleTypes(fileDatas));
            await WriteTableAsync(db, GetTypeLists(fileDatas));
            await WriteTableAsync(db, GetMasteries(fileDatas), true);
            await WriteTableAsync(db, GetCloneGrades(fileDatas));
            await WriteTableAsync(db, GetNpcCharacters(fileDatas, language));
            await WriteTableAsync(db, GetLandmarks(fileDatas, language));
            await WriteTableAsync(db, GetMercenaryTacticalOperations(fileDatas, language));

            await WriteTableAsync(db, GetIndustryActivities(fileDatas, language));
            var industryAssemblyLines = GetIndustryAssemblyLines(fileDatas, language);
            await WriteTableAsync(db, industryAssemblyLines.Item1);
            await WriteTableAsync(db, industryAssemblyLines.Item2, true);
            await WriteTableAsync(db, GetIndustryInstallationTypes(fileDatas), true);
            await WriteTableAsync(db, GetIndustryModifierSources(fileDatas), true);
            var industryTargetFilters = GetIndustryTargetFilters(fileDatas);
            await WriteTableAsync(db, industryTargetFilters.Item1);
            await WriteTableAsync(db, industryTargetFilters.Item2, true);
            await WriteTableAsync(db, industryTargetFilters.Item3, true);

            var sdeInfo = GetSDEInfo(sdeFiles);
            if (sdeInfo != null)
            {
                await WriteTableAsync(db, new List<DBModels.SDEInfo> { sdeInfo });
            }

            BuildVerifier.Verify(db, outputFile, fileDatas, sdeFiles);
        }

        private static async Task WriteTableAsync<T>(SqlSugarScope db, List<T> rows, bool createIfEmpty = false) where T : class, new()
        {
            if (rows == null)
            {
                return;
            }
            if (rows.Count == 0 && !createIfEmpty)
            {
                return;
            }
            db.CodeFirst.InitTables(typeof(T));
            if (rows.Count == 0)
            {
                return;
            }
            await db.Insertable(rows).ExecuteCommandAsync();
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

        public static Task<List<DBModels.Types>> GetTypes(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language, bool getPackagedVolume)
        {
            if (!fileDatas.TryGetValue("types", out var datas))
            {
                return Task.FromResult<List<DBModels.Types>>(null);
            }
            var types = datas.Select(p => new DBModels.Types(p, language)).ToList();

            // 打包体积（packagedVolume）自 CCP SDE 4.0 起直接随 types.jsonl 提供，已在
            // DBModels.Types 的构造函数里读取；这里的 ref.db 仅作兜底（本地库，不发网络）。
            // **不再逐条调用 ESI /universe/types/{id} 补全**：新 SDE 下会有数万个类型进入该分支，
            // 任一请求失败/超时都会抛异常并中断整个构建——连 types 表都写不出来，
            // 其后的全部工业表（blueprints/blueprintMaterials/...）随之缺失，留下的只是一个残缺库。
            if (getPackagedVolume)
            {
                try
                {
                    RefDBHelper.Init();
                    var dict = RefDBHelper.QueryType().ToDictionary(p => p.TypeID);
                    foreach (var type in types)
                    {
                        if (type.PackagedVolume <= 0
                            && dict.TryGetValue(type.Id, out var invType)
                            && invType.PackagedVolume > 0)
                        {
                            type.PackagedVolume = invType.PackagedVolume;
                        }
                    }
                }
                catch (Exception)
                {
                    // ref.db 缺失/损坏不影响主线构建，后续按 Volume 兜底。
                }
            }

            // 仍缺失者（天体/抽象物品等）打包体积即等于体积。
            foreach (var type in types)
            {
                if (type.PackagedVolume <= 0)
                {
                    type.PackagedVolume = type.Volume;
                }
            }
            return Task.FromResult(types);
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

        /// <summary>设施升级定义（SDE sovereigntyUpgrades.jsonl）。</summary>
        public static List<DBModels.SovereigntyUpgrades> GetSovereigntyUpgrades(Dictionary<string, List<BaseModel>> fileDatas)
        {
            var datas = fileDatas
                .FirstOrDefault(p => p.Key.Equals("sovereigntyUpgrades", StringComparison.OrdinalIgnoreCase))
                .Value;
            return datas == null
                ? new List<DBModels.SovereigntyUpgrades>()
                : datas.Select(p => new DBModels.SovereigntyUpgrades(p)).ToList();
        }

        public static List<DBModels.MapSecondarySuns> GetMapSecondarySuns(Dictionary<string, List<BaseModel>> fileDatas)
        {
            if (!fileDatas.TryGetValue("mapSecondarySuns", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.MapSecondarySuns(p)).ToList();
        }

        public static List<DBModels.MapSolarSystemJumps> GetMapSolarSystemJumps(Dictionary<string, List<BaseModel>> fileDatas)
        {
            if (!fileDatas.TryGetValue("mapStargates", out var mapStargates) || !fileDatas.TryGetValue("mapSolarSystems", out var mapSolarSystems))
            {
                return null;
            }
            var mapStargatesDict = mapStargates.Select(p => p as MapStargates).Where(p => p != null).ToDictionary(p => p.Id);
            var mapSolarSystemsDict = mapSolarSystems.Select(p => p as MapSolarSystems).Where(p => p != null).ToDictionary(p => p.Id);
            List<DBModels.MapSolarSystemJumps> jumps = new List<DBModels.MapSolarSystemJumps>();
            foreach (var system in mapSolarSystems)
            {
                var from = system as MapSolarSystems;
                if (from?.StargateIDs != null)
                {
                    foreach (var gateID in from.StargateIDs)
                    {
                        if (!mapStargatesDict.TryGetValue(gateID, out var gate) || gate.Destination == null)
                        {
                            continue;
                        }
                        if (!mapSolarSystemsDict.TryGetValue(gate.Destination.SolarSystemID, out var to))
                        {
                            continue;
                        }
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

            var groupsDict = groups.Select(p => p as Groups).Where(p => p != null).ToDictionary(p => p.Id);
            string GroupName(int id, string fallback)
            {
                return groupsDict.TryGetValue(id, out var group) ? (group.Names?.GetValue(language) ?? fallback) : fallback;
            }

            var mapSolarSystemsDict = mapSolarSystems.Select(p => p as MapSolarSystems).Where(p => p != null).ToDictionary(p => p.Id);
            string SystemName(int id)
            {
                return mapSolarSystemsDict.TryGetValue(id, out var system) ? (system.Names?.GetValue(language) ?? id.ToString()) : id.ToString();
            }

            var starGroupName = GroupName(6, "Sun");
            var moonGroupName = GroupName(8, "Moon");
            var beltGroupName = GroupName(9, "Asteroid Belt");
            var gateGroupName = GroupName(10, "Stargate");
            var secondarySunGroupName = GroupName(995, "Secondary Sun");

            List<DBModels.MapDenormalizes> allDatas = new List<DBModels.MapDenormalizes>();
            var celestialDict = new Dictionary<int, DBModels.MapDenormalizes>();

            foreach (var item in mapStars)
            {
                var data = item as MapStars;
                if (data == null) continue;
                var row = new DBModels.MapDenormalizes()
                {
                    ID = data.Id,
                    GroupID = 6,
                    SolarSystemID = data.SolarSystemID,
                    TypeID = data.TypeID,
                    Radius = data.Radius,
                    ItemName = $"{SystemName(data.SolarSystemID)} - {starGroupName}"
                };
                allDatas.Add(row);
                celestialDict[data.Id] = row;
            }

            foreach (var item in mapPlanets)
            {
                var data = item as MapPlanets;
                if (data == null) continue;
                var row = new DBModels.MapDenormalizes()
                {
                    ID = data.Id,
                    GroupID = 7,
                    SolarSystemID = data.SolarSystemID,
                    TypeID = data.TypeID,
                    Radius = data.Radius,
                    ItemName = $"{SystemName(data.SolarSystemID)} {RomanConverter.ToRoman(data.CelestialIndex)}"
                };
                allDatas.Add(row);
                celestialDict[data.Id] = row;
            }

            foreach (var item in mapMoons)
            {
                var data = item as MapMoons;
                if (data == null) continue;
                var parent = celestialDict.TryGetValue(data.OrbitID, out var planet) ? planet.ItemName : SystemName(data.SolarSystemID);
                var row = new DBModels.MapDenormalizes()
                {
                    ID = data.Id,
                    GroupID = 8,
                    SolarSystemID = data.SolarSystemID,
                    TypeID = data.TypeID,
                    Radius = data.Radius,
                    ItemName = $"{parent} - {moonGroupName} {data.OrbitIndex}"
                };
                allDatas.Add(row);
                celestialDict[data.Id] = row;
            }

            foreach (var item in mapAsteroidBelts)
            {
                var data = item as MapAsteroidBelts;
                if (data == null) continue;
                var parent = celestialDict.TryGetValue(data.OrbitID, out var planet) ? planet.ItemName : SystemName(data.SolarSystemID);
                var row = new DBModels.MapDenormalizes()
                {
                    ID = data.Id,
                    GroupID = 9,
                    SolarSystemID = data.SolarSystemID,
                    TypeID = data.TypeID,
                    Radius = data.Radius,
                    ItemName = $"{parent} - {beltGroupName} {data.OrbitIndex}"
                };
                allDatas.Add(row);
                celestialDict[data.Id] = row;
            }

            if (fileDatas.TryGetValue("mapSecondarySuns", out var mapSecondarySuns))
            {
                foreach (var item in mapSecondarySuns)
                {
                    var data = item as MapSecondarySuns;
                    if (data == null) continue;
                    allDatas.Add(new DBModels.MapDenormalizes()
                    {
                        ID = data.Id,
                        GroupID = 995,
                        SolarSystemID = data.SolarSystemID,
                        TypeID = data.TypeID,
                        ItemName = $"{SystemName(data.SolarSystemID)} - {secondarySunGroupName} {data.Id}"
                    });
                }
            }

            if (fileDatas.TryGetValue("mapStargates", out var mapStargates))
            {
                foreach (var item in mapStargates)
                {
                    var data = item as MapStargates;
                    if (data == null) continue;
                    var destination = data.Destination == null ? "?" : SystemName(data.Destination.SolarSystemID);
                    allDatas.Add(new DBModels.MapDenormalizes()
                    {
                        ID = data.Id,
                        GroupID = 10,
                        SolarSystemID = data.SolarSystemID,
                        TypeID = data.TypeID,
                        ItemName = $"{SystemName(data.SolarSystemID)} - {gateGroupName} ({destination})"
                    });
                }
            }

            if (fileDatas.TryGetValue("npcStations", out var npcStations)
                && fileDatas.TryGetValue("npcCorporations", out var npcCorporations)
                && fileDatas.TryGetValue("stationOperations", out var stationOperations))
            {
                var npcCorporationsDict = npcCorporations.Select(p => p as NpcCorporations).Where(p => p != null).ToDictionary(p => p.Id);
                var stationOperationsDict = stationOperations.Select(p => p as StationOperations).Where(p => p != null).ToDictionary(p => p.Id);
                foreach (var item in npcStations)
                {
                    var data = item as NpcStations;
                    if (data == null) continue;
                    allDatas.Add(new DBModels.MapDenormalizes()
                    {
                        ID = data.Id,
                        GroupID = 15,
                        SolarSystemID = data.SolarSystemID,
                        TypeID = data.TypeID,
                        ItemName = BuildStationName(data, celestialDict, npcCorporationsDict, stationOperationsDict, language)
                    });
                }
            }

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
            var npcCorporationsDict = npcCorporations.Select(p => p as NpcCorporations).Where(p => p != null).ToDictionary(p => p.Id);
            var stationOperationsDict = stationOperations.Select(p => p as StationOperations).Where(p => p != null).ToDictionary(p => p.Id);
            List<DBModels.Stations> datas = new List<DBModels.Stations>();
            foreach (var item in npcStations)
            {
                var data = item as NpcStations;
                if (data == null) continue;
                datas.Add(new DBModels.Stations()
                {
                    Id = data.Id,
                    SolarSystemID = data.SolarSystemID,
                    StationName = BuildStationName(data, mapDenormalizesDict, npcCorporationsDict, stationOperationsDict, language)
                });
            }
            return datas;
        }

        private static string BuildStationName(NpcStations data, Dictionary<int, DBModels.MapDenormalizes> celestialDict, Dictionary<int, NpcCorporations> npcCorporationsDict, Dictionary<int, StationOperations> stationOperationsDict, LanguageEnum language)
        {
            string parent = celestialDict.TryGetValue(data.OrbitID, out var celestial) ? celestial.ItemName : data.SolarSystemID.ToString();
            string corporation = npcCorporationsDict.TryGetValue(data.OwnerID, out var corp) ? (corp.Names?.GetValue(language) ?? data.OwnerID.ToString()) : data.OwnerID.ToString();
            string operation = stationOperationsDict.TryGetValue(data.OperationID, out var op) ? (op.OperationName?.GetValue(language) ?? data.OperationID.ToString()) : data.OperationID.ToString();
            return $"{parent} - {corporation} {operation}";
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
                // typeBonus 的 _key 是船体 TypeID；types 数组的 _key 是提供加成的技能 TypeID。
                if (data.Types != null)
                {
                    foreach (var typeItem in data.Types)
                    {
                        if (typeItem.Bonuses == null) continue;
                        foreach (var bonus in typeItem.Bonuses)
                        {
                            result.Add(new DBModels.TypeBonus()
                            {
                                TypeID = data.Id,
                                SkillTypeID = typeItem.TypeID,
                                Bonus = bonus.Bonus,
                                Importance = bonus.Importance,
                                UnitID = bonus.UnitID,
                                BonusText = bonus.BonusText?.GetValue(language),
                                BonusTextEn = bonus.BonusText?.En
                            });
                        }
                    }
                }
                if (data.RoleBonuses != null)
                {
                    foreach (var bonus in data.RoleBonuses)
                    {
                        result.Add(new DBModels.TypeBonus()
                        {
                            TypeID = data.Id,
                            SkillTypeID = 0,
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
                var data = item as DeserializeModels.TypeDogma;
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
                var data = item as DeserializeModels.PlanetSchematics;
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

        public static List<DBModels.Factions> GetFactions(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("factions", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.Factions(p, language)).ToList();
        }

        public static List<DBModels.Races> GetRaces(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("races", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.Races(p, language)).ToList();
        }

        public static List<DBModels.MetaGroups> GetMetaGroups(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("metaGroups", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.MetaGroups(p, language)).ToList();
        }

        public static List<DBModels.StationServices> GetStationServices(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("stationServices", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.StationServices(p, language)).ToList();
        }

        public static List<DBModels.NpcCorporationDivisions> GetNpcCorporationDivisions(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("npcCorporationDivisions", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.NpcCorporationDivisions(p, language)).ToList();
        }

        public static List<DBModels.Archetypes> GetArchetypes(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("archetypes", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.Archetypes(p, language)).ToList();
        }

        public static List<DBModels.CompressibleTypes> GetCompressibleTypes(Dictionary<string, List<BaseModel>> fileDatas)
        {
            if (!fileDatas.TryGetValue("compressibleTypes", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.CompressibleTypes(p)).ToList();
        }

        public static List<DBModels.TypeLists> GetTypeLists(Dictionary<string, List<BaseModel>> fileDatas)
        {
            if (!fileDatas.TryGetValue("typeLists", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.TypeLists(p)).ToList();
        }

        public static List<DBModels.Masteries> GetMasteries(Dictionary<string, List<BaseModel>> fileDatas)
        {
            if (!fileDatas.TryGetValue("masteries", out var datas))
            {
                return null;
            }
            List<DBModels.Masteries> result = new List<DBModels.Masteries>();
            foreach (var item in datas)
            {
                var data = item as DeserializeModels.Masteries;
                if (data?.Values == null) continue;
                foreach (var level in data.Values)
                {
                    result.Add(new DBModels.Masteries(data.Id, level));
                }
            }
            return result;
        }

        public static List<DBModels.CloneGrades> GetCloneGrades(Dictionary<string, List<BaseModel>> fileDatas)
        {
            if (!fileDatas.TryGetValue("cloneGrades", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.CloneGrades(p)).ToList();
        }

        public static List<DBModels.NpcCharacters> GetNpcCharacters(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("npcCharacters", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.NpcCharacters(p, language)).ToList();
        }

        public static List<DBModels.Landmarks> GetLandmarks(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("landmarks", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.Landmarks(p, language)).ToList();
        }

        public static List<DBModels.MercenaryTacticalOperations> GetMercenaryTacticalOperations(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("mercenaryTacticalOperations", out var datas))
            {
                return null;
            }
            return datas.Select(p => new DBModels.MercenaryTacticalOperations(p, language)).ToList();
        }

        public static List<DBModels.IndustryActivityDefinition> GetIndustryActivities(Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("industryActivities", out var datas)) return null;
            return datas.OfType<IndustryActivities>().Select(p => new DBModels.IndustryActivityDefinition
            {
                Id = p.Id,
                Name = p.Names?.GetValue(language) ?? string.Empty,
                Description = p.Descriptions?.GetValue(language) ?? string.Empty,
            }).ToList();
        }

        public static (List<DBModels.IndustryAssemblyLine>, List<DBModels.IndustryAssemblyLineDetail>) GetIndustryAssemblyLines(
            Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language)
        {
            if (!fileDatas.TryGetValue("industryAssemblyLines", out var datas)) return (null, null);
            var lines = new List<DBModels.IndustryAssemblyLine>();
            var details = new List<DBModels.IndustryAssemblyLineDetail>();
            foreach (var item in datas.OfType<IndustryAssemblyLines>())
            {
                lines.Add(new DBModels.IndustryAssemblyLine
                {
                    Id = item.Id,
                    ActivityID = item.ActivityID,
                    Name = item.Names?.GetValue(language) ?? string.Empty,
                    Description = item.Descriptions?.GetValue(language) ?? string.Empty,
                    BaseCostMultiplier = item.BaseCostMultiplier,
                    BaseMaterialMultiplier = item.BaseMaterialMultiplier,
                    BaseTimeMultiplier = item.BaseTimeMultiplier,
                });
                if (item.DetailsPerCategory != null)
                {
                    foreach (var detail in item.DetailsPerCategory)
                    {
                        if (detail.CategoryID <= 0) continue;
                        details.Add(new DBModels.IndustryAssemblyLineDetail
                        {
                            AssemblyLineID = item.Id,
                            IsCategory = true,
                            TargetID = detail.CategoryID,
                            CostMultiplier = detail.CostMultiplier,
                            MaterialMultiplier = detail.MaterialMultiplier,
                            TimeMultiplier = detail.TimeMultiplier,
                        });
                    }
                }
                if (item.DetailsPerGroup != null)
                {
                    foreach (var detail in item.DetailsPerGroup)
                    {
                        if (detail.GroupID <= 0) continue;
                        details.Add(new DBModels.IndustryAssemblyLineDetail
                        {
                            AssemblyLineID = item.Id,
                            IsCategory = false,
                            TargetID = detail.GroupID,
                            CostMultiplier = detail.CostMultiplier,
                            MaterialMultiplier = detail.MaterialMultiplier,
                            TimeMultiplier = detail.TimeMultiplier,
                        });
                    }
                }
            }
            return (lines, details);
        }

        public static List<DBModels.IndustryInstallationType> GetIndustryInstallationTypes(Dictionary<string, List<BaseModel>> fileDatas)
        {
            if (!fileDatas.TryGetValue("industryInstallationTypes", out var datas)) return null;
            return datas.OfType<IndustryInstallationTypes>().Select(p => new DBModels.IndustryInstallationType
            {
                InstallationTypeID = p.Id,
                AssemblyLineIDs = string.Join(",", (p.AssemblyLines ?? new List<IndustryInstallationAssemblyLine>())
                    .Select(x => x.AssemblyLineID)
                    .Where(x => x > 0)
                    .Distinct()),
            }).ToList();
        }

        public static List<DBModels.IndustryModifierSource> GetIndustryModifierSources(Dictionary<string, List<BaseModel>> fileDatas)
        {
            if (!fileDatas.TryGetValue("industryModifierSources", out var datas)) return null;
            var rows = new List<DBModels.IndustryModifierSource>();
            foreach (var item in datas.OfType<IndustryModifierSources>())
            {
                AddModifier(item.Id, 1, item.Manufacturing);
                AddModifier(item.Id, 3, item.ResearchTime);
                AddModifier(item.Id, 4, item.ResearchMaterial);
                AddModifier(item.Id, 5, item.Copying);
                AddModifier(item.Id, 8, item.Invention);
                AddModifier(item.Id, 9, item.Reaction);
            }
            return rows;

            void AddModifier(int sourceTypeId, int activityId, IndustryModifierActivity activity)
            {
                if (activity == null) return;
                Add(activity.Cost, 1);
                Add(activity.Time, 2);
                Add(activity.Material, 3);

                void Add(List<IndustryModifierAttribute> attrs, int modifierType)
                {
                    if (attrs == null) return;
                    foreach (var attr in attrs)
                    {
                        if (attr.DogmaAttributeID <= 0) continue;
                        rows.Add(new DBModels.IndustryModifierSource
                        {
                            SourceTypeID = sourceTypeId,
                            ActivityID = activityId,
                            ModifierType = modifierType,
                            DogmaAttributeID = attr.DogmaAttributeID,
                            FilterID = attr.FilterID,
                        });
                    }
                }
            }
        }

        public static (List<DBModels.IndustryTargetFilterDefinition>, List<DBModels.IndustryTargetFilterCategory>, List<DBModels.IndustryTargetFilterGroup>) GetIndustryTargetFilters(
            Dictionary<string, List<BaseModel>> fileDatas)
        {
            if (!fileDatas.TryGetValue("industryTargetFilters", out var datas))
                return (null, null, null);
            var filters = new List<DBModels.IndustryTargetFilterDefinition>();
            var categories = new List<DBModels.IndustryTargetFilterCategory>();
            var groups = new List<DBModels.IndustryTargetFilterGroup>();
            foreach (var item in datas.OfType<IndustryTargetFilters>())
            {
                filters.Add(new DBModels.IndustryTargetFilterDefinition
                {
                    Id = item.Id,
                    Name = item.Names?.En ?? string.Empty,
                });
                if (item.CategoryIDs != null)
                {
                    categories.AddRange(item.CategoryIDs.Where(x => x > 0).Distinct()
                        .Select(x => new DBModels.IndustryTargetFilterCategory { FilterID = item.Id, CategoryID = x }));
                }
                if (item.GroupIDs != null)
                {
                    groups.AddRange(item.GroupIDs.Where(x => x > 0).Distinct()
                        .Select(x => new DBModels.IndustryTargetFilterGroup { FilterID = item.Id, GroupID = x }));
                }
            }
            return (filters, categories, groups);
        }

        public static DBModels.SDEInfo GetSDEInfo(string[] sdeFiles)
        {
            if (sdeFiles == null || sdeFiles.Length == 0)
            {
                return null;
            }
            var folder = Path.GetDirectoryName(sdeFiles[0]);
            if (string.IsNullOrEmpty(folder))
            {
                return null;
            }
            var file = Path.Combine(folder, "_sde.jsonl");
            if (!File.Exists(file))
            {
                return null;
            }
            try
            {
                var line = File.ReadLines(file).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
                if (line == null)
                {
                    return null;
                }
                var model = JsonConvert.DeserializeObject<Models.SDEInfo>(line);
                return model == null ? null : new DBModels.SDEInfo(model);
            }
            catch
            {
                return null;
            }
        }
    }
}
