using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Enums;

namespace TheGuideToTheNewEden.Core.Services.Api
{
    public static partial class APIService
    {
        /// <summary>
        /// 由名称搜索
        /// </summary>
        /// <param name="server"></param>
        /// <param name="category"></param>
        /// <param name="name"></param>
        /// <param name="isStrict">是否精确搜索</param>
        /// <returns></returns>
        public static string SearchCategory(GameServerType server, string category, string name, bool isStrict = true)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/search/?categories={category}&datasource=tranquility&language=en-us&search={name}&strict={isStrict}";
            else
                return $"{SerenityUri}/search/?categories={category}&datasource=serenity&language=zh&search={name}&strict={isStrict}";
        }

        /// <summary>
        /// 搜索角色
        /// </summary>
        /// <param name="server"></param>
        /// <param name="name"></param>
        /// <param name="isStrict"></param>
        /// <returns></returns>
        public static string SearchCharacter(GameServerType server, string name, bool isStrict = true)
        {
            return SearchCategory(server, "character",name,isStrict);
        }

        /// <summary>
        /// 搜索军团
        /// </summary>
        /// <param name="server"></param>
        /// <param name="name"></param>
        /// <param name="isStrict"></param>
        /// <returns></returns>
        public static string SearchCorporation(GameServerType server, string name, bool isStrict = true)
        {
            return SearchCategory(server, "corporation", name, isStrict);
        }

        /// <summary>
        /// 搜索联盟
        /// </summary>
        /// <param name="server"></param>
        /// <param name="name"></param>
        /// <param name="isStrict"></param>
        /// <returns></returns>
        public static string SearchAlliance(GameServerType server, string name, bool isStrict = true)
        {
            return SearchCategory(server, "alliance", name, isStrict);
        }
    }
}
