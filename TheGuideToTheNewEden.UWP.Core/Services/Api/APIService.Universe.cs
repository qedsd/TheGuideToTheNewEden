using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.UWP.Core.Enums;

namespace TheGuideToTheNewEden.UWP.Core.Services.Api
{
    public static partial class APIService
    {
        /// <summary>
        /// 建筑信息
        /// </summary>
        /// <returns></returns>
        public static string UniverseStructureInfo(GameServerType server, long structureId, string token)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/universe/structures/{structureId}/?datasource=tranquility&token={token}";
            else
                return $"{SerenityUri}/universe/structures/{structureId}/?datasource=serenity&token={token}";
        }

        /// <summary>
        /// 由物品名字搜索ID
        /// Http.Post
        /// </summary>
        /// <returns></returns>
        public static string UniverseName(GameServerType server)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/universe/names/?datasource=tranquility";
            else
                return $"{SerenityUri}/universe/names/?datasource=serenity";
        }

        /// <summary>
        /// 指定筛选条件建筑
        /// Http.Post
        /// </summary>
        /// <returns></returns>
        public static string UniverseStructure(GameServerType server, string filter)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/universe/structures/?datasource=tranquility&filter={filter}";
            else
                return $"{SerenityUri}/universe/structures/?datasource=serenity&filter={filter}";
        }
        /// <summary>
        /// 指定筛选条件建筑
        /// Http.Post
        /// </summary>
        /// <returns></returns>
        public static string UniverseStructureWithMarket(GameServerType server)
        {
            return UniverseStructure(server, "market");
        }
    }
}
