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
        /// invMarketGroups 全表的 MarketGroupID → ParentGroupID 映射（表只有两千行左右，进程内缓存一次）。
        /// 原先 QueryRootGroupOfType 沿树逐级查库（每个图标 3~5 次 SQLite 查询），图标是高频调用
        /// （击杀列表/星图舰船图标都在后台线程调），与其他线程撞库会炸出 Microsoft.Data.Sqlite 的 Close() NRE。
        /// </summary>
        private static Dictionary<int, int?> _groupParents;
        private static readonly object _groupParentsLock = new object();

        private static Dictionary<int, int?> GetGroupParents()
        {
            if (_groupParents is null)
            {
                lock (_groupParentsLock)
                {
                    if (_groupParents is null)
                    {
                        var map = new Dictionary<int, int?>();
                        foreach (var group in DBService.MainDb.Queryable<InvMarketGroup>().ToList())
                        {
                            map[group.MarketGroupID] = group.ParentGroupID;
                        }

                        _groupParents = map;
                    }
                }
            }

            return _groupParents;
        }

        /// <summary>
        /// 查找物品最顶级分类id
        /// </summary>
        /// <param name="typeId"></param>
        /// <returns></returns>
        public static int QueryRootGroupOfType(long typeId)
        {
            var type = InvTypeService.QueryType(typeId, false);
            var marketGroupId = type?.MarketGroupID;
            if (marketGroupId == null || marketGroupId <= 0)
            {
                return -1;
            }

            var parents = GetGroupParents();
            var current = marketGroupId.Value;
            var root = current;
            // 防御：市场分组理论上是树，给步数上限避免脏数据成环导致死循环
            for (var step = 0; step < 32; step++)
            {
                if (!parents.TryGetValue(current, out var parent) || parent == null || parent <= 0)
                {
                    break;
                }

                root = parent.Value;
                current = parent.Value;
            }

            return root;
        }
        public static int? QueryParentId(int groupId)
        {
            // 分组不在本地库时 First() 返回 null，直接点 .ParentGroupID 会 NRE（阶段 63 实机："查询物品不在本地数据库时经常出现 null 报错"）
            return DBService.MainDb.Queryable<InvMarketGroup>().First(p => p.MarketGroupID == groupId)?.ParentGroupID;
        }
    }
}
