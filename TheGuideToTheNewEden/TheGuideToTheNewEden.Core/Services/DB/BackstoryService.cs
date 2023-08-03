using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public  class BackstoryService
    {
        public static async Task<List<Backstory>> QueryBackstoryAsync(int type)
        {
            return await DBService.StaticDb.Queryable<Backstory>().Where(p => p.Type == type).ToListAsync();
        }

        public static async Task<List<Backstory>> QueryBackstoryAsync(string partName)
        {
            return await DBService.StaticDb.Queryable<Backstory>().Where(p => p.Title_Zh.Contains(partName) || p.Title_En.Contains(partName)).ToListAsync();
        }

        public static BackstoryContent QueryBackstoryContent(int id)
        {
            return DBService.StaticDb.Queryable<BackstoryContent>().First(p => p.Id == id);
        }
    }
}
