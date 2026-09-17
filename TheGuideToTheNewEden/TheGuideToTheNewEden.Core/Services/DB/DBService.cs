using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public class DBService
    {
        public static Enums.Language DBLanguage { get => Config.DBLanguage; }

        // =====================================================================
        // 数据库句柄：每线程一个 SqlSugarClient
        //
        // 原实现是 6 个 SqlSugarScope 静态字段。SqlSugarScope 的客户端按 AsyncLocal
        // 上下文共享——同一上下文里并行跑的 Task.Run / 线程池线程会共用同一个
        // SqliteConnection，并发查询（图标、击杀流富化、翻译、星图、总览页……）时会炸出
        // Microsoft.Data.Sqlite 的 Close() / SqliteDataReader.Dispose NRE
        // （阶段 63 实机多次：QueryParentId / QueryType 等，"功能正常但时不时崩"）。
        //
        // 改为 ThreadLocal<SqlugarClient>：每个线程各自一个客户端、各自一条连接，
        // IsAutoCloseConnection=true 保证每次操作后连接即归还连接池，跨线程互不干扰。
        // 事务语义不变：本项目没有任何 UseTran/BeginTran，全部是单条操作（已全仓核对）。
        // =====================================================================

        private sealed class ThreadDb
        {
            private string _path;
            private long _version;
            private readonly ThreadLocal<(long Version, SqlSugarClient Client)> _clients =
                new ThreadLocal<(long, SqlSugarClient)>(() => default);

            /// <summary>设置数据库文件路径（初始化时调用一次；重复调用会使各线程缓存失效）。</summary>
            public void SetPath(string path)
            {
                Interlocked.Exchange(ref _path, path);
                Interlocked.Increment(ref _version);
            }

            /// <summary>当前线程的客户端；未初始化时返回 null（保持原"字段为 null"的判空语义）。</summary>
            public SqlSugarClient Db
            {
                get
                {
                    var path = _path;
                    if (string.IsNullOrEmpty(path))
                    {
                        return null;
                    }

                    var (version, client) = _clients.Value;
                    if (client != null && version == _version)
                    {
                        return client;
                    }

                    client = new SqlSugarClient(CreateConfig(path));
                    _clients.Value = (_version, client);
                    return client;
                }
            }
        }

        private static ConnectionConfig CreateConfig(string path)
        {
            return new ConnectionConfig()
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
            };
        }

        private static readonly ThreadDb MainDbHandle = new ThreadDb();
        private static readonly ThreadDb LocalDbHandle = new ThreadDb();
        private static readonly ThreadDb CacheDbHandle = new ThreadDb();
        private static readonly ThreadDb DEDDbHandle = new ThreadDb();
        private static readonly ThreadDb WormholeDbHandle = new ThreadDb();
        private static readonly ThreadDb StaticDbHandle = new ThreadDb();

        /// <summary>主数据库（当前线程的客户端）。</summary>
        internal static SqlSugarClient MainDb => MainDbHandle.Db;

        /// <summary>本地化数据库（当前线程的客户端）。</summary>
        internal static SqlSugarClient LocalDb => LocalDbHandle.Db;

        /// <summary>缓存数据库（当前线程的客户端）。</summary>
        internal static SqlSugarClient CacheDb => CacheDbHandle.Db;

        /// <summary>DED数据库（当前线程的客户端）。</summary>
        internal static SqlSugarClient DEDDb => DEDDbHandle.Db;

        /// <summary>虫洞数据库（当前线程的客户端）。</summary>
        internal static SqlSugarClient WormholeDb => WormholeDbHandle.Db;

        /// <summary>死亡远征、虫洞、任务、北背景故事等静态数据库（当前线程的客户端）。</summary>
        internal static SqlSugarClient StaticDb => StaticDbHandle.Db;

        internal static bool NeedLocalization => Config.NeedLocalization;

        /// <summary>
        /// 主数据库是否已成功载入。未载入时任何按库查询都不可用
        /// （<see cref="MainDb"/> 返回 null，直接查询会抛 NullReferenceException）。
        /// </summary>
        public static bool MainDbReady => MainDb != null;

        /// <summary>
        /// 本地化数据库（zh.db 等）是否已成功载入。
        /// 未启用本地化或本地化数据库文件缺失时为 false。
        /// </summary>
        public static bool LocalDbReady => LocalDb != null;

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
                LocalDbHandle.SetPath(path);
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
                MainDbHandle.SetPath(path);
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
                DEDDbHandle.SetPath(path);
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
                WormholeDbHandle.SetPath(path);
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
                StaticDbHandle.SetPath(path);
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
                CacheDbHandle.SetPath(path);
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
