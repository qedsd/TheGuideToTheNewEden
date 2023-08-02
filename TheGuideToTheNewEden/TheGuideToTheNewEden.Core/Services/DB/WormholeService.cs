using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public class WormholeService
    {
        public static async Task InsertAsync(List<WormholePortal> portals)
        {
            await DBService.WormholeDb.Insertable(portals).ExecuteCommandAsync();
        }
        public static async Task InsertAsync(List<Wormhole> wormholes)
        {
            await DBService.WormholeDb.Insertable(wormholes).ExecuteCommandAsync();
        }
    }
}
