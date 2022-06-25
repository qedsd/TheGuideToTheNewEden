using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.UWP.Core.Enums;

namespace TheGuideToTheNewEden.UWP.Core.Services.Api
{
    public static partial class APIService
    {
        /// <summary>
        /// 授权token
        /// </summary>
        /// <returns></returns>
        public static string OauthToken(GameServerType server = GameServerType.Tranquility)
        {
            if (server == GameServerType.Tranquility)
                return "https://login.eveonline.com/v2/oauth/token";
            else
                return "https://login.eveonline.com/v2/oauth/token";
        }

        /// <summary>
        /// 检验token
        /// </summary>
        /// <returns></returns>
        public static string VerifyToken( string client_id, string token, GameServerType server = GameServerType.Tranquility)
        {
            if (server == GameServerType.Tranquility)
                return $"https://esi.evetech.net/verify/?user_agent={client_id}&datasource=tranquility&token={token}";
            else
                return $"https://esi.evepc.163.com/verify/?user_agent={client_id}&datasource=tranquility&token={token}";
        }
    }
}
