using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.ServerLogger.DbModels;
using TheGuideToTheNewEden.ServerLogger.Models;

namespace TheGuideToTheNewEden.ServerLogger
{
    internal static class DbService
    {
        private static readonly string PGConnectionString = "PORT=5432;DATABASE=NewEdenServerLogger;HOST=localhost;PASSWORD=5233;USER ID=postgres";
        public static SqlSugarScope DB;

        public static void Init()
        {
            DB = new SqlSugarScope(new ConnectionConfig()
            {
                ConnectionString = PGConnectionString,
                DbType = DbType.PostgreSQL,
                IsAutoCloseConnection = true
            });
            CodeFirst();
        }
        private static void CodeFirst()
        {
            try
            {
                DB.DbMaintenance.CreateDatabase();
                DB.CodeFirst.InitTables(typeof(SerenityServerStatus));
                DB.CodeFirst.InitTables(typeof(SerenityTiebaInfo));
                DB.CodeFirst.InitTables(typeof(TranquilityServerStatus));
                DB.CodeFirst.InitTables(typeof(TranquilityTiebaInfo));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public static void Insert(SerenityServerStatus serverStatus)
        {
            DB.Insertable(serverStatus).ExecuteCommand();
        }
        public static void Insert(TranquilityServerStatus serverStatus)
        {
            DB.Insertable(serverStatus).ExecuteCommand();
            var rs = DB.Queryable<TranquilityServerStatus>().ToList();
        }
        public static void Insert(SerenityTiebaInfo tiebaInfo)
        {
            DB.Insertable(tiebaInfo).ExecuteCommand();
        }
        public static void Insert(TranquilityTiebaInfo tiebaInfo)
        {
            DB.Insertable(tiebaInfo).ExecuteCommand();
        }
    }
}
