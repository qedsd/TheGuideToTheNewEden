using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public static class MissionService
    {
        public static async Task<List<Mission>> QueryMissionAsync(int level)
        {
            return await DBService.StaticDb.Queryable<Mission>().Where(p => p.Level == level).ToListAsync();
        }

        public static async Task<List<Mission>> QueryMissionAsync(string partName)
        {
            return await DBService.StaticDb.Queryable<Mission>().Where(p => p.Title_Zh.Contains(partName) || p.Title_En.Contains(partName)).ToListAsync();
        }

        public static MissionContent QueryMissionContent(int id)
        {
            return DBService.StaticDb.Queryable<MissionContent>().First(p => p.Id == id);
        }
    }
}
