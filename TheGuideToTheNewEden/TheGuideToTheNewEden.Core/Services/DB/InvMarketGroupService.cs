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
        public static InvMarketGroup Query(int id)
        {
            var type = DBService.MainDb.Queryable<InvMarketGroup>().First(p => p.MarketGroupID == id);
            if (DBService.NeedLocalization)
            {
                LocalDbService.TranInvMarketGroup(type);
            }
            return type;
        }
        /// <summary>
        /// 查找ParentGroupID等于传入id的所有group
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static List<InvMarketGroup> QuerySubGroupId(int id)
        {
            var groups = DBService.MainDb.Queryable<InvMarketGroup>().Where(p => p.ParentGroupID == id).ToList();
            if (DBService.NeedLocalization)
            {
                foreach(var group in groups)
                {
                    LocalDbService.TranInvMarketGroup(group);
                }
            }
            return groups;
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

        /// <summary>
        /// 查找物品最顶级分类id
        /// </summary>
        /// <param name="typeId"></param>
        /// <returns></returns>
        public static int QueryRootGroupOfType(int typeId)
        {
            var type = InvTypeService.QueryType(typeId, false);
            if(type != null)
            {
                int? groupId = type.MarketGroupID;
                int? groupIdTemp = groupId;
                while (groupId != null && groupId > 0)
                {
                    groupIdTemp = groupId;
                    groupId = QueryParentId((int)groupId);
                }
                return groupIdTemp == null ? -1 : (int)groupIdTemp;
            }
            return -1;
        }
        public static int? QueryParentId(int groupId)
        {
            return DBService.MainDb.Queryable<InvMarketGroup>().First(p => p.MarketGroupID == groupId).ParentGroupID;
        }
    }
}
