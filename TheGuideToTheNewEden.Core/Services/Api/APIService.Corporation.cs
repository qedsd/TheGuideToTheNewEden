using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Enums;

namespace TheGuideToTheNewEden.Core.Services.Api
{
    public static partial class APIService
    {
        /// <summary>
        /// 军团基本信息
        /// </summary>
        /// <param name="server"></param>
        /// <param name="coporationId"></param>
        /// <returns></returns>
        public static string CorporationInfo(GameServerType server, int coporationId)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/corporations/{coporationId}/?datasource=tranquility";
            else
                return $"{SerenityUri}/corporations/{coporationId}/?datasource=serenity";
        }
        /// <summary>
        /// 军团基本信息
        /// </summary>
        /// <param name="coporationId"></param>
        /// <returns></returns>
        public static string CorporationInfo(int coporationId) => CorporationInfo(DefaultGameServer,coporationId);
    }
}
