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
            CoreConfig.LocalDbPath = LocalDbSelectorService.Value;
            CoreConfig.DefaultGameServer = GameServerSelectorService.Value;
            CoreConfig.PlayerStatusApi = PlayerStatusService.Value;
            CoreConfig.InitDb();
        }
    }
}
