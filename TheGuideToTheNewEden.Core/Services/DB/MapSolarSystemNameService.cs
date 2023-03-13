using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    /// <summary>
    /// 频道预警专用星系名匹配
    /// </summary>
    public class MapSolarSystemNameService
    {
        private SqlSugarClient CreateDb(string path)
        {
            return new SqlSugarClient(new ConnectionConfig()
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
        }
        private static MapSolarSystemNameService current;
        private static MapSolarSystemNameService Current
        {
            get
            {
                if(current == null)
                {
                    current = new MapSolarSystemNameService();
                }
                return current;
            }
        }
        /// <summary>
        /// 缓存结果
        /// 节省SqlSugarClient资源开销
        /// </summary>
        private readonly Dictionary<string, List<MapSolarSystemBase>> Cache = new Dictionary<string, List<MapSolarSystemBase>>();
        public static async Task<List<MapSolarSystemBase>> QueryAllAsync(string dbPath)
        {
            if (Current.Cache.TryGetValue(dbPath, out var cacheResult))
            {
                return cacheResult;
            }
            else
            {
                ISqlSugarClient db;
                if (dbPath == Config.DBPath)
                {
                    db = DBService.MainDb;
                }
                else if (dbPath == Config.LocalDBPath)
                {
                    db = DBService.LocalDb;
                }
                else
                {
                    db = Current.CreateDb(dbPath);
                }
                if (db != null)
                {
                    //ID大于31000000的为虫洞空间之类的
                    List<MapSolarSystemBase> result = await db.Queryable<MapSolarSystemBase>().Where(p=>p.SolarSystemID < 31000000).ToListAsync();
                    if (result.NotNullOrEmpty())
                    {
                        Current.Cache.Add(dbPath, result);
                    }
                    return result;
                }
                else
                {
                    return null;
                }
            }
        }
        /// <summary>
        /// 清空缓存
        /// </summary>
        public static void ClearCache()
        {
            current?.Cache.Clear();
        }
    }
}
