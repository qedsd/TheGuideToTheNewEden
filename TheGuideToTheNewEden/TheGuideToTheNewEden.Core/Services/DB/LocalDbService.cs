using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public static class LocalDbService
    {
        #region type
        public static async Task<List<InvTypeBase>> TranInvTypesAsync(List<int> invTypeIds)
        {
            return await DBService.LocalDb.Queryable<InvTypeBase>().Where(p => invTypeIds.Contains(p.TypeID)).ToListAsync();
        }
        public static List<InvTypeBase> TranInvTypes(List<int> invTypeIds)
        {
            return DBService.LocalDb.Queryable<InvTypeBase>().Where(p => invTypeIds.Contains(p.TypeID)).ToList();
        }
        public static async Task<InvTypeBase> TranInvTypeAsync(int invTypeId)
        {
            return await DBService.LocalDb.Queryable<InvTypeBase>().FirstAsync(p => invTypeId == p.TypeID);
        }
        public static InvTypeBase TranInvType(int invTypeId)
        {
            return DBService.LocalDb.Queryable<InvTypeBase>().First(p => invTypeId == p.TypeID);
        }

        public static List<InvTypeBase> SearchInvType(string partName)
        {
            return DBService.LocalDb.Queryable<InvTypeBase>().Where(p => p.TypeName.Contains(partName)).ToList();
        }

        public static async Task TranInvTypesAsync(List<InvType> invTypes)
        {
            Dictionary<int, InvType> keyValuePairs = new Dictionary<int, InvType>();
            foreach (InvType invType in invTypes)
            {
                keyValuePairs.Add(invType.TypeID, invType);
            }
            var results = await TranInvTypesAsync(invTypes.Select(p => p.TypeID).ToList());
            foreach(var result in results)
            {
                keyValuePairs.TryGetValue(result.TypeID, out var keyValue);
                {
                    keyValue.TypeName = result.TypeName;
                    keyValue.Description = result.Description;
                }
            }
            keyValuePairs.Clear();
            keyValuePairs = null;
        }
        public static void TranInvTypes(List<InvType> invTypes)
        {
            Dictionary<int, InvType> keyValuePairs = new Dictionary<int, InvType>();
            foreach (InvType invType in invTypes)
            {
                keyValuePairs.Add(invType.TypeID, invType);
            }
            var results = TranInvTypes(invTypes.Select(p => p.TypeID).ToList());
            foreach (var result in results)
            {
                keyValuePairs.TryGetValue(result.TypeID, out var keyValue);
                {
                    keyValue.TypeName = result.TypeName;
                    keyValue.Description = result.Description;
                }
            }
            keyValuePairs.Clear();
            keyValuePairs = null;
        }

        public static async Task TranInvTypeAsync(InvType invType)
        {
            var type = await TranInvTypeAsync(invType.TypeID);
            invType.TypeName = type?.TypeName;
            invType.Description = type?.Description;
        }
        public static void TranInvType(InvType invType)
        {
            var type = TranInvType(invType.TypeID);
            invType.TypeName = type?.TypeName;
            invType.Description = type?.Description;
        }
        #endregion

        #region group
        public static async Task<List<InvGroupBase>> TranInvGroupsAsync(List<int> invTypeIds)
        {
            return await DBService.LocalDb.Queryable<InvGroupBase>().Where(p => invTypeIds.Contains(p.GroupID)).ToListAsync();
        }
        public static List<InvGroupBase> TranInvGroups(List<int> invTypeIds)
        {
            return DBService.LocalDb.Queryable<InvGroupBase>().Where(p => invTypeIds.Contains(p.GroupID)).ToList();
        }
        public static async Task<InvGroupBase> TranInvGroupAsync(int invTypeId)
        {
            return await DBService.LocalDb.Queryable<InvGroupBase>().FirstAsync(p => invTypeId == p.GroupID);
        }
        public static InvGroupBase TranInvGroup(int invTypeId)
        {
            return DBService.LocalDb.Queryable<InvGroupBase>().First(p => invTypeId == p.GroupID);
        }

        public static async Task TranInvGroupsAsync(List<InvGroup> invGroups)
        {
            Dictionary<int, InvGroup> keyValuePairs = new Dictionary<int, InvGroup>();
            foreach (InvGroup invGroup in invGroups)
            {
                keyValuePairs.Add(invGroup.GroupID, invGroup);
            }
            var results = await TranInvGroupsAsync(invGroups.Select(p => p.GroupID).ToList());
            foreach (var result in results)
            {
                keyValuePairs.TryGetValue(result.GroupID, out var keyValue);
                {
                    keyValue.GroupName = result.GroupName;
                }
            }
            keyValuePairs.Clear();
            keyValuePairs = null;
        }
        public static void TranInvGroups(List<InvGroup> invGroups)
        {
            Dictionary<int, InvGroup> keyValuePairs = new Dictionary<int, InvGroup>();
            foreach (InvGroup invGroup in invGroups)
            {
                keyValuePairs.Add(invGroup.GroupID, invGroup);
            }
            var results = TranInvGroups(invGroups.Select(p => p.GroupID).ToList());
            foreach (var result in results)
            {
                keyValuePairs.TryGetValue(result.GroupID, out var keyValue);
                {
                    keyValue.GroupName = result.GroupName;
                }
            }
            keyValuePairs.Clear();
            keyValuePairs = null;
        }
        public static async Task TranInvGroupAsync(InvGroup invGroup)
        {
            var type = await TranInvGroupAsync(invGroup.GroupID);
            invGroup.GroupName = type?.GroupName;
        }
        public static void TranInvGroup(InvGroup invGroup)
        {
            var type = TranInvGroup(invGroup.GroupID);
            invGroup.GroupName = type?.GroupName;
        }

        public static List<InvGroupBase> SearchInvGroup(string partName)
        {
            return DBService.LocalDb.Queryable<InvGroupBase>().Where(p => p.GroupName.Contains(partName)).ToList();
        }
        #endregion

        #region region
        public static async Task<List<MapRegionBase>> TranMapRegionsAsync(List<int> ids)
        {
            return await DBService.LocalDb.Queryable<MapRegionBase>().Where(p => ids.Contains(p.RegionID)).ToListAsync();
        }
        public static List<MapRegionBase> TranMapRegions(List<int> ids)
        {
            return DBService.LocalDb.Queryable<MapRegionBase>().Where(p => ids.Contains(p.RegionID)).ToList();
        }
        public static async Task<MapRegionBase> TranMapRegionAsync(int id)
        {
            return await DBService.LocalDb.Queryable<MapRegionBase>().FirstAsync(p => id == p.RegionID);
        }
        public static MapRegionBase TranMapRegion(int id)
        {
            return DBService.LocalDb.Queryable<MapRegionBase>().First(p => id == p.RegionID);
        }

        public static async Task TranMapRegionsAsync(List<MapRegion> items)
        {
            Dictionary<int, MapRegion> keyValuePairs = new Dictionary<int, MapRegion>();
            foreach (var item in items)
            {
                keyValuePairs.Add(item.RegionID, item);
            }
            var results = await TranMapRegionsAsync(items.Select(p => p.RegionID).ToList());
            foreach (var result in results)
            {
                keyValuePairs.TryGetValue(result.RegionID, out var keyValue);
                {
                    keyValue.RegionName = result.RegionName;
                }
            }
            keyValuePairs.Clear();
            keyValuePairs = null;
        }

        public static void TranMapRegions(List<MapRegion> items)
        {
            Dictionary<int, MapRegion> keyValuePairs = new Dictionary<int, MapRegion>();
            foreach (var item in items)
            {
                keyValuePairs.Add(item.RegionID, item);
            }
            var results = TranMapRegions(items.Select(p => p.RegionID).ToList());
            foreach (var result in results)
            {
                keyValuePairs.TryGetValue(result.RegionID, out var keyValue);
                {
                    keyValue.RegionName = result.RegionName;
                }
            }
            keyValuePairs.Clear();
            keyValuePairs = null;
        }

        public static async Task TranMapRegionAsync(MapRegion item)
        {
            var type = await TranMapRegionAsync(item.RegionID);
            item.RegionName = type?.RegionName;
        }
        public static void TranMapRegion(MapRegion item)
        {
            var type = TranMapRegion(item.RegionID);
            item.RegionName = type?.RegionName;
        }

        public static List<MapRegionBase> SearchMapRegion(string partName)
        {
            return DBService.LocalDb.Queryable<MapRegionBase>().Where(p => p.RegionName.Contains(partName)).ToList();
        }
        #endregion

        #region solarySystem
        public static async Task<List<MapSolarSystemBase>> TranMapSolarSystemsAsync(List<int> ids)
        {
            return await DBService.LocalDb.Queryable<MapSolarSystemBase>().Where(p => ids.Contains(p.SolarSystemID)).ToListAsync();
        }
        public static List<MapSolarSystemBase> TranMapSolarSystems(List<int> ids)
        {
            return DBService.LocalDb.Queryable<MapSolarSystemBase>().Where(p => ids.Contains(p.SolarSystemID)).ToList();
        }
        public static MapSolarSystemBase TranMapSolarSystem(int id)
        {
            return DBService.LocalDb.Queryable<MapSolarSystemBase>().First(p => id == p.SolarSystemID);
        }
        public static async Task<MapSolarSystemBase> TranMapSolarSystemAsync(int id)
        {
            return await DBService.LocalDb.Queryable<MapSolarSystemBase>().FirstAsync(p => id == p.SolarSystemID);
        }
        public static void TranMapSolarSystem(MapSolarSystem item)
        {
            var tran = TranMapSolarSystem(item.SolarSystemID);
            if(tran != null)
            {
                item.SolarSystemName = tran.SolarSystemName;
            }
        }
        public static async Task TranMapSolarSystemsAsync(List<MapSolarSystem> items)
        {
            Dictionary<int, MapSolarSystem> keyValuePairs = new Dictionary<int, MapSolarSystem>();
            foreach (var item in items)
            {
                keyValuePairs.Add(item.SolarSystemID, item);
            }
            var results = await TranMapSolarSystemsAsync(items.Select(p => p.SolarSystemID).ToList());
            foreach (var result in results)
            {
                keyValuePairs.TryGetValue(result.SolarSystemID, out var keyValue);
                {
                    keyValue.SolarSystemName = result.SolarSystemName;
                }
            }
            keyValuePairs.Clear();
            keyValuePairs = null;
        }
        public static void TranMapSolarSystems(List<MapSolarSystem> items)
        {
            Dictionary<int, MapSolarSystem> keyValuePairs = new Dictionary<int, MapSolarSystem>();
            foreach (var item in items)
            {
                keyValuePairs.Add(item.SolarSystemID, item);
            }
            var results = TranMapSolarSystems(items.Select(p => p.SolarSystemID).ToList());
            foreach (var result in results)
            {
                keyValuePairs.TryGetValue(result.SolarSystemID, out var keyValue);
                {
                    keyValue.SolarSystemName = result.SolarSystemName;
                }
            }
            keyValuePairs.Clear();
            keyValuePairs = null;
        }

        public static async Task TranMapSolarSystemAsync(MapSolarSystem item)
        {
            var type = await TranMapSolarSystemAsync(item.SolarSystemID);
            item.SolarSystemName = type?.SolarSystemName;
        }
        public static List<MapSolarSystemBase> SearchMapSolarSystem(string partName)
        {
            return DBService.LocalDb.Queryable<MapSolarSystemBase>().Where(p => p.SolarSystemName.Contains(partName)).ToList();
        }
        #endregion

        #region station
        public static async Task<List<StaStationBase>> TranStaStationsAsync(List<int> ids)
        {
            return await DBService.LocalDb.Queryable<StaStationBase>().Where(p => ids.Contains(p.StationID)).ToListAsync();
        }
        public static async Task<StaStationBase> TranStaStationAsync(int id)
        {
            return await DBService.LocalDb.Queryable<StaStationBase>().FirstAsync(p => id == p.StationID);
        }
        public static StaStationBase TranStaStation(int id)
        {
            return DBService.LocalDb.Queryable<StaStationBase>().First(p => id == p.StationID);
        }

        public static async Task TranStaStationsAsync(List<StaStation> items)
        {
            Dictionary<int, StaStation> keyValuePairs = new Dictionary<int, StaStation>();
            foreach (var item in items)
            {
                keyValuePairs.Add(item.StationID, item);
            }
            var results = await TranStaStationsAsync(items.Select(p => p.StationID).ToList());
            foreach (var result in results)
            {
                keyValuePairs.TryGetValue(result.StationID, out var keyValue);
                {
                    keyValue.StationName = result.StationName;
                }
            }
            keyValuePairs.Clear();
            keyValuePairs = null;
        }

        public static async Task TranStaStationAsync(StaStation item)
        {
            var type = await TranStaStationAsync(item.StationID);
            item.StationName = type?.StationName;
        }
        public static void TranStaStation(StaStation item)
        {
            var type = TranStaStation(item.StationID);
            item.StationName = type?.StationName;
        }
        public static List<StaStationBase> SearchStaStations(string partName)
        {
            return DBService.LocalDb.Queryable<StaStationBase>().Where(p => p.StationName.Contains(partName)).ToList();
        }
        #endregion

        #region marketGroup
        public static async Task<List<InvMarketGroupBase>> TranInvMarketGroupsAsync(List<int> ids)
        {
            return await DBService.LocalDb.Queryable<InvMarketGroupBase>().Where(p => ids.Contains(p.MarketGroupID)).ToListAsync();
        }
        public static async Task<InvMarketGroupBase> TranInvMarketGroupAsync(int id)
        {
            return await DBService.LocalDb.Queryable<InvMarketGroupBase>().FirstAsync(p => id == p.MarketGroupID);
        }
        public static InvMarketGroupBase TranInvMarketGroup(int id)
        {
            return DBService.LocalDb.Queryable<InvMarketGroupBase>().First(p => id == p.MarketGroupID);
        }

        public static async Task TranInvMarketGroupsAsync(List<InvMarketGroup> items)
        {
            Dictionary<int, InvMarketGroup> keyValuePairs = new Dictionary<int, InvMarketGroup>();
            foreach (var item in items)
            {
                keyValuePairs.Add(item.MarketGroupID, item);
            }
            var results = await TranInvMarketGroupsAsync(items.Select(p => p.MarketGroupID).ToList());
            foreach (var result in results)
            {
                keyValuePairs.TryGetValue(result.MarketGroupID, out var keyValue);
                {
                    keyValue.MarketGroupName = result.MarketGroupName;
                    keyValue.Description = result.Description;
                }
            }
            keyValuePairs.Clear();
            keyValuePairs = null;
        }

        public static async Task TranInvMarketGroupAsync(InvMarketGroup item)
        {
            var type = await TranInvMarketGroupAsync(item.MarketGroupID);
            item.MarketGroupName = type?.MarketGroupName;
            item.Description = type?.Description;
        }
        public static void TranInvMarketGroup(InvMarketGroup item)
        {
            var type = TranInvMarketGroup(item.MarketGroupID);
            item.MarketGroupName = type?.MarketGroupName;
            item.Description = type?.Description;
        }
        #endregion
    }
}
