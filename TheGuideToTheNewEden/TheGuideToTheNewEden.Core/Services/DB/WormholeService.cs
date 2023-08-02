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

        public static async Task<List<WormholePortal>> QueryPortalAsync()
        {
            return await DBService.StaticDb.Queryable<WormholePortal>().ToListAsync();
        }
        public static async Task<List<WormholePortal>> QueryPortalAsync(string partName)
        {
            return await DBService.StaticDb.Queryable<WormholePortal>().Where(p=>p.Name.Contains(partName)).ToListAsync();
        }

        public static WormholePortal QueryPortal(int id)
        {
            return DBService.StaticDb.Queryable<WormholePortal>().First(p => p.Id == id);
        }

        public static async Task<List<Wormhole>> QueryWormholeAsync()
        {
            return await DBService.StaticDb.Queryable<Wormhole>().ToListAsync();
        }
        public static async Task<List<Wormhole>> QueryWormholeAsync(string partName)
        {
            return await DBService.StaticDb.Queryable<Wormhole>().Where(p => p.Name.Contains(partName)).ToListAsync();
        }
    }
}
