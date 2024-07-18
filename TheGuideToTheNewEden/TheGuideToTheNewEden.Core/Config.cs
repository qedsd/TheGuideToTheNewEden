using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.Core
{
    /// <summary>
    /// 全局配置
    /// </summary>
    public static class Config
    {
        #region 文件路径
        /// <summary>
        /// 主数据库数据库文件路径
        /// </summary>
        public static string DBPath { get; set; }
        /// <summary>
        /// 本地化数据库文件路径
        /// </summary>
        public static string LocalDBPath { get; set; }
        /// <summary>
        /// 是否需要翻译数据库
        /// </summary>
        public static bool NeedLocalization { get; set; }
        /// <summary>
        /// 死亡远征数据库文件路径
        /// </summary>
        public static string DEDDBPath { get; set; }
        /// <summary>
        /// 虫洞数据库文件路径
        /// </summary>
        public static string WormholeDBPath { get; set; }

        /// <summary>
        /// 死亡远征、虫洞、任务、北背景故事等静态数据库
        /// </summary>
        public static string StaticDBPath { get; set; }
        /// <summary>
        /// 缓存数据库路径
        /// 如名字id
        /// </summary>
        public static string CacheDBPath { get; set; }
        /// <summary>
        /// 星系位置更新文件路径
        /// </summary>
        public static string SolarSystemMapPath { get; set; }
        public static string RegionMapPath { get; set; }
        public static string CapitalJumpShipInfoPath { get; set; }
        #endregion
        /// <summary>
        /// 数据库使用语言
        /// </summary>
        public static Enums.Language DBLanguage { get; set; } = Enums.Language.Chinese;
        /// <summary>
        /// API服务器
        /// </summary>
        public static Enums.GameServerType DefaultGameServer = Enums.GameServerType.Tranquility;
        /// <summary>
        /// 授权服务的clientid
        /// </summary>
        public static string ClientId { get; set; }
        /// <summary>
        /// 授权服务的scope
        /// </summary>
        public static string Scope { get; set; }
        public static List<string> Scopes { get; set; }
        /// <summary>
        /// 玩家统计服务器API
        /// </summary>
        public static string PlayerStatusApi { get; set; }
        public static string ESICallback { get; set; }
        public static bool InitDb()
        {
            bool result = true;
            result = DBService.InitMainDb(DBPath) && result;
            //其他必须的数据库
            //result = DBService.InitDEDDb(DEDDBPath) && result;
            //result = DBService.InitWormholeDb(WormholeDBPath) && result;
            result = DBService.InitStaticDb(StaticDBPath) && result;
            result = DBService.InitCacheDb(CacheDBPath) && result;
            //...
            //本地化数据库不是必须的
            DBService.InitLocalDb(LocalDBPath);
            return result;
        }
    }
}
