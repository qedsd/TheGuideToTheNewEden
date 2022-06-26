using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.UWP.Core.DBModels;

namespace TheGuideToTheNewEden.UWP.Core.Services.DB
{
    public class InvTypeService
    {
        private static Enums.Language DBLanguage { get => DBService.Language; }

        public static async Task<InvType> QueryTypeAsync(int id)
        {
            SqlSugarClient db = new SqlSugarClient(Config.ZHDBConnectionConfig);
            var type = await db.Queryable<InvType>().FirstAsync(p => p.TypeID == id);
            if (DBLanguage == Enums.Language.Chinese)
            {
                await ZHDBService.TranInvType(type);
            }
            return type;
        }

        public static async Task<List<InvType>> QueryTypesAsync(List<int> ids)
        {
            return await Task.Run(() =>
            {
                SqlSugarClient db = new SqlSugarClient(Config.ZHDBConnectionConfig);
                return db.Queryable<InvType>().Where(p => ids.Contains(p.TypeID)).ToList();
            });
        }
    }
}
