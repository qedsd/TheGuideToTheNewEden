using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Enums;

namespace TheGuideToTheNewEden.Core.Services.Api
{
    public static partial class APIService
    {
        /// <summary>
        /// 主权增强列表
        /// </summary>
        /// <returns></returns>
        public static string SovereigntyCampaigns(GameServerType server)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/sovereignty/campaigns/?datasource=tranquility";
            else
                return $"{SerenityUri}/sovereignty/campaigns/?datasource=serenity";
        }
    }
}
