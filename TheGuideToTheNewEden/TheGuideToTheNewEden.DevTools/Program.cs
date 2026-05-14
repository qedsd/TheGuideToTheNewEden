using TheGuideToTheNewEden.DevTools.Map;
using TheGuideToTheNewEden.DevTools.Translation;

string dbPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "main.db");
string localPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "zh.db");

TheGuideToTheNewEden.Core.Config.DBPath = dbPath;
TheGuideToTheNewEden.Core.Config.LocalDBPath = localPath;
TheGuideToTheNewEden.Core.Config.NeedLocalization = true;
TheGuideToTheNewEden.Core.Config.InitDb();

//SolarSystemMap.CreateSloarSystemMap("SolarSystemMap.json");
//RegionMap.CreateMap("SolarSystemMap.json", "RegionMap.json");
await CreateTranslationVoca.Start("MarketNamese2z.csv", "MarketNamesz2e.csv");
