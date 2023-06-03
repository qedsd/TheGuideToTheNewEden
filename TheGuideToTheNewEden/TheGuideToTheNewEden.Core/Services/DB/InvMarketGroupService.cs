using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public class InvMarketGroupService
    {
        public static async Task<InvMarketGroup> QueryAsync(int id)
        {
            var type = await DBService.MainDb.Queryable<InvMarketGroup>().FirstAsync(p => p.MarketGroupID == id);
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranInvMarketGroupAsync(type);
            }
            return type;
        }

        public static async Task<List<InvMarketGroup>> QueryAsync(List<int> ids)
        {
            var types = await DBService.MainDb.Queryable<InvMarketGroup>().Where(p => ids.Contains(p.MarketGroupID)).ToListAsync();
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranInvMarketGroupsAsync(types);
            }
            return types;
        }

        public static async Task<List<InvMarketGroup>> QueryRootGroupAsync()
        {
            var groups = await DBService.MainDb.Queryable<InvMarketGroup>().Where(p => p.ParentGroupID == null).ToListAsync();
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranInvMarketGroupsAsync(groups);
            }
            return groups;
        }
        public static async Task<List<InvMarketGroup>> QuerySubGroupAsync()
        {
            var groups = await DBService.MainDb.Queryable<InvMarketGroup>().Where(p => p.ParentGroupID != null).ToListAsync();
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranInvMarketGroupsAsync(groups);
            }
            return groups;
        }
    }
}
