using TheGuideToTheNewEden.DevTools.Map;

string dbPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "main.db");
TheGuideToTheNewEden.Core.Config.DBPath = dbPath;
TheGuideToTheNewEden.Core.Config.InitDb();

SolarSystemMap.CreateSloarSystemMap("SolarSystemMap.json");
RegionMap.CreateMap("SolarSystemMap.json", "RegionMap.json");
