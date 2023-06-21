﻿using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public class MapSolarSystemService
    {
        public static async Task<MapSolarSystem> QueryAsync(int id)
        {
            var type = await DBService.MainDb.Queryable<MapSolarSystem>().FirstAsync(p => p.SolarSystemID == id);
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranMapSolarSystemAsync(type);
            }
            return type;
        }

        public static async Task<List<MapSolarSystem>> QueryAsync(List<int> ids)
        {
            var types = await DBService.MainDb.Queryable<MapSolarSystem>().Where(p => ids.Contains(p.SolarSystemID)).ToListAsync();
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranMapSolarSystemsAsync(types);
            }
            return types;
        }
        public static List<MapSolarSystem> Query(List<int> ids)
        {
            var types = DBService.MainDb.Queryable<MapSolarSystem>().Where(p => ids.Contains(p.SolarSystemID)).ToList();
            if (DBService.NeedLocalization)
            {
                LocalDbService.TranMapSolarSystems(types);
            }
            return types;
        }
        public static MapSolarSystem Query(int id)
        {
            var system = DBService.MainDb.Queryable<MapSolarSystem>().First(p => id == p.SolarSystemID);
            if (DBService.NeedLocalization)
            {
                LocalDbService.TranMapSolarSystem(system);
            }
            return system;
        }

        public static async Task<List<MapSolarSystem>> QueryAllAsync()
        {
            var list = await DBService.MainDb.Queryable<MapSolarSystem>().ToListAsync();
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranMapSolarSystemsAsync(list);
            }
            return list;
        }
        public static List<MapSolarSystem> QueryAll()
        {
            var list = DBService.MainDb.Queryable<MapSolarSystem>().ToList();
            if (DBService.NeedLocalization)
            {
                LocalDbService.TranMapSolarSystems(list);
            }
            return list;
        }
    }
}
