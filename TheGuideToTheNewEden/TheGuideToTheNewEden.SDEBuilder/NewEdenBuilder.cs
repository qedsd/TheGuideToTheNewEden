using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TheGuideToTheNewEden.SDEBuilder.DeserializeModels;

namespace TheGuideToTheNewEden.SDEBuilder
{
    public static class NewEdenBuilder
    {
        public static async Task StartBuilder(string[] sdeFiles, string folder, LanguageEnum[] languages, DateTime releaseDate, bool getPackagedVolume)
        {
            var fileDatas = await Task.Run(() => Builder.ReadAllFiles(sdeFiles, LanguageEnum.En));
            if (fileDatas.Any())
            {
                await BuilderMain(folder, fileDatas, releaseDate, getPackagedVolume, sdeFiles);
                foreach (var lang in languages)
                {
                    await BuilderLocal(folder, fileDatas, lang, releaseDate);
                }
            }
        }

        private static async Task BuilderMain(string folder, Dictionary<string, List<BaseModel>> fileDatas, DateTime releaseDate, bool getPackagedVolume, string[] sdeFiles)
        {
            string mainFile = Path.Combine(folder, $"main_{releaseDate.ToString("yyyyMMdd")}.db");
            if (File.Exists(mainFile))
            {
                File.Delete(mainFile);
            }
            var db = Builder.CreateDB(mainFile);
            try
            {
                // main 库与单库共用同一条落库管线，避免表集合漂移。
                await Builder.WriteAllTablesAsync(db, fileDatas, LanguageEnum.En, getPackagedVolume, sdeFiles, mainFile);
            }
            finally
            {
                db.Close();
                db.Dispose();
            }
        }

        private static async Task BuilderLocal(string folder, Dictionary<string, List<BaseModel>> fileDatas, LanguageEnum language, DateTime releaseDate)
        {
            string mainFile = Path.Combine(folder, $"{language.ToString().ToLower()}_{releaseDate.ToString("yyyyMMdd")}.db");
            if (File.Exists(mainFile))
            {
                File.Delete(mainFile);
            }
            var db = Builder.CreateDB(mainFile);
            try
            {
                var categories = Builder.GetCategories(fileDatas, language);
                if (categories != null)
                {
                    db.CodeFirst.InitTables(typeof(LocalDBModels.Categories));
                    await db.Insertable(categories.Select(p => new LocalDBModels.Categories(p)).ToList()).ExecuteCommandAsync();
                }

                var groups = Builder.GetGroups(fileDatas, language);
                if (groups != null)
                {
                    db.CodeFirst.InitTables(typeof(LocalDBModels.Groups));
                    await db.Insertable(groups.Select(p => new LocalDBModels.Groups(p)).ToList()).ExecuteCommandAsync();
                }

                var mapRegions = Builder.GetMapRegions(fileDatas, language);
                if (mapRegions != null)
                {
                    db.CodeFirst.InitTables(typeof(LocalDBModels.MapRegions));
                    await db.Insertable(mapRegions.Select(p => new LocalDBModels.MapRegions(p)).ToList()).ExecuteCommandAsync();
                }

                var mapSolarSystems = Builder.GetMapSolarSystems(fileDatas, language);
                if (mapSolarSystems != null)
                {
                    db.CodeFirst.InitTables(typeof(LocalDBModels.MapSolarSystems));
                    await db.Insertable(mapSolarSystems.Select(p => new LocalDBModels.MapSolarSystems(p)).ToList()).ExecuteCommandAsync();
                }

                var marketGroups = Builder.GetMarketGroups(fileDatas, language);
                if (marketGroups != null)
                {
                    db.CodeFirst.InitTables(typeof(LocalDBModels.MarketGroups));
                    await db.Insertable(marketGroups.Select(p => new LocalDBModels.MarketGroups(p)).ToList()).ExecuteCommandAsync();
                }

                var types = await Builder.GetTypes(fileDatas, language, false);
                if (types != null)
                {
                    db.CodeFirst.InitTables(typeof(LocalDBModels.Types));
                    await db.Insertable(types.Select(p => new LocalDBModels.Types(p)).ToList()).ExecuteCommandAsync();
                }

                var mapDenormalizes = Builder.GetMapDenormalizes(fileDatas, language);
                if (mapDenormalizes != null)
                {
                    db.CodeFirst.InitTables(typeof(LocalDBModels.MapDenormalizes));
                    await db.Insertable(mapDenormalizes.Select(p => new LocalDBModels.MapDenormalizes(p)).ToList()).ExecuteCommandAsync();

                    var mapDenormalizesDict = mapDenormalizes.ToDictionary(p => p.ID);
                    var stations = Builder.GetStations(fileDatas, language, mapDenormalizesDict);
                    if (stations != null)
                    {
                        db.CodeFirst.InitTables(typeof(LocalDBModels.Stations));
                        await db.Insertable(stations.Select(p => new LocalDBModels.Stations(p)).ToList()).ExecuteCommandAsync();
                    }
                }

                var mapConstellations = Builder.GetMapConstellations(fileDatas, language);
                if (mapConstellations != null)
                {
                    db.CodeFirst.InitTables(typeof(LocalDBModels.MapConstellations));
                    await db.Insertable(mapConstellations.Select(p => new LocalDBModels.MapConstellations(p)).ToList()).ExecuteCommandAsync();
                }

                var planetSchematics = Builder.GetPlanetSchematics(fileDatas, language);
                if (planetSchematics.Item1 != null)
                {
                    db.CodeFirst.InitTables(typeof(LocalDBModels.PlanetSchematic));
                    await db.Insertable(planetSchematics.Item1.Select(p => new LocalDBModels.PlanetSchematic(p)).ToList()).ExecuteCommandAsync();
                }
            }
            finally
            {
                db.Close();
                db.Dispose();
            }
        }
    }
}
