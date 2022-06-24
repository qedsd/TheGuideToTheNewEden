using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.UWP.Core.Enums;

namespace TheGuideToTheNewEden.UWP.Core.Services.Api
{
    public static partial class APIService
    {
        /// <summary>
        /// 指定星域全部类型公开合同列表
        /// </summary>
        /// <param name="server"></param>
        /// <param name=" allianceId"></param>
        /// <returns></returns>
        public static string PublicContract(GameServerType server, int regionId, int page)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/contracts/public/{regionId}/?datasource=tranquility&page={page}";
            else
                return $"{SerenityUri}/contracts/public/{regionId}/?datasource=serenity&page={page}";
        }

        /// <summary>
        /// 拍卖合同竞价信息
        /// </summary>
        /// <param name="server"></param>
        /// <param name=" allianceId"></param>
        /// <returns></returns>
        public static string PublicContractBids(GameServerType server, int contractId, int page)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/contracts/public/bids/{contractId}/?datasource=tranquility&page={page}";
            else
                return $"{SerenityUri}/contracts/public/bids/{contractId}/?datasource=serenity&page={page}";
        }


        /// <summary>
        /// 公开合同物品详情
        /// </summary>
        /// <param name="server"></param>
        /// <param name=" allianceId"></param>
        /// <returns></returns>
        public static string PublicContractItems(GameServerType server, int contractId, int page)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/contracts/public/items/{contractId}/?datasource=tranquility&page={page}";
            else
                return $"{SerenityUri}/contracts/public/items/{contractId}/?datasource=serenity&page={page}";
        }


    }
}
