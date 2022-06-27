using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Enums;

namespace TheGuideToTheNewEden.Core.Services.Api
{
    public static partial class APIService
    {
        /// <summary>
        /// 军团忠诚点商店
        /// </summary>
        /// <param name="server"></param>
        /// <param name="corpId"></param>
        /// <returns></returns>
        public static string LoyaltyStores(GameServerType server, int corpId)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/loyalty/stores/{corpId}/offers/?datasource=tranquility";
            else
                return $"{SerenityUri}/loyalty/stores/{corpId}/offers/?datasource=serenity";
        }
    }
}
