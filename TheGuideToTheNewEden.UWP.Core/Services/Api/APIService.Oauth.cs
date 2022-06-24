using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.UWP.Core.Enums;

namespace TheGuideToTheNewEden.UWP.Core.Services.Api
{
    public static partial class APIService
    {
        /// <summary>
        /// 检验token
        /// </summary>
        /// <returns></returns>
        public static string OauthToken(GameServerType server = GameServerType.Tranquility)
        {
            if (server == GameServerType.Tranquility)
                return "https://login.eveonline.com/v2/oauth/token";
            else
                return "https://login.eveonline.com/v2/oauth/token";
        }
    }
}
