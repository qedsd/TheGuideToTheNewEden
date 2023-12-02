using ESI.NET.Models.Character;
using Microsoft.UI.Xaml;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.WinUI.Services.Settings;
using static TheGuideToTheNewEden.WinUI.Services.MarketOrderService;

namespace TheGuideToTheNewEden.WinUI.Services
{
    public class MarketOrderService
    {
        private static string StructureOrderFolder => MarketOrderSettingService.StructureOrderFolder;
        private static string RegionOrderFolder => MarketOrderSettingService.RegionOrderFolder;
        private static string HistoryOrderFolder => MarketOrderSettingService.HistoryOrderFolder;
        private static MarketOrderService current;
        public static MarketOrderService Current
        {
            get
            {
                if (current == null)
                {
                    current = new MarketOrderService();
                }
                return current;
            }
        }
        private ESI.NET.EsiClient EsiClient;
        public MarketOrderService()
        {
            EsiClient = Core.Services.ESIService.GetDefaultEsi();
        }

        /// <summary>
        /// 订单过期时间
        /// 分钟
        /// </summary>
        private static int OrderDuration => MarketOrderSettingService.OrderDurationValue;
        /// <summary>
        /// 历史记录过期时间
        /// 分钟
        /// </summary>
        private static int HistoryDuration => MarketOrderSettingService.HistoryDurationValue;
        private static int MaxThread => MarketOrderSettingService.ThreadValue;

        /// <summary>
        /// 获取建筑指定物品订单，优先从缓存获取，缓存不存在或过期时自动刷新
        /// </summary>
        /// <param name="structureId"></param>
        /// <param name="invTypeId"></param>
        /// <returns></returns>
        public async Task<List<Core.Models.Market.Order>> GetStructureOrdersAsync(long structureId, int invTypeId)
        {
            var orders = await GetStructureOrdersAsync(structureId);
            if(orders.NotNullOrEmpty())
            {
                return orders.Where(p => p.TypeId == invTypeId).ToList();
            }
            else
            {
                return null;
            }
        }
        public async Task<List<Core.Models.Market.Order>> GetStructureOrdersAsync(long structureId)
        {
            return await GetStructureOrdersAsync(structureId, null);
        }
        /// <summary>
        /// 获取建筑所有订单，优先从缓存获取，缓存不存在或过期时自动刷新
        /// </summary>
        /// <param name="structureId"></param>
        /// <returns></returns>
        public async Task<List<Core.Models.Market.Order>> GetStructureOrdersAsync(long structureId, PageCallBackDelegate pageCallBack = null)
        {
            //优先从本地加载
            string filePath = GetFilePath(structureId);
            if (File.Exists(filePath))
            {
                var fileText = File.ReadAllText(filePath);
                if (!string.IsNullOrEmpty(fileText))
                {
                    var localOrder = await Task.Run(() => JsonConvert.DeserializeObject<StructureOrder>(fileText));
                    if (localOrder != null && localOrder.Orders.NotNullOrEmpty())//本地存在订单数据
                    {
                        if ((DateTime.Now - localOrder.UpdateTime).TotalMinutes < OrderDuration)//还在有效期内
                        {
                            await Task.Run(() =>
                            {
                                localOrder.Orders.ForEach(p => p.RemainTimeSpan = p.Issued.AddDays(p.Duration) - DateTime.Now);
                            });
                            await SetOrderInfo(localOrder.Orders);
                            return localOrder.Orders;
                        }
                        else
                        {
                            //不在有效期内当作本地不存在订单处理
                        }
                    }
                }
            }

            //本地不存在或过期或解析失败，重新获取
            var orders = await GetLatestStructureOrdersAsync(structureId, pageCallBack);
            if (orders.NotNullOrEmpty())
            {
                var newOrder = new StructureOrder()
                {
                    StructureId = structureId,
                    UpdateTime = DateTime.Now,
                    Orders = orders
                };
                await Save(newOrder);
            }
            return orders;
        }

        /// <summary>
        /// 实时获取建筑所有订单
        /// 不使用缓存
        /// </summary>
        /// <param name="structureId"></param>
        /// <returns></returns>
        public async Task<List<Core.Models.Market.Order>> GetLatestStructureOrdersAsync(long structureId, PageCallBackDelegate pageCallBack = null)
        {
            var structure = Services.StructureService.GetStructure(structureId);
            if(structure == null)
            {
                Core.Log.Error($"未找到建筑{structureId}");
                return null;
            }
            var character = CharacterService.GetCharacter(structure.CharacterId);
            if (character == null)
            {
                Core.Log.Error($"未找到角色{structure.CharacterId}");
                return null;
            }
            if (!character.IsTokenValid())
            {
                if (!await character.RefreshTokenAsync())
                {
                    Core.Log.Error("Token已过期，尝试刷新失败");
                    return null;
                }
            }
            EsiClient.SetCharacterData(character);
            List<Core.Models.Market.Order> orders = new List<Core.Models.Market.Order>();
            int page = 0;
            while (true)
            {
                page++;
                var resp = await EsiClient.Market.StructureOrders(structureId, page);
                if (resp != null && resp.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    pageCallBack?.Invoke(page, "Structure");
                    if (resp.Data.Any())
                    {
                        orders.AddRange(resp.Data.Select(p => new Core.Models.Market.Order(p)));
                        if (orders.Count < 1000)
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    Core.Log.Error(resp?.Message);
                    break;
                }
            }
            if (orders.Any())
            {
                foreach (var order in orders)
                {
                    order.SystemId = structure.SolarSystemId;
                }
                await SetOrderInfo(orders);
            }
            return orders;
        }

        private static async Task Save(StructureOrder order)
        {
            await Task.Run(() =>
            {
                string json = JsonConvert.SerializeObject(order);
                if (!Directory.Exists(StructureOrderFolder))
                {
                    Directory.CreateDirectory(StructureOrderFolder);
                }
                File.WriteAllText(order.SaveFilePath, json);
            });
        }
        private static async Task Save(RegionOrder order)
        {
            await Task.Run(() =>
            {
                string json = JsonConvert.SerializeObject(order);
                if (!Directory.Exists(RegionOrderFolder))
                {
                    Directory.CreateDirectory(RegionOrderFolder);
                }
                File.WriteAllText(order.SaveFilePath, json);
            });
        }

        private static string GetFilePath(long structureId)
        {
            return System.IO.Path.Combine(StructureOrderFolder, $"{structureId}.json");
        }
        private static string GetFilePath(int regionId)
        {
            return System.IO.Path.Combine(RegionOrderFolder, $"{regionId}.json");
        }

        /// <summary>
        /// 获取星域订单
        /// 买单只包含空间站，卖单可包含建筑
        /// </summary>
        /// <param name="typeId"></param>
        /// <param name="regionId"></param>
        /// <returns></returns>
        public async Task<List<Core.Models.Market.Order>> GetOnlyRegionOrdersAsync(int typeId, int regionId)
        {
            List<Core.Models.Market.Order> orders = new List<Core.Models.Market.Order>();
            int page = 1;
            while (true)
            {
                var resp = await Core.Services.ESIService.Current.EsiClient.Market.RegionOrders(regionId, ESI.NET.Enumerations.MarketOrderType.All, page++, typeId);
                if (resp != null && resp.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    if (resp.Data.Any())
                    {
                        orders.AddRange(resp.Data.Select(p => new Core.Models.Market.Order(p)));
                        if (orders.Count < 1000)
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    Core.Log.Error(resp?.Message);
                    break;
                }
            }
            if (orders.Any())
            {
                await SetOrderInfo(orders);
            }
            return orders;
        }
        /// <summary>
        /// API获取最新星域所有订单
        /// </summary>
        /// <param name="regionId"></param>
        /// <returns></returns>
        public async Task<List<Core.Models.Market.Order>> GetLatestOnlyRegionOrdersAsync(int regionId, PageCallBackDelegate pageCallBack = null)
        {
            List<Core.Models.Market.Order> orders = new List<Core.Models.Market.Order>();
            int page = 0;
            while (true)
            {
                page++;
                var resp = await EsiClient.Market.RegionOrders(regionId, ESI.NET.Enumerations.MarketOrderType.All, page);
                if (resp != null && resp.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    pageCallBack?.Invoke(page, "Region");
                    if (resp.Data.Any())
                    {
                        orders.AddRange(resp.Data.Select(p => new Core.Models.Market.Order(p)));
                        if (orders.Count < 1000)
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    Core.Log.Error(resp?.Message);
                    break;
                }
            }
            if (orders.Any())
            {
                await SetOrderInfo(orders);
            }
            return orders;
        }
        /// <summary>
        /// 获取星域所有订单，优先尝试缓存
        /// </summary>
        /// <param name="regionId"></param>
        /// <returns></returns>
        public async Task<List<Core.Models.Market.Order>> GetOnlyRegionOrdersAsync(int regionId, PageCallBackDelegate pageCallBack = null)
        {
            //优先从本地加载
            string filePath = GetFilePath(regionId);
            if (File.Exists(filePath))
            {
                System.IO.FileInfo fileInfo = new FileInfo(filePath);
                if ((DateTime.Now - fileInfo.LastWriteTime).TotalMinutes < OrderDuration)//还在有效期内
                {
                    var fileText = File.ReadAllText(filePath);
                    if (!string.IsNullOrEmpty(fileText))
                    {
                        var localOrder = await Task.Run(() => JsonConvert.DeserializeObject<RegionOrder>(fileText));
                        if (localOrder != null && localOrder.Orders.NotNullOrEmpty())//本地存在订单数据
                        {
                            await Task.Run(() =>
                            {
                                localOrder.Orders.ForEach(p => p.RemainTimeSpan = p.Issued.AddDays(p.Duration) - DateTime.Now);
                            });
                            await SetOrderInfo(localOrder.Orders);
                            return localOrder.Orders;
                        }
                    }
                }
            }

            //本地不存在或过期或解析失败，重新获取
            var orders = await GetLatestOnlyRegionOrdersAsync(regionId, pageCallBack);
            if (orders.NotNullOrEmpty())
            {
                var newOrder = new RegionOrder()
                {
                    RegionId = regionId,
                    UpdateTime = DateTime.Now,
                    Orders = orders
                };
                await Save(newOrder);
            }
            return orders;
        }

        /// <summary>
        /// 获取指定星域下的建筑订单
        /// </summary>
        /// <param name="typeId"></param>
        /// <param name="regionId"></param>
        /// <returns></returns>
        public async Task<List<Core.Models.Market.Order>> GetOnlyStructureOrdersAsync(int typeId, int regionId)
        {
            var strutures = StructureService.GetStructuresOfRegion(regionId);
            if(strutures.NotNullOrEmpty())
            {
                var result = await Core.Helpers.ThreadHelper.RunAsync(strutures.Select(p=>p.Id), MaxThread, GetStructureOrdersAsync);
                if(result.NotNullOrEmpty())
                {
                    List<Core.Models.Market.Order> orders = new List<Core.Models.Market.Order>();
                    foreach(var list in result)
                    {
                        if(list != null)
                        {
                            var targetOrders = list.Where(p => p.TypeId == typeId).ToList();
                            if (targetOrders.NotNullOrEmpty())
                            {
                                orders.AddRange(targetOrders);
                            }
                        }
                    }
                    return orders;
                }
            }
            return null;
        }
        /// <summary>
        /// 获取指定星域下的建筑订单
        /// </summary>
        /// <param name="regionId"></param>
        /// <returns></returns>
        public async Task<List<Core.Models.Market.Order>> GetOnlyStructureOrdersAsync(int regionId, PageCallBackDelegate pageCallBack = null)
        {
            var strutures = StructureService.GetStructuresOfRegion(regionId);
            if (strutures.NotNullOrEmpty())
            {
                async Task<List<Core.Models.Market.Order>> getStructureOrdersAsync(long id)
                {
                    return await GetStructureOrdersAsync(id, pageCallBack);
                }
                var result = await Core.Helpers.ThreadHelper.RunAsync(strutures.Select(p => p.Id), MaxThread, getStructureOrdersAsync);
                if (result.NotNullOrEmpty())
                {
                    List<Core.Models.Market.Order> orders = new List<Core.Models.Market.Order>();
                    foreach (var list in result)
                    {
                        if (list.NotNullOrEmpty())
                        {
                            orders.AddRange(list);
                        }
                    }
                    return orders;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取星域订单，包含建筑订单
        /// </summary>
        /// <param name="typeId"></param>
        /// <param name="regionId"></param>
        /// <returns></returns>
        public async Task<List<Core.Models.Market.Order>> GetRegionOrdersAsync(int typeId, int regionId)
        {
            var regions = await GetOnlyRegionOrdersAsync(typeId, regionId);
            var structures = await GetOnlyStructureOrdersAsync(typeId, regionId);
            if(regions.NotNullOrEmpty() || structures.NotNullOrEmpty())
            {
                List<Core.Models.Market.Order> orders = new List<Core.Models.Market.Order>();
                if(regions.NotNullOrEmpty())
                {
                    orders.AddRange(regions);
                }
                if(structures.NotNullOrEmpty())
                {
                    //星域订单买单是包含建筑订单的，需要过滤，优先使用星域订单，因为API刷新时间更短
                    var regionsHashSet = regions.Select(p => p.OrderId).ToHashSet2();
                    foreach (var structureOrder in structures)
                    {
                        if (!regionsHashSet.Contains(structureOrder.OrderId))
                        {
                            orders.Add(structureOrder);
                        }
                    }
                }
                return orders;
            }
            return null;
        }
        /// <summary>
        /// 获取星域订单，包含建筑订单
        /// </summary>
        /// <param name="regionId"></param>
        /// <returns></returns>
        public async Task<List<Core.Models.Market.Order>> GetRegionOrdersAsync(int regionId, PageCallBackDelegate pageCallBack = null)
        {
            var regions = await GetOnlyRegionOrdersAsync(regionId, pageCallBack);
            var structures = await GetOnlyStructureOrdersAsync(regionId, pageCallBack);
            if (regions.NotNullOrEmpty() || structures.NotNullOrEmpty())
            {
                List<Core.Models.Market.Order> orders = new List<Core.Models.Market.Order>();
                if (regions.NotNullOrEmpty())
                {
                    orders.AddRange(regions);
                }
                if (structures.NotNullOrEmpty())
                {
                    //星域订单买单是包含建筑订单的，需要过滤，优先使用星域订单，因为API刷新时间更短
                    var regionsHashSet = regions.Select(p => p.OrderId).ToHashSet2();
                    foreach (var structureOrder in structures)
                    {
                        if (!regionsHashSet.Contains(structureOrder.OrderId))
                        {
                            orders.Add(structureOrder);
                        }
                    }
                }
                return orders;
            }
            return null;
        }
        public async Task<List<Core.Models.Market.Order>> GetMapSolarSystemOrdersAsync(int mapSolarSystemId, PageCallBackDelegate pageCallBack = null)
        {
            var system = await Core.Services.DB.MapSolarSystemService.QueryAsync(mapSolarSystemId);
            if(system != null)
            {
                var regionOrders = await GetRegionOrdersAsync(system.RegionID, pageCallBack);
                if(regionOrders.NotNullOrEmpty())
                {
                    return regionOrders.Where(p => p.SystemId == mapSolarSystemId).ToList();
                }
            }
            return null;
        }
        private async Task<Core.Models.Universe.Structure> GetStructure(long id)
        {
            try
            {
                var localStructure = StructureService.GetStructure(id);
                if(localStructure != null)
                {
                    return localStructure;
                }
                else
                {
                    var resp = await EsiClient.Universe.Structure(id);
                    if (resp != null && resp.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        return new Core.Models.Universe.Structure()
                        {
                            Id = id,
                            Name = resp.Data.Name,
                            SolarSystemId = resp.Data.SolarSystemId
                        };
                    }
                    else
                    {
                        return new Core.Models.Universe.Structure()
                        {
                            Id = id,
                            Name = id.ToString(),
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                return new Core.Models.Universe.Structure()
                {
                    Id = id,
                    Name = id.ToString(),
                };
            }
        }

        /// <summary>
        /// 优先读取缓存
        /// 缓存存放在Configs/{regionId}/{typeId}.json
        /// </summary>
        /// <param name="typeId"></param>
        /// <param name="regionId"></param>
        /// <returns></returns>
        public async Task<List<ESI.NET.Models.Market.Statistic>> GetHistoryAsync(int typeId, int regionId)
        {
            string folder = System.IO.Path.Combine(HistoryOrderFolder, regionId.ToString());
            string localFile = System.IO.Path.Combine(folder, $"{typeId}.json");
            if(System.IO.File.Exists(localFile))
            {
                var local =  await Task.Run(() =>
                {
                    System.IO.FileInfo fileInfo = new FileInfo(localFile);
                    if ((DateTime.Now - fileInfo.LastWriteTime).TotalMinutes < HistoryDuration)
                    {
                        return JsonConvert.DeserializeObject<List<ESI.NET.Models.Market.Statistic>>(File.ReadAllText(localFile));
                    }
                    else
                    {
                        return null;
                    }
                });
                if(local.NotNullOrEmpty())
                {
                    return local;
                }
            }
            var resp = await EsiClient.Market.TypeHistoryInRegion(regionId, typeId);
            if (resp != null && resp.StatusCode == System.Net.HttpStatusCode.OK)
            {
                if(!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
                string json = JsonConvert.SerializeObject(resp.Data);
                File.WriteAllText(localFile, json);
                return resp.Data;
            }
            else
            {
                Core.Log.Error(resp?.Message);
                return null;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ids">[0] regionId [1] typeId</param>
        /// <returns></returns>
        public async Task<List<Core.Models.Market.Statistic>> GetHistory(int[] ids)
        {
            try
            {
                var data = await GetHistoryAsync(ids[1], ids[0]);
                if (data.NotNullOrEmpty())
                {
                    return data.Select(p => new Core.Models.Market.Statistic(p, ids[1])).ToList();
                }
                else
                {
                    Core.Log.Info($"获取{ids[0]} {ids[1]}历史记录为空");
                    return null;
                }
            }
            catch(Exception ex)
            {
                Core.Log.Error(ex);
                return null;
            }
        }
        public async Task<List<Core.Models.Market.Statistic>> GetHistory(int typeId, int regionId)
        {
            var data = await GetHistoryAsync(regionId, typeId);
            if (data.NotNullOrEmpty())
            {
                return data.Select(p => new Core.Models.Market.Statistic(p, typeId)).ToList();
            }
            else
            {
                Core.Log.Info($"获取{regionId} {typeId}历史记录为空");
                return null;
            }
        }
        public async Task<Dictionary<int, List<Core.Models.Market.Statistic>>> GetHistoryAsync(List<int> typeId, int regionId, PageCallBackDelegate pageCallBack = null)
        {
            int doneCount = 0;
            object locker = new object();
            async Task<List<Core.Models.Market.Statistic>> getHistory(int[] parms)
            {
                var result =  await GetHistory(parms);
                lock(locker)
                {
                    doneCount++;
                    pageCallBack?.Invoke(doneCount, "History");
                }
                return result;
            }
            var result = await Core.Helpers.ThreadHelper.RunAsync(typeId.Select(p=> new int[] { regionId, p}), MaxThread, getHistory);
            var valid = result?.Where(p => p.NotNullOrEmpty()).ToList();
            Dictionary<int, List<Core.Models.Market.Statistic>> dic = new Dictionary<int, List<Core.Models.Market.Statistic>>();
            foreach(var list in valid)
            {
                var key = list.First().InvTypeId;
                dic.TryAdd(key, list);
            }
            return dic;
        }

        public class StructureOrder
        {
            public long StructureId { get; set; }
            public DateTime UpdateTime { get; set; }
            public List<Core.Models.Market.Order> Orders { get; set; }
            public string SaveFilePath
            {
                get => GetFilePath(StructureId);
            }
        }
        public class RegionOrder
        {
            public int RegionId { get; set; }
            public DateTime UpdateTime { get; set; }
            public List<Core.Models.Market.Order> Orders { get; set; }
            public string SaveFilePath
            {
                get => GetFilePath(RegionId);
            }
        }

        #region 个人订单
        /// <summary>
        /// 获取个人订单
        /// </summary>
        /// <param name="characterId"></param>
        /// <returns></returns>
        public async Task<List<Core.Models.Market.Order>> GetCharacterOrdersAsync(int characterId)
        {
            var character = CharacterService.GetCharacter(characterId);
            if (character == null)
            {
                Core.Log.Error($"未找到角色{characterId}");
                return null;
            }
            if (!character.IsTokenValid())
            {
                if (!await character.RefreshTokenAsync())
                {
                    Core.Log.Error("Token已过期，尝试刷新失败");
                    return null;
                }
            }
            EsiClient.SetCharacterData(character);
            var resp = await EsiClient.Market.CharacterOrders();
            if (resp != null && resp.StatusCode == System.Net.HttpStatusCode.OK)
            {
                if (resp.Data.Any())
                {
                    var orders = resp.Data.Select(p => new Core.Models.Market.Order(p)).ToList();
                    await SetOrderInfo(orders);
                    return orders;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                Core.Log.Error(resp?.Message);
                return null;
            }
        }
        /// <summary>
        /// 获取角色的军团订单
        /// </summary>
        /// <param name="characterId"></param>
        /// <returns></returns>
        public async Task<List<Core.Models.Market.Order>> GetCorpOrdersAsync(int characterId)
        {
            var character = CharacterService.GetCharacter(characterId);
            if (character == null)
            {
                Core.Log.Error($"未找到角色{characterId}");
                return null;
            }
            if (!character.IsTokenValid())
            {
                if (!await character.RefreshTokenAsync())
                {
                    Core.Log.Error("Token已过期，尝试刷新失败");
                    return null;
                }
            }
            EsiClient.SetCharacterData(character);
            List<Core.Models.Market.Order> orders = new List<Core.Models.Market.Order>();
            int page = 1;
            while (true)
            {
                var resp = await EsiClient.Market.CorporationOrders(page++);
                if (resp != null && resp.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    if (resp.Data.Any())
                    {
                        orders.AddRange(resp.Data.Select(p => new Core.Models.Market.Order(p)));
                        if (orders.Count < 1000)
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    Core.Log.Error(resp?.Message);
                    break;
                }
            }
            if (orders.Any())
            {
                await SetOrderInfo(orders);
            }
            return orders;
        }
        #endregion

        private async Task SetOrderInfo(List<Core.Models.Market.Order> orders)
        {
            if(orders.NotNullOrEmpty())
            {
                await SetTypeInfo(orders);
                await SetLocationInfo(orders);
                await SetSystemInfo(orders);
            }
        }
        private async Task SetTypeInfo(List<Core.Models.Market.Order> orders)
        {
            if (orders.NotNullOrEmpty())
            {
                var ids = orders.Select(p => p.TypeId).Distinct().ToList();
                var types = await Core.Services.DB.InvTypeService.QueryTypesAsync(ids);
                var typesDic = types.ToDictionary(p => p.TypeID);
                foreach ( var order in orders)
                {
                    if(typesDic.TryGetValue(order.TypeId,out var type))
                    {
                        order.InvType = type;
                    }
                }
            }
        }
        private void SetTypeInfo(List<Core.Models.Market.Order> orders, int typeId)
        {
            if (orders.NotNullOrEmpty())
            {
                var type = Core.Services.DB.InvTypeService.QueryType(typeId);
                foreach (var order in orders)
                {
                    order.InvType = type;
                }
            }
        }
        private async Task SetSystemInfo(List<Core.Models.Market.Order> orders)
        {
            if(orders.NotNullOrEmpty())
            {
                var systems = await Core.Services.DB.MapSolarSystemService.QueryAsync(orders.Select(p => (int)p.SystemId).Distinct().ToList());
                var systemsDic = systems.ToDictionary(p => p.SolarSystemID);
                foreach (var order in orders)
                {
                    if (systemsDic.TryGetValue((int)order.SystemId, out var system))
                    {
                        order.SolarSystem = system;
                        order.RegionId = system.RegionID;
                    }
                }
            }
        }
        private async Task SetLocationInfo(List<Core.Models.Market.Order> orders)
        {
            var stationOrders = orders.Where(p => p.IsStation).ToList();
            var structureOrders = orders.Where(p => !p.IsStation).ToList();
            if (stationOrders.NotNullOrEmpty())
            {
                var stations = await Core.Services.DB.StaStationService.QueryAsync(stationOrders.Select(p => (int)p.LocationId).ToList());
                var stationsDic = stations.ToDictionary(p => p.StationID);
                foreach (var order in stationOrders)
                {
                    if (stationsDic.TryGetValue((int)order.LocationId, out var station))
                    {
                        order.LocationName = station.StationName;
                        order.SystemId = station.SolarSystemID;//避免个人订单只有LocationId没有SystemId
                    }
                }
            }
            if (structureOrders.NotNullOrEmpty())
            {
                var c = await Services.CharacterService.GetDefaultCharacterAsync();
                if (c != null)
                {
                    EsiClient.SetCharacterData(c);
                    var result = await Core.Helpers.ThreadHelper.RunAsync(structureOrders.Select(p => p.LocationId).Distinct(), MaxThread, GetStructure);
                    var data = result?.Where(p => p != null).ToList();
                    var structuresDic = data.ToDictionary(p => p.Id);
                    foreach (var order in structureOrders)
                    {
                        if (structuresDic.TryGetValue(order.LocationId, out var structure))
                        {
                            order.LocationName = structure.Name;
                            order.SystemId = structure.SolarSystemId;//避免个人订单只有LocationId没有SystemId
                        }
                    }
                }
                else
                {
                    foreach (var order in structureOrders)
                    {
                        order.LocationName = order.LocationId.ToString();
                    }
                }
            }
        }


        public delegate void PageCallBackDelegate(int page, string tag);
    }
}
