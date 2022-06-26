using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.UWP.Core.DBModels
{
    public static class Config
    {
        private static string MainDBPath = "db.sqlite";
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

        private static string ZHDBPath = "zh.sqlite";
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
