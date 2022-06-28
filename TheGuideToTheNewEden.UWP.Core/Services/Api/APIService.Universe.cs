using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Enums;

namespace TheGuideToTheNewEden.Core.Services.Api
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
        /// 建筑信息
        /// </summary>
        /// <param name="structureId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static string UniverseStructureInfo(long structureId, string token)=> UniverseStructureInfo(DefaultGameServer,structureId,token);

        /// <summary>
        /// 由ID搜索名字
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
        public static string UniverseName() => UniverseName(DefaultGameServer);
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
        public static string UniverseStructure(string filter) => UniverseStructure(DefaultGameServer,filter);
        /// <summary>
        /// 指定筛选条件建筑
        /// Http.Post
        /// </summary>
        /// <returns></returns>
        public static string UniverseStructureWithMarket(GameServerType server)
        {
            return UniverseStructure(server, "market");
        }
        public static string UniverseStructureWithMarket() => UniverseStructureWithMarket(DefaultGameServer);
    }
}
