using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;

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
        /// 缓存数据库
        /// </summary>
        internal static SqlSugarScope CacheDb;


        /// <summary>
        /// DED数据库
        /// </summary>
        internal static SqlSugarScope DEDDb;
        /// <summary>
        /// 虫洞数据库
        /// </summary>
        internal static SqlSugarScope WormholeDb;
        /// <summary>
        /// 死亡远征、虫洞、任务、北背景故事等静态数据库
        /// </summary>
        internal static SqlSugarScope StaticDb;

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

        internal static bool InitWormholeDb(string path)
        {
            if (!ValidFile(path))
            {
                return false;
            }
            try
            {
                WormholeDb = new SqlSugarScope(new ConnectionConfig()
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
                WormholeDb.DbMaintenance.CreateDatabase();
                WormholeDb.CodeFirst.InitTables(typeof(WormholePortal));
                WormholeDb.CodeFirst.InitTables(typeof(Wormhole));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool InitStaticDb(string path)
        {
            if (!ValidFile(path))
            {
                return false;
            }
            try
            {
                StaticDb = new SqlSugarScope(new ConnectionConfig()
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

        internal static bool InitCacheDb(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }
            try
            {
                CacheDb = new SqlSugarScope(new ConnectionConfig()
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
                if (!File.Exists(path))//不存在数据库自动创建
                {
                    CacheDb.DbMaintenance.CreateDatabase();
                    CacheDb.CodeFirst.InitTables(typeof(DBModels.IdName));
                    //其他需要自动创建的表...
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
