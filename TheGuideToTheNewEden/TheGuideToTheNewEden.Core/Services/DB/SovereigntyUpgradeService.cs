using System;
using System.Collections.Generic;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public static class SovereigntyUpgradeService
    {
        /// <summary>查询全部设施升级定义；旧库没有该表时补建空表，返回空集合。</summary>
        public static List<SovereigntyUpgrade> QueryAll()
        {
            try
            {
                DBService.MainDb.CodeFirst.InitTables(typeof(SovereigntyUpgrade));
                return DBService.MainDb.Queryable<SovereigntyUpgrade>()
                    .OrderBy(p => p.TypeID)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return new List<SovereigntyUpgrade>();
            }
        }
    }
}
