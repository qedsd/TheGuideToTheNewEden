using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public static class DEDService
    {
        public static async Task<List<DED>> QueryAllAsync(int type)
        {
            return await DBService.DEDDb.Queryable<DED>().Where(p=>p.Type == type).ToListAsync();
        }
        public static async Task<List<DED>> SearchAsync(string portName)
        {
            return await DBService.DEDDb.Queryable<DED>().Where(p => p.TitleCN.Contains(portName) || p.TitleEN.Contains(portName)).ToListAsync();
        }
    }
}
