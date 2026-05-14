using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.WinUI.Services.Settings;

namespace TheGuideToTheNewEden.WinUI.Services
{
    internal static class ActivationService
    {
        public static void Init()
        {
            SettingService.Load();
            CoreConfig.MainDbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Database", "main.db");
            CoreConfig.StaticDbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Database", "static.db");
            CoreConfig.CacheDbPath = System.IO.Path.Combine(App.DataPath, "Configs", "cache.db");
            CoreConfig.NeedLocalization = DBLocalizationSettingService.Value;
            CoreConfig.LocalDbPath = LocalDbSelectorService.Value;
            CoreConfig.DefaultGameServer = GameServerSelectorService.Value;
            CoreConfig.PlayerStatusApi = PlayerStatusService.Value;
            CoreConfig.RegionMapPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Configs", "RegionMap.json");
            CoreConfig.CapitalJumpShipInfoPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "Configs", "CapitalJumpShipInfo.json");
            CoreConfig.AppDataPath = App.DataPath;
            CoreConfig.InitDb();
            Services.CharacterService.RegisterLicense(GetESILicense());
            CharacterService.Init();
            StructureService.Init();
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(GetSyncfusionLicense());
            ZKB.NET.Config.UserAgent = "TheGuideToTheNewEden";
            YDTranslationService.Init(GetYDTranslationLicense());
            Core.Helpers.GithubHelper.RegisterLicense(GetGithubLicense());
        }
        private static string GetSyncfusionLicense()
        {
            string file = System.IO.Path.Combine(App.DataPath, "Configs", "SyncfusionLicense.txt");
            if(System.IO.File.Exists(file))
            {
                return System.IO.File.ReadAllText(file);
            }
            else
            {
                //TODO:release
                return "";
            }
        }
        private static string[] GetYDTranslationLicense()
        {
            string file = System.IO.Path.Combine(App.DataPath, "Configs", "YoudaoLicense.txt");
            if (System.IO.File.Exists(file))
            {
                return System.IO.File.ReadAllLines(file);
            }
            else
            {
                //TODO:release
                return ["", "", ""];
            }
        }
        private static string[] GetESILicense()
        {
            string file = System.IO.Path.Combine(App.DataPath, "Configs", "ESILicense.txt");
            if (System.IO.File.Exists(file))
            {
                return System.IO.File.ReadAllLines(file);
            }
            else
            {
                //TODO:release
                return ["", ""];
            }
        }
        private static string GetGithubLicense()
        {
            string file = System.IO.Path.Combine(App.DataPath, "Configs", "GithubLicense.txt");
            if (System.IO.File.Exists(file))
            {
                return System.IO.File.ReadAllText(file);
            }
            else
            {
                //TODO:release
                return null;
            }
        }
    }
}
