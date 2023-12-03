using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Services.Api
{
    public static partial class APIService
    {
        private static string TranquilityUri = "https://esi.evetech.net/latest";
        private static string SerenityUri = "https://ali-esi.evepc.163.com/latest";

        public static Enums.GameServerType DefaultGameServer { get => Config.DefaultGameServer; }
    }
}
