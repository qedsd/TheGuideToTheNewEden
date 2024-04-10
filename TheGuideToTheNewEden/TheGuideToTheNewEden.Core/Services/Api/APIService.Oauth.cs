using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Enums;

namespace TheGuideToTheNewEden.Core.Services.Api
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

        public static string VerifyToken(string client_id, string token) => VerifyToken(client_id, token, DefaultGameServer);
        /// <summary>
        /// 检验token
        /// </summary>
        /// <returns></returns>
        public static string VerifyToken( string client_id, string token, GameServerType server = GameServerType.Tranquility)
        {
            return $"https://{APIService.GetSSOUrl(server)}/verify/?user_agent={client_id}&datasource={server.ToString().ToLower()}&token={token}";
        }
    }
}
