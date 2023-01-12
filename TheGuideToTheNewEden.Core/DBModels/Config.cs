using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    public static class Config
    {
        private static string MainDBPath = System.IO.Path.Combine(Core.Config.DBPath, "main.db");
        private static string MainDBConnectionString = @"DataSource=" + MainDBPath;
        private static ConnectionConfig mainDBConnectionConfig;
        public static ConnectionConfig MainDBConnectionConfig
        {
            get
            {
                if (mainDBConnectionConfig == null)
                {
                    mainDBConnectionConfig = new ConnectionConfig()
                    {
                        ConnectionString = MainDBConnectionString,
                        DbType = DbType.Sqlite,
                        IsAutoCloseConnection = true
                    };
                }
                return mainDBConnectionConfig;
            }
        }

        private static string ZHDBPath = System.IO.Path.Combine(Core.Config.DBPath, "zh.db");
        private static string ZHDBConnectionString = @"DataSource=" + ZHDBPath;
        private static ConnectionConfig zhDBConnectionConfig;
        public static ConnectionConfig ZHDBConnectionConfig
        {
            get
            {
                if (zhDBConnectionConfig == null)
                {
                    zhDBConnectionConfig = new ConnectionConfig()
                    {
                        ConnectionString = ZHDBConnectionString,
                        DbType = DbType.Sqlite,
                        IsAutoCloseConnection = true
                    };
                }
                return zhDBConnectionConfig;
            }
        }
    }
}
