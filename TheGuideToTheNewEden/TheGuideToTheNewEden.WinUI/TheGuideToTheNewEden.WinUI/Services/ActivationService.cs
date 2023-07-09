using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Services.Settings;

namespace TheGuideToTheNewEden.WinUI.Services
{
    internal class ActivationService
    {
        public static void Init()
        {
            SettingService.Load();
            CoreConfig.MainDbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Database", "main.db");
            CoreConfig.DEDDbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Database", "ded.db");
            CoreConfig.NeedLocalization = DBLocalizationSettingService.Value;
            CoreConfig.LocalDbPath = LocalDbSelectorService.Value;
            CoreConfig.DefaultGameServer = GameServerSelectorService.Value;
            CoreConfig.PlayerStatusApi = PlayerStatusService.Value;
            CoreConfig.SolarSystemMapPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Configs", "SolarSystemMap.json");
            CoreConfig.InitDb();
            CharacterService.Init();
            StructureService.Init();
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(GetSyncfusionLicense());
        }
        private static string GetSyncfusionLicense()
        {
            string file = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "SyncfusionLicense.txt");
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
    }
}
