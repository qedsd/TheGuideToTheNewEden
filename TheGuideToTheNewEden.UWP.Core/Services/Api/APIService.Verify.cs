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
        public static string CorporationInfo(GameServerType server, string client_id, string token)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/verify/?user_agent={client_id}/datasource=tranquility&token={token}";
            else
                return $"{SerenityUri}/verify/?user_agent={client_id}/datasource=serenity&token={token}";
        }
    }
}
