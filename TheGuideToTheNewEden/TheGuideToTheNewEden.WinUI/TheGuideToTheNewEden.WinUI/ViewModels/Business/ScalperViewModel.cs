using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TheGuideToTheNewEden.Core.Models.Market;
using static TheGuideToTheNewEden.Core.Models.Market.ScalperSetting;
using TheGuideToTheNewEden.Core.Extensions;
using CommunityToolkit.WinUI.UI.Controls.TextToolbarSymbols;
using SqlSugar;

namespace TheGuideToTheNewEden.WinUI.ViewModels.Business
{
    public class ScalperViewModel:BaseViewModel
    {
        private ScalperSetting setting;
        public ScalperSetting Setting
        {
            get => setting;
            set => SetProperty(ref setting, value);
        }

        private int buyPriceType;
        public int BuyPriceType
        {
            get => buyPriceType;
            set
            {
                SetProperty(ref buyPriceType, value);
                Setting.BuyPrice = (PriceType)value;
            }
        }

        private int sellPriceType;
        public int SellPriceType
        {
            get => sellPriceType;
            set
            {
                SetProperty(ref sellPriceType, value);
                Setting.SellPrice = (PriceType)value;
            }
        }

        private List<Core.DBModels.InvMarketGroup> invMarketGroups;
        public List<Core.DBModels.InvMarketGroup> InvMarketGroups
        {
            get => invMarketGroups;
            set
            {
                SetProperty(ref invMarketGroups, value);
            }
        }

        private Core.DBModels.InvMarketGroup selectedInvMarketGroup;
        public Core.DBModels.InvMarketGroup SelectedInvMarketGroup
        {
            get => selectedInvMarketGroup;
            set
            {
                SetProperty(ref selectedInvMarketGroup, value);
            }
        }

        public ScalperViewModel()
        {
            Init();
        }
        private static readonly string SettingFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "ScalperSetting.json");
        private static readonly string SettingFolderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs");
        private async void Init()
        {
            if(System.IO.File.Exists(SettingFilePath))
            {
                string json = System.IO.File.ReadAllText(SettingFilePath);
                if(!string.IsNullOrEmpty(json))
                {
                    Setting = JsonConvert.DeserializeObject<ScalperSetting>(json);
                }
            }
            Setting ??= new ScalperSetting();
            InvMarketGroups = await Core.Services.DB.InvMarketGroupService.QueryRootGroupAsync();
            SelectedInvMarketGroup = InvMarketGroups.FirstOrDefault(p => p.MarketGroupID == Setting.MarketGroup);
        }

        public ICommand StartCommand => new RelayCommand(async() =>
        {
            Window?.ShowWaiting();
            if(IsValid())
            {
                SaveSetting();
                await Start();
            }
            Window?.HideWaiting();
        });
        private void SaveSetting()
        {
            if (!System.IO.Directory.Exists(SettingFolderPath))
            {
                System.IO.Directory.CreateDirectory(SettingFolderPath);
            }
            string json = JsonConvert.SerializeObject(Setting);
            System.IO.File.WriteAllText(SettingFilePath, json);
        }


        private bool IsValid()
        {
            if(Setting.SourceMarketLocation == null)
            {
                Window?.ShowError("请选择源市场");
                return false;
            }
            if(Setting.DestinationMarketLocation == null)
            {
                Window?.ShowError("请选择目的市场");
                return false;
            }
            if(SelectedInvMarketGroup == null)
            {
                Window?.ShowError("请选择物品类型");
                return false;
            }
            return true;
        }

        private async Task<List<Core.Models.Market.Order>> GetAllSourceOrders()
        {
            List<Core.Models.Market.Order> orders = null;
            switch(Setting.SourceMarketLocation.Type)
            {
                case MarketLocationType.Region:
                    {
                        orders = await Services.MarketOrderService.Current.GetRegionOrdersAsync((int)Setting.SourceMarketLocation.Id);
                    }
                    break;
                case MarketLocationType.SolarSystem:
                    {
                        orders = await Services.MarketOrderService.Current.GetMapSolarSystemOrdersAsync((int)Setting.SourceMarketLocation.Id);
                    }
                    break;
                case MarketLocationType.Structure:
                    {
                        orders = await Services.MarketOrderService.Current.GetStructureOrdersAsync(Setting.SourceMarketLocation.Id);
                    }
                    break;
            }
            return orders;
        }
        private async Task<List<Core.Models.Market.Order>> GetAllDestinationOrders()
        {
            List<Core.Models.Market.Order> orders = null;
            switch (Setting.DestinationMarketLocation.Type)
            {
                case MarketLocationType.Region:
                    {
                        orders = await Services.MarketOrderService.Current.GetRegionOrdersAsync((int)Setting.DestinationMarketLocation.Id);
                    }
                    break;
                case MarketLocationType.SolarSystem:
                    {
                        orders = await Services.MarketOrderService.Current.GetMapSolarSystemOrdersAsync((int)Setting.DestinationMarketLocation.Id);
                    }
                    break;
                case MarketLocationType.Structure:
                    {
                        orders = await Services.MarketOrderService.Current.GetStructureOrdersAsync(Setting.DestinationMarketLocation.Id);
                    }
                    break;
            }
            return orders;
        }
        private async Task Start()
        {
            var allSourceOrders = await GetAllSourceOrders();
            var allDestinationOrders = await GetAllDestinationOrders();
            if (allSourceOrders.NotNullOrEmpty() && allDestinationOrders.NotNullOrEmpty())
            {
                var subGroupsOfSelectedInvMarketGroup = await GetSubGroupOfTargetGroup();
                List<int> sourceTypeIds = null;
                List<int> destinationTypeIds = null;
                List<ScalperItem> sourceScalperItems = null;
                List<ScalperItem> destinationScalperItems = null;
                await Task.Run(() =>
                {
                    var subGroupsIds = subGroupsOfSelectedInvMarketGroup.Select(p => p.MarketGroupID).ToHashSet2();
                    sourceScalperItems = GetScalperItems(allSourceOrders, subGroupsIds);
                    destinationScalperItems = GetScalperItems(allDestinationOrders, subGroupsIds);
                    sourceTypeIds = sourceScalperItems.Select(p=>p.InvType.TypeID).ToList();
                    destinationTypeIds = destinationScalperItems.Select(p => p.InvType.TypeID).ToList();
                });
                if(sourceScalperItems.NotNullOrEmpty() && destinationScalperItems.NotNullOrEmpty())
                {
                    var sourceHistory = await Services.MarketOrderService.Current.GetHistoryAsync(sourceTypeIds.Distinct().ToList(), Setting.SourceMarketLocation.RegionId);
                    var destinationHistory = await Services.MarketOrderService.Current.GetHistoryAsync(destinationTypeIds.Distinct().ToList(), Setting.DestinationMarketLocation.RegionId);
                    await Task.Run(() =>
                    {
                        SetHistory(sourceScalperItems, sourceHistory);
                        SetHistory(destinationScalperItems, destinationHistory);
                        sourceScalperItems = sourceScalperItems.Where(p=>p.Statistics .NotNullOrEmpty()).ToList();
                        destinationScalperItems = destinationScalperItems.Where(p => p.Statistics.NotNullOrEmpty()).ToList();
                    });
                }
            }
        }

        private List<ScalperItem> GetScalperItems(List<Order> orders, HashSet<int> marketGroupIDs)
        {
            try
            {
                List<ScalperItem> scalperItems = new List<ScalperItem>();
                var groups = orders.GroupBy(p => p.TypeId);
                foreach (var group in groups)
                {
                    var list = group.ToList();
                    var invType = list.First().InvType;
                    if (invType?.MarketGroupID != null)
                    {
                        if (marketGroupIDs.Contains((int)invType.MarketGroupID))
                        {
                            scalperItems.Add(new ScalperItem()
                            {
                                InvType = list.First().InvType,
                                SellOrders = list.Where(p => !p.IsBuyOrder).OrderBy(p=>p.Price).ToList(),
                                BuyOrders = list.Where(p => p.IsBuyOrder).OrderByDescending(p=>p.Price).ToList(),
                            });
                        }
                    }
                }
                return scalperItems;
            }
            catch(Exception ex)
            {
                Core.Log.Error(ex);
                return null;
            }
        }
        private void SetHistory(List<ScalperItem> scalperItems, Dictionary<int, List<Core.Models.Market.Statistic>> statisticDic)
        {
            foreach(var item in scalperItems)
            {
                if(statisticDic.TryGetValue(item.InvType.TypeID, out var value))
                {
                    item.Statistics = value;
                }
            }
        }
        private async Task<List<Core.DBModels.InvMarketGroup>> GetSubGroupOfTargetGroup()
        {
            var subGroups = await Core.Services.DB.InvMarketGroupService.QuerySubGroupAsync();
            return await Task.Run(() =>
            {
                List<Core.DBModels.InvMarketGroup> targetroups = new List<Core.DBModels.InvMarketGroup>();
                foreach (var group in subGroups)
                {
                    var top = GetTopGroup(subGroups, group);
                    if (top.ParentGroupID == SelectedInvMarketGroup.MarketGroupID)
                    {
                        targetroups.Add(group);
                    }
                }
                return targetroups;
            });
        }
        private Core.DBModels.InvMarketGroup GetTopGroup(List<Core.DBModels.InvMarketGroup> subGroups, Core.DBModels.InvMarketGroup targetGroup)
        {
            var parent = subGroups.FirstOrDefault(p=>p.MarketGroupID == targetGroup.ParentGroupID);
            if(parent == null)
            {
                return targetGroup;
            }
            else
            {
                return GetTopGroup(subGroups, parent);
            }
        }

        private async Task Cal(List<ScalperItem> sourceScalperItems, List<ScalperItem> destinationScalperItems)
        {
            await Task.Run(() =>
            { 
                CalSourceSales(sourceScalperItems);
                CalDestinationSales(destinationScalperItems);
            });

        }
        /// <summary>
        /// 计算源市场日销量
        /// </summary>
        /// <param name="items"></param>
        private void CalSourceSales(List<ScalperItem> items)
        {
            foreach (var item in items)
            {
                var history = item.Statistics.Where(p=>p.Date > DateTime.Now.AddDays(- Setting.SourceSalesDay)).ToList();
                if(history.NotNullOrEmpty())
                {
                    if(history.Count > 3)
                    {
                        if (Setting.SourceRemoveExtremum)
                        {
                            var order = history.OrderBy(p => p.Volume);
                            history.Remove(order.First());
                            history.Remove(order.Last());
                        }
                    }
                    item.SourceSales = (long)Math.Ceiling(history.Sum(p => p.Volume) / (decimal)history.Count);
                }
            }
        }
        /// <summary>
        /// 计算源市场日销量
        /// </summary>
        /// <param name="items"></param>
        private void CalDestinationSales(List<ScalperItem> items)
        {
            foreach (var item in items)
            {
                var history = item.Statistics.Where(p => p.Date > DateTime.Now.AddDays(-Setting.DestinationSalesDay)).ToList();
                if (history.NotNullOrEmpty())
                {
                    if (history.Count > 3)
                    {
                        if (Setting.DestinationRemoveExtremum)
                        {
                            var order = history.OrderBy(p => p.Volume);
                            history.Remove(order.First());
                            history.Remove(order.Last());
                        }
                    }
                    item.SourceSales = (long)Math.Ceiling(history.Sum(p => p.Volume) / (decimal)history.Count);
                }
            }
        }
        private void CalSellPrice(List<ScalperItem> items)
        {
            CalPrice calPrice;
            switch(Setting.SellPrice)
            {
                case PriceType.SellTop5: calPrice = CalPriceSellTop5;break;
            }
        }
        private delegate double CalPrice(ScalperItem item);
        /// <summary>
        /// 卖单前5%最低价格订单平均价格
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private double CalPriceSellTop5(ScalperItem item)
        {
            int top5P = (int)(item.SellOrders.Count() * 0.05);
            if (top5P > 1)
            {
                return (double)item.SellOrders.Take(top5P).Average(p => p.Price);
            }
            else
            {
                return (double)item.SellOrders.FirstOrDefault()?.Price;
            }
        }
        /// <summary>
        /// 卖单实际数量的相应价格
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private double CalPriceSellAvailable(ScalperItem item)
        {
            decimal price = 0;
            long count = 0;
            foreach (var order in item.SellOrders)
            {
                long takeVolume = count + order.VolumeRemain > item.DestinationSales ? item.DestinationSales - count : order.VolumeRemain;
                count += takeVolume;
                price += takeVolume * order.Price;
                if(count == item.DestinationSales)
                {
                    break;
                }
            }
            return (double)price / count;
        }
    }
}
