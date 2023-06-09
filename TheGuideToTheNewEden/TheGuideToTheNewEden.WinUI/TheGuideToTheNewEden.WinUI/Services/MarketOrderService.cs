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

namespace TheGuideToTheNewEden.WinUI.Services
{
    public class MarketOrderService
    {
        private static readonly string FolderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "StructureOrders");
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

        private ConcurrentDictionary<long, StructureOrder> StructureOrders = new ConcurrentDictionary<long, StructureOrder>();
        /// <summary>
        /// 订单过期时间
        /// 分钟
        /// </summary>
        private static readonly int OrderDuration = 60;

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

        /// <summary>
        /// 获取建筑所有订单，优先从缓存获取，缓存不存在或过期时自动刷新
        /// </summary>
        /// <param name="structureId"></param>
        /// <returns></returns>
        public async Task<List<Core.Models.Market.Order>> GetStructureOrdersAsync(long structureId)
        {
            if(StructureOrders.TryGetValue(structureId, out StructureOrder order))
            {
                if((DateTime.Now - order.UpdateTime).TotalMinutes > OrderDuration)
                {
                    var orders = await GetLatestStructureOrdersAsync(structureId);
                    if(orders.NotNullOrEmpty())
                    {
                        StructureOrders.TryRemove(structureId,out _);
                        var newOrder = new StructureOrder()
                        {
                            StructureId = structureId,
                            UpdateTime = DateTime.Now,
                            Orders = orders
                        };
                        StructureOrders.TryAdd(structureId, newOrder);
                        Save(newOrder);
                    }
                    return orders;
                }
                else
                {
                    return order.Orders;
                }
            }
            else
            {
                //优先从本地加载
                string filePath = GetFilePath(structureId);
                if(File.Exists(filePath))
                {
                    var fileText = File.ReadAllText(filePath);
                    if(!string.IsNullOrEmpty(fileText))
                    {
                        var localOrder = JsonConvert.DeserializeObject<StructureOrder>(fileText);
                        if(localOrder != null)//本地存在订单数据
                        {
                            if ((DateTime.Now - localOrder.UpdateTime).TotalMinutes < OrderDuration)//还在有效期内
                            {
                                var sturcture = StructureService.GetStructure(structureId);
                                if(sturcture != null)
                                {
                                    var mapSolarSystem = Core.Services.DB.MapSolarSystemService.Query(sturcture.SolarSystemId);
                                    localOrder.Orders.ForEach(p => p.SolarSystem = mapSolarSystem);
                                }
                                StructureOrders.TryAdd(structureId, localOrder);
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
                var orders = await GetLatestStructureOrdersAsync(structureId);
                if (orders.NotNullOrEmpty())
                {
                    var newOrder = new StructureOrder()
                    {
                        StructureId = structureId,
                        UpdateTime = DateTime.Now,
                        Orders = orders
                    };
                    StructureOrders.TryAdd(structureId, newOrder);
                    Save(newOrder);
                }
                return orders;
            }
        }

        /// <summary>
        /// 实时获取建筑所有订单
        /// 不使用缓存
        /// </summary>
        /// <param name="structureId"></param>
        /// <returns></returns>
        public async Task<List<Core.Models.Market.Order>> GetLatestStructureOrdersAsync(long structureId)
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
            int page = 1;
            while (true)
            {
                var resp = await EsiClient.Market.StructureOrders(structureId, page++);
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
            var mapSolarSystem = Core.Services.DB.MapSolarSystemService.Query(structure.SolarSystemId);
            foreach(var order in orders)
            {
                order.LocationName = structure.Name;
                order.SolarSystem = mapSolarSystem;
            }
            return orders;
        }

        private void Save(StructureOrder order)
        {
            string json = JsonConvert.SerializeObject(order);
            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
            }
            File.WriteAllText(order.SaveFilePath, json);
        }

        private static string GetFilePath(long structureId)
        {
            return System.IO.Path.Combine(FolderPath, $"{structureId}.json");
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
                #region 赋值物品、星系信息
                var type = Core.Services.DB.InvTypeService.QueryType(typeId);
                var systems = await Core.Services.DB.MapSolarSystemService.QueryAsync(orders.Select(p => (int)p.SystemId).Distinct().ToList());
                var systemsDic = systems.ToDictionary(p => p.SolarSystemID);
                foreach (var order in orders)
                {
                    order.InvType = type;
                    if (systemsDic.TryGetValue((int)order.SystemId, out var system))
                    {
                        order.SolarSystem = system;
                    }
                }
                #endregion
                #region 赋值空间站、建筑星系
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
                        }
                    }
                }
                if (structureOrders.NotNullOrEmpty())
                {
                    var c = await Services.CharacterService.GetDefaultCharacterAsync();
                    if(c != null)
                    {
                        EsiClient.SetCharacterData(c);
                        var result = await Core.Helpers.ThreadHelper.RunAsync(structureOrders.Select(p => p.LocationId).Distinct(), GetStructure);
                        var data = result?.Where(p => p != null).ToList();
                        var structuresDic = data.ToDictionary(p => p.Id);
                        foreach (var order in structureOrders)
                        {
                            if (structuresDic.TryGetValue(order.LocationId, out var structure))
                            {
                                order.LocationName = structure.Name;
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
                #endregion
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
                var result = await Core.Helpers.ThreadHelper.RunAsync(strutures.Select(p=>p.Id), GetStructureOrdersAsync);
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

        public async Task<List<ESI.NET.Models.Market.Statistic>> GetHistory(int typeId, int regionId)
        {
            var resp = await EsiClient.Market.TypeHistoryInRegion(regionId, typeId);
            if (resp != null && resp.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return resp.Data;
            }
            else
            {
                Core.Log.Error(resp?.Message);
                return null;
            }
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
    }
}
