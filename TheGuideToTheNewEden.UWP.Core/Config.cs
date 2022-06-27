using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core
{
    /// <summary>
    /// 全局配置
    /// </summary>
    public static class Config
    {
        /// <summary>
        /// 数据库所在文件夹
        /// </summary>
        public static string DBPath { get; set; } = string.Empty;
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
    }
}
