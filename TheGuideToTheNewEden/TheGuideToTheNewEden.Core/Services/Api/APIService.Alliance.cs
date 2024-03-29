﻿using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Enums;

namespace TheGuideToTheNewEden.Core.Services.Api
{
    public static partial class APIService
    {
        /// <summary>
        /// 联盟基本信息
        /// </summary>
        /// <param name="server"></param>
        /// <param name=" allianceId"></param>
        /// <returns></returns>
        public static string AllianceInfo(GameServerType server, int allianceId)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/alliances/{allianceId}/?datasource=tranquility";
            else
                return $"{SerenityUri}/alliances/{allianceId}/?datasource=serenity";
        }
        public static string AllianceInfo(int allianceId) => AllianceInfo(DefaultGameServer,allianceId);
    }
}
