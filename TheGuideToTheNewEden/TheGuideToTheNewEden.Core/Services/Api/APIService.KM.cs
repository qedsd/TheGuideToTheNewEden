using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Enums;

namespace TheGuideToTheNewEden.Core.Services.Api
{
    public static partial class APIService
    {
        /// <summary>
        /// km详情
        /// </summary>
        /// <returns></returns>
        public static string KillmailDetail(GameServerType server, int id, string hash)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/killmails/{id}/{hash}?datasource=tranquility";
            else
                return $"{SerenityUri}/killmails/{id}/{hash}?datasource=serenity";
        }
    }
}
