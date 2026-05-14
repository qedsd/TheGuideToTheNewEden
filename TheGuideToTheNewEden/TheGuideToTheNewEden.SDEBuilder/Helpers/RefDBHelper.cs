using System;
using System.Collections.Generic;
using System.Text;
using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.Helpers
{
    [SugarTable("invTypes")]
    public class InvType
    {
        public int TypeID { get; set; }
        public double Volume { get; set; }
        public double PackagedVolume { get; set; }
    }

    /// <summary>
    /// 老SDE版本数据库
    /// 用于补全新SDE缺失的数据
    /// </summary>
    public static class RefDBHelper
    {
        private static SqlSugarScope _db;
        private static SqlSugarScope CreateDb(string path)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                return null;
            }
            try
            {
                return new SqlSugarScope(new ConnectionConfig()
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
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static void Init()
        {
            _db = CreateDb("Resources\\ref.db");
        }

        public static InvType QueryType(int id)
        {
            return _db.Queryable<InvType>().First(p => p.TypeID == id);
        }
        public static List<InvType> QueryType()
        {
            return _db.Queryable<InvType>().ToList();
        }

        public static bool AddType(int id, double volume, double packagedVolume)
        {
            return AddType(new InvType() { TypeID = id, Volume = volume, PackagedVolume = packagedVolume });
        }
        public static bool AddType(InvType invType)
        {
            return _db.Insertable(invType).ExecuteCommand() > 0;
        }
        public static bool AddType(List<InvType> invTypes)
        {
            return _db.Insertable(invTypes).ExecuteCommand() > 0;
        }
    }
}
