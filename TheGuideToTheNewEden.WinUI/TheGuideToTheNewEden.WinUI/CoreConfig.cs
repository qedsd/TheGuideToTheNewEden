using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI
{
    /// <summary>
    /// 配置core
    /// 直接set即可
    /// </summary>
    public static class CoreConfig
    {
        public static string MainDbPath { set=>Core.Config.DBPath = value; }
        public static string LocalDbPath { set => Core.Config.LocalDBPath = value; }
        public static Core.Enums.Language DBLanguage { set => Core.Config.DBLanguage = value; }
        public static Core.Enums.GameServerType DefaultGameServer{set=>Core.Config.DefaultGameServer = value; }
        public static string ClientId {set=>Core.Config.ClientId = value; }
        public static string Scope {set=>Core.Config.Scope = value; }
        public static string PlayerStatusApi { set=>Core.Config.PlayerStatusApi = value; }
        public static bool InitDb()
        {
            return Core.Config.InitDb();
        }
    }
}
