using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public class DBService
    {
        public static Enums.Language DBLanguage { get => Config.DBLanguage; }
        /// <summary>
        /// 主数据库
        /// </summary>
        internal static SqlSugarScope MainDb;
        /// <summary>
        /// 本地化数据库
        /// </summary>
        internal static SqlSugarScope LocalDb;
        /// <summary>
        /// DED数据库
        /// </summary>
        internal static SqlSugarScope DEDDb;

        internal static bool NeedLocalization => Config.NeedLocalization;

        internal static bool ValidFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }
            if(!System.IO.File.Exists(path))
            {
                return false;
            }
            return true;
        }
        internal static bool InitLocalDb(string path)
        {
            if (!ValidFile(path))
            {
                return false;
            }
            try
            {
                LocalDb = new SqlSugarScope(new ConnectionConfig()
                {
                    ConnectionString = $"DataSource={path}",
                    DbType = DbType.Sqlite,
                    IsAutoCloseConnection = true,
                    ConfigId = Guid.NewGuid(),
                    ConfigureExternalServices = new ConfigureExternalServices
                    {
                        EntityService = (c, p) =>
                        {
                            if (c.PropertyType.IsGenericType && c.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                            {
                                p.IsNullable = true;
                            }
                        }
                    },
                    MoreSettings = new ConnMoreSettings()
                    {
                        IsAutoRemoveDataCache = true
                    }
                });
                LocalDb.DbMaintenance.CreateDatabase();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        internal static bool InitMainDb(string path)
        {
            if(!ValidFile(path))
            {
                return false;
            }
            try
            {
                MainDb = new SqlSugarScope(new ConnectionConfig()
                {
                    ConnectionString = $"DataSource={path}",
                    DbType = DbType.Sqlite,
                    IsAutoCloseConnection = true,
                    ConfigId = Guid.NewGuid(),
                    ConfigureExternalServices = new ConfigureExternalServices
                    {
                        EntityService = (c, p) =>
                        {
                            if (c.PropertyType.IsGenericType && c.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                            {
                                p.IsNullable = true;
                            }
                        }
                    },
                    MoreSettings = new ConnMoreSettings()
                    {
                        IsAutoRemoveDataCache = true
                    }
                });
                MainDb.DbMaintenance.CreateDatabase();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        internal static bool InitDEDDb(string path)
        {
            if (!ValidFile(path))
            {
                return false;
            }
            try
            {
                DEDDb = new SqlSugarScope(new ConnectionConfig()
                {
                    ConnectionString = $"DataSource={path}",
                    DbType = DbType.Sqlite,
                    IsAutoCloseConnection = true,
                    ConfigId = Guid.NewGuid(),
                    ConfigureExternalServices = new ConfigureExternalServices
                    {
                        EntityService = (c, p) =>
                        {
                            if (c.PropertyType.IsGenericType && c.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                            {
                                p.IsNullable = true;
                            }
                        }
                    },
                    MoreSettings = new ConnMoreSettings()
                    {
                        IsAutoRemoveDataCache = true
                    }
                });
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
