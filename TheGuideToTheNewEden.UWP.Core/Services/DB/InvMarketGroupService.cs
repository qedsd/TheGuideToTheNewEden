using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public class InvMarketGroupService : DBService
    {
        public static async Task<InvMarketGroup> QueryAsync(int id)
        {
            SqlSugarClient db = new SqlSugarClient(DBModels.Config.MainDBConnectionConfig);
            var type = await db.Queryable<InvMarketGroup>().FirstAsync(p => p.MarketGroupID == id);
            if (DBLanguage == Enums.Language.Chinese)
            {
                await ZHDBService.TranInvMarketGroupAsync(type);
            }
            return type;
        }

        public static async Task<List<InvMarketGroup>> QueryAsync(List<int> ids)
        {
            return await Task.Run(async() =>
            {
                SqlSugarClient db = new SqlSugarClient(DBModels.Config.MainDBConnectionConfig);
                var types = db.Queryable<InvMarketGroup>().Where(p => ids.Contains(p.MarketGroupID)).ToList();
                if (DBLanguage == Enums.Language.Chinese)
                {
                    await ZHDBService.TranInvMarketGroupsAsync(types);
                }
                return types;
            });
        }
    }
}
