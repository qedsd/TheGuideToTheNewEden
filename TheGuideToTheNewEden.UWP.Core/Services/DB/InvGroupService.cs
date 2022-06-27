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
    public class InvGroupService: DBService
    {
        public static async Task<InvGroup> QueryGroupAsync(int id)
        {
            SqlSugarClient db = new SqlSugarClient(DBModels.Config.MainDBConnectionConfig);
            var item = await db.Queryable<InvGroup>().FirstAsync(p => p.GroupID == id);
            if (DBLanguage == Enums.Language.Chinese)
            {
                await ZHDBService.TranInvGroupAsync(item);
            }
            return item;
        }

        public static async Task<List<InvGroup>> QueryGroupsAsync(List<int> ids)
        {
            return await Task.Run(async() =>
            {
                SqlSugarClient db = new SqlSugarClient(DBModels.Config.MainDBConnectionConfig);
                var items = db.Queryable<InvGroup>().Where(p => ids.Contains(p.GroupID)).ToList();
                if (DBLanguage == Enums.Language.Chinese)
                {
                    await ZHDBService.TranInvGroupsAsync(items);
                }
                return items;
            });
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
                SqlSugarClient db = new SqlSugarClient(DBModels.Config.MainDBConnectionConfig);
                var groups = db.Queryable<InvGroup>().Where(p => p.CategoryID == 16).ToList();
                if (DBLanguage == Enums.Language.Chinese)
                {
                    await ZHDBService.TranInvGroupsAsync(groups);
                }
                List<SkillGroup> skillGroups = new List<SkillGroup>();
                foreach(var group in groups)
                {
                    var types = await InvTypeService.QueryTypesInGroupAsync(group.GroupID);
                    skillGroups.Add(new SkillGroup()
                    {
                        GroupId = group.GroupID,
                        GroupName = group.GroupName,
                        SkillIds = types.Select(p=>p.TypeID).ToHashSet()
                    });
                }
                return skillGroups;
            });
        }
    }
}
