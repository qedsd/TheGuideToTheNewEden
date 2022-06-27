using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public class InvTypeService: DBService
    {
        public static async Task<InvType> QueryTypeAsync(int id)
        {
            SqlSugarClient db = new SqlSugarClient(DBModels.Config.MainDBConnectionConfig);
            var type = await db.Queryable<InvType>().FirstAsync(p => p.TypeID == id);
            if (DBLanguage == Enums.Language.Chinese)
            {
                await ZHDBService.TranInvTypeAsync(type);
            }
            return type;
        }

        public static async Task<List<InvType>> QueryTypesAsync(List<int> ids)
        {
            return await Task.Run(async() =>
            {
                SqlSugarClient db = new SqlSugarClient(DBModels.Config.MainDBConnectionConfig);
                var types = db.Queryable<InvType>().Where(p => ids.Contains(p.TypeID)).ToList();
                if (DBLanguage == Enums.Language.Chinese)
                {
                    await ZHDBService.TranInvTypesAsync(types);
                }
                return types;
            });
        }

        public static async Task<List<InvType>> QueryTypesInGroupAsync(int groupId)
        {
            return await Task.Run(async () =>
            {
                SqlSugarClient db = new SqlSugarClient(DBModels.Config.MainDBConnectionConfig);
                var types = db.Queryable<InvType>().Where(p => p.GroupID == groupId).ToList();
                if (DBLanguage == Enums.Language.Chinese)
                {
                    await ZHDBService.TranInvTypesAsync(types);
                }
                return types;
            });
        }
    }
}
