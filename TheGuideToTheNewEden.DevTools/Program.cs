using TheGuideToTheNewEden.DevTools.Map;

string dbPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Db", "main.db");
TheGuideToTheNewEden.Core.Config.DBPath = dbPath;
TheGuideToTheNewEden.Core.Config.InitDb();

SolarSystemMap.CreateSloarSystemMap("map.json");
