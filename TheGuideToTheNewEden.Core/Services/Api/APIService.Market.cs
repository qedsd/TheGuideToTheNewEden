using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Enums;

namespace TheGuideToTheNewEden.Core.Services.Api
{
    public static partial class APIService
    {
        /// <summary>
        /// 星域全部市场订单
        /// </summary>
        /// <param name="server"></param>
        /// <param name=" allianceId"></param>
        /// <returns></returns>
        public static string MarketRegionOrders(GameServerType server, int regionId, int page)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/markets/{regionId}/orders/?datasource=tranquility&page={page}";
            else
                return $"{SerenityUri}/markets/{regionId}/orders/?datasource=serenity&page={page}";
        }

        /// <summary>
        /// 星域某物品市场订单
        /// </summary>
        /// <param name="server"></param>
        /// <param name=" allianceId"></param>
        /// <returns></returns>
        public static string MarketRegionTypeOrders(GameServerType server, int regionId, int typeId,int page)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/markets/{regionId}/orders/?datasource=tranquility&type_id={typeId}&page={page}";
            else
                return $"{SerenityUri}/markets/{regionId}/orders/?datasource=serenity&type_id={typeId}&page={page}";
        }

        /// <summary>
        /// 建筑订单
        /// </summary>
        /// <param name="server"></param>
        /// <param name=" allianceId"></param>
        /// <returns></returns>
        public static string MarketStructureOrders(GameServerType server, int structureID, int page,string token)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/markets/structures/{structureID}/?datasource=tranquility&token={token}&page={page}";
            else
                return $"{SerenityUri}/markets/structures/{structureID}/?datasource=serenity&token={token}&page={page}";
        }

        /// <summary>
        /// 指定星域指定物品历史订单
        /// </summary>
        /// <param name="server"></param>
        /// <param name=" allianceId"></param>
        /// <returns></returns>
        public static string MarketRegionHistory(GameServerType server, int regionId, int typeId)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/markets/{regionId}/history/?datasource=tranquility&type_id={typeId}";
            else
                return $"{SerenityUri}/markets/{regionId}/history/?datasource=serenity&type_id={typeId}";
        }


    }
}
