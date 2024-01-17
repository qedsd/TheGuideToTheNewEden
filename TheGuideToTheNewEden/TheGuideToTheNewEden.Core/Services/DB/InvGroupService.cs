using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.Character;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public class InvGroupService
    {
        public static async Task<InvGroup> QueryGroupAsync(int id)
        {
            var item = await DBService.MainDb.Queryable<InvGroup>().FirstAsync(p => p.GroupID == id);
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranInvGroupAsync(item);
            }
            return item;
        }
        public static InvGroup QueryGroup(int id)
        {
            var item = DBService.MainDb.Queryable<InvGroup>().First(p => p.GroupID == id);
            if (DBService.NeedLocalization)
            {
                LocalDbService.TranInvGroup(item);
            }
            return item;
        }
        public static async Task<List<InvGroup>> QueryGroupsAsync(string name)
        {
            var items = await DBService.MainDb.Queryable<InvGroup>().Where(p => p.GroupName.Contains(name)).ToListAsync();
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranInvGroupsAsync(items);
            }
            return items;
        }
        public static List<InvGroup> QueryGroups(string name)
        {
            var items = DBService.MainDb.Queryable<InvGroup>().Where(p => p.GroupName.Contains(name)).ToList();
            if (DBService.NeedLocalization)
            {
                LocalDbService.TranInvGroups(items);
            }
            return items;
        }
        public static async Task<List<InvGroup>> QueryGroupsAsync(List<int> ids)
        {
            var items = await DBService.MainDb.Queryable<InvGroup>().Where(p => ids.Contains(p.GroupID)).ToListAsync();
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranInvGroupsAsync(items);
            }
            return items;
        }
        /// <summary>
        /// 获取每个技能组的id、name、包含的skill id
        /// 不对Skills赋值
        /// </summary>
        /// <returns></returns>

        public static async Task<List<SkillGroup>> QuerySkillGroupsAsync()
        {
            return await Task.Run(async () =>
            {
                var groups = DBService.MainDb.Queryable<InvGroup>().Where(p => p.CategoryID == 16).ToList();
                if (DBService.NeedLocalization)
                {
                    await LocalDbService.TranInvGroupsAsync(groups);
                }
                List<SkillGroup> skillGroups = new List<SkillGroup>();
                foreach(var group in groups)
                {
                    var types = await InvTypeService.QueryTypesInGroupAsync(group.GroupID);
                    skillGroups.Add(new SkillGroup()
                    {
                        GroupId = group.GroupID,
                        GroupName = group.GroupName,
                        SkillIds = types.Select(p=>p.TypeID).ToHashSet2()
                    });
                }
                return skillGroups;
            });
        }
        /// <summary>
        /// 查询蓝图下的分组
        /// </summary>
        /// <returns></returns>
        public static async Task<List<InvGroup>> QueryBlueprintGroupsAsync()
        {
            var groups = await DBService.MainDb.Queryable<InvGroup>().Where(p => p.CategoryID == 9).ToListAsync();
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranInvGroupsAsync(groups);
            }
            return groups;
        }
        /// <summary>
        /// 查询蓝图下的分组id
        /// </summary>
        /// <returns></returns>
        public static async Task<List<int>> QueryBlueprintGroupIdsAsync()
        {
            var groups = await DBService.MainDb.Queryable<InvGroup>().Where(p => p.CategoryID == 9).ToListAsync();
            
            return groups.Select(p=>p.GroupID).ToList();
        }

        public static List<int> QueryGroupIdOfCategory(List<int> categories)
        {
            var groups = DBService.MainDb.Queryable<InvGroup>().Where(p => categories.Contains(p.CategoryID)).ToList();
            return groups.Select(p => p.GroupID).ToList();
        }
    }
}
