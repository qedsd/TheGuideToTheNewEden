﻿using CommunityToolkit.Mvvm.Input;
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
using TheGuideToTheNewEden.WinUI.Wins;
using Syncfusion.UI.Xaml.Data;
using System.Collections.ObjectModel;
using TheGuideToTheNewEden.Core.DBModels;
using WinUIEx;
using TheGuideToTheNewEden.WinUI.Services;
using System.Diagnostics;

namespace TheGuideToTheNewEden.WinUI.ViewModels.Business
{
    public class ScalperViewModel:BaseViewModel
    {
        private ObservableCollection<InvType> filterTypes = new ObservableCollection<InvType>();
        public ObservableCollection<InvType> FilterTypes
        {
            get => filterTypes;
            set => SetProperty(ref filterTypes, value);
        }

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

        private int sourceSalesType;
        public int SourceSalesType
        {
            get => sourceSalesType;
            set
            {
                if(SetProperty(ref sourceSalesType, value))
                {
                    Setting.SourceSalesType = (SalesType)value;
                }
            }
        }

        private int destinationSalesType;
        public int DestinationSalesType
        {
            get => destinationSalesType;
            set
            {
                if (SetProperty(ref destinationSalesType, value))
                {
                    Setting.DestinationSalesType = (SalesType)value;
                }
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

        private List<Core.DBModels.InvMarketGroup> selectedInvMarketGroups;
        public List<Core.DBModels.InvMarketGroup> SelectedInvMarketGroups
        {
            get => selectedInvMarketGroups;
            set
            {
                SetProperty(ref selectedInvMarketGroups, value);
            }
        }

        private List<ScalperItem> scalperItems;
        public List<ScalperItem> ScalperItems
        {
            get => scalperItems;
            set
            {
                SetProperty(ref scalperItems, value);
            }
        }

        private ScalperItem selectedScalperItem;
        public ScalperItem SelectedScalperItem
        {
            get => selectedScalperItem;
            set
            {
                if(SetProperty(ref selectedScalperItem, value) && value != null)
                {
                    LoadItemDetail();
                }
            }
        }

        private Views.TabViewBasePage _page;
        public ScalperViewModel()
        {
            
        }
        private static readonly string SettingFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "ScalperSetting.json");
        private static readonly string SettingFolderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs");
        public async Task Init()
        {
            _page = NavigationService.GetCurrentPage();
            if (System.IO.File.Exists(SettingFilePath))
            {
                string json = System.IO.File.ReadAllText(SettingFilePath);
                if(!string.IsNullOrEmpty(json))
                {
                    Setting = JsonConvert.DeserializeObject<ScalperSetting>(json);
                }
            }
            Setting ??= new ScalperSetting();
            BuyPriceType = (int)Setting.BuyPrice;
            SellPriceType = (int)Setting.SellPrice;
            SourceSalesType = (int)Setting.SourceSalesType;
            DestinationSalesType = (int)Setting.DestinationSalesType;
            InvMarketGroups = await Core.Services.DB.InvMarketGroupService.QueryRootGroupAsync();
            if(Setting.MarketGroups.NotNullOrEmpty())
            {
                SelectedInvMarketGroups = InvMarketGroups.Where(p => Setting.MarketGroups.Contains(p.MarketGroupID)).ToList();
            }
            else
            {
                SelectedInvMarketGroups = new List<InvMarketGroup>();
            }
        }

        public ICommand AnalyseCommand => new RelayCommand(async() =>
        {
            if(ScalperItems.NotNullOrEmpty())
            {
                Window?.ShowWaiting(_page, "计算中");
                if (IsValid())
                {
                    SaveSetting();
                    await Cal();
                    Window?.ShowSuccess($"完成{ScalperItems.Count}个订单计算");
                }
                Window?.HideWaiting(_page);
            }
            else
            {
                Window?.ShowError("请先获取订单");
            }
        });
        public ICommand GetOrdersCommand => new RelayCommand(async () =>
        {
            Window?.ShowWaiting(_page, "获取订单中");
            try
            {
                if (IsValid())
                {
                    SaveSetting();
                    await GetOrders();
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                Window?.ShowError(ex.Message);
            }
            Window?.HideWaiting(_page);
        });
        private void SaveSetting()
        {
            if (!System.IO.Directory.Exists(SettingFolderPath))
            {
                System.IO.Directory.CreateDirectory(SettingFolderPath);
            }
            Setting.MarketGroups = SelectedInvMarketGroups.Select(p=>p.MarketGroupID).ToArray();
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
            if(!SelectedInvMarketGroups.Any())
            {
                Window?.ShowError("请选择物品类型");
                return false;
            }
            return true;
        }

        private ScalperItemDetailWindow itemDetailWindow;
        private async void LoadItemDetail()
        {
            if(SelectedScalperItem != null)
            {
                if (itemDetailWindow == null)
                {
                    itemDetailWindow = new ScalperItemDetailWindow();
                    itemDetailWindow.Closed += ItemDetailWindow_Closed;
                }
                await Task.Delay(100);
                itemDetailWindow.SetItem(SelectedScalperItem);
                itemDetailWindow.Activate();
            }
        }

        private void ItemDetailWindow_Closed(object sender, Microsoft.UI.Xaml.WindowEventArgs args)
        {
            itemDetailWindow = null;
        }
        private void GetSourceOrdersPageCallBack(int page, string tag)
        {
            Window.DispatcherQueue.TryEnqueue(() =>
            {
                Window?.ShowWaiting(_page, $"获取源市场订单中（{page}页）");
            });
        }
        private void GetDestinationOrdersPageCallBack(int page, string tag)
        {
            Window.DispatcherQueue.TryEnqueue(() =>
            {
                Window?.ShowWaiting(_page, $"获取目的市场订单中（{page}页）");
            });
        }
        private void GetSourceHistoryPageCallBack(int page, string tag)
        {
            Window.DispatcherQueue.TryEnqueue(() =>
            {
                Window?.ShowWaiting(_page, $"获取源市场订单历史中（{page}/{_typeCount}）");
            });
        }
        private void GetDestinationHistoryPageCallBack(int page, string tag)
        {
            Window.DispatcherQueue.TryEnqueue(() =>
            {
                Window?.ShowWaiting(_page, $"获取目的市场订单历史中（{page}/{_typeCount}）");
            });
        }
        private async Task<List<Core.Models.Market.Order>> GetAllSourceOrders()
        {
            List<Core.Models.Market.Order> orders = null;
            switch (Setting.SourceMarketLocation.Type)
            {
                case MarketLocationType.Region:
                    {
                        orders = await Services.MarketOrderService.Current.GetRegionOrdersAsync((int)Setting.SourceMarketLocation.Id, GetSourceOrdersPageCallBack);
                    }
                    break;
                case MarketLocationType.SolarSystem:
                    {
                        orders = await Services.MarketOrderService.Current.GetMapSolarSystemOrdersAsync((int)Setting.SourceMarketLocation.Id, GetSourceOrdersPageCallBack);
                    }
                    break;
                case MarketLocationType.Structure:
                    {
                        orders = await Services.MarketOrderService.Current.GetStructureOrdersAsync(Setting.SourceMarketLocation.Id, GetSourceOrdersPageCallBack);
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
                        orders = await Services.MarketOrderService.Current.GetRegionOrdersAsync((int)Setting.DestinationMarketLocation.Id, GetDestinationOrdersPageCallBack);
                    }
                    break;
                case MarketLocationType.SolarSystem:
                    {
                        orders = await Services.MarketOrderService.Current.GetMapSolarSystemOrdersAsync((int)Setting.DestinationMarketLocation.Id, GetDestinationOrdersPageCallBack);
                    }
                    break;
                case MarketLocationType.Structure:
                    {
                        orders = await Services.MarketOrderService.Current.GetStructureOrdersAsync(Setting.DestinationMarketLocation.Id, GetDestinationOrdersPageCallBack);
                    }
                    break;
            }
            return orders;
        }
        private int _typeCount = 0;
        private async Task GetOrders()
        {
            long errorCount = Core.Log.GetErrorCount();
            long infoCount = Core.Log.GetInfoCount();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            List<Order> allSourceOrders = null;
            List<Order> allDestinationOrders = null;
            Window?.ShowWaiting(_page, "获取源市场订单中");
            try
            {
                allSourceOrders = await GetAllSourceOrders();
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                Window?.ShowError($"获取源市场订单出错：{ex.Message}");
                return;
            }
            Window?.ShowWaiting(_page, "获取目的市场订单中");
            try
            {
                allDestinationOrders = await GetAllDestinationOrders();
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                Window?.ShowError($"获取目的市场订单出错：{ex.Message}");
            }
            if (FilterTypes.NotNullOrEmpty())//移除过滤物品
            {
                await Task.Run(() =>
                {
                    allSourceOrders = RemoveFilterTypes(allSourceOrders);
                    allDestinationOrders = RemoveFilterTypes(allDestinationOrders);
                });
            }
            if (allSourceOrders.NotNullOrEmpty() && allDestinationOrders.NotNullOrEmpty())
            {
                var subGroupsOfSelectedInvMarketGroup = await GetSubGroupOfTargetGroup();
                List<int> typeIds = null;
                List<ScalperItem> scalperItems = null;
                await Task.Run(() =>
                {
                    var subGroupsIds = subGroupsOfSelectedInvMarketGroup.Select(p => p.MarketGroupID).ToHashSet2();
                    scalperItems = GetScalperItems(allSourceOrders, allDestinationOrders,subGroupsIds);
                    typeIds = scalperItems.Select(p=>p.InvType.TypeID).ToList().Distinct().ToList();
                });
                _typeCount = typeIds.Count;
                int sourceNoHistoryCount = -1;
                int destinatioNoHistoryCount = -1;
                if (scalperItems.NotNullOrEmpty())
                {
                    Window?.ShowWaiting(_page, "获取源市场订单历史中");
                    var sourceHistory = await Services.MarketOrderService.Current.GetHistoryBatchAsync(typeIds, Setting.SourceMarketLocation.RegionId,GetSourceHistoryPageCallBack);
                    sourceNoHistoryCount = typeIds.Count - sourceHistory.Count;
                    Window?.ShowWaiting(_page, "获取目的市场订单历史中");
                    var destinationHistory = await Services.MarketOrderService.Current.GetHistoryBatchAsync(typeIds, Setting.DestinationMarketLocation.RegionId, GetDestinationHistoryPageCallBack);
                    destinatioNoHistoryCount = typeIds.Count - destinationHistory.Count;
                    Window?.ShowWaiting(_page, "匹配订单历史中");
                    await Task.Run(() =>
                    {
                        SetHistory(scalperItems, sourceHistory, destinationHistory);
                        scalperItems = scalperItems.Where(p => p.SourceStatistics.NotNullOrEmpty() && p.DestinationStatistics.NotNullOrEmpty()).ToList();
                    });
                }
                ScalperItems = scalperItems;
                stopwatch.Stop();
                long errorCount2 = Core.Log.GetErrorCount();
                long infoCount2 = Core.Log.GetInfoCount();
                Window?.ShowSuccess($"已获取到{ScalperItems.Count}个有效物品订单(耗时：{stopwatch.Elapsed.TotalMinutes.ToString("N2")}分钟  错误：{errorCount2 - errorCount}  异常：{infoCount2 - infoCount}) 无历史记录：{sourceNoHistoryCount} + {destinatioNoHistoryCount}",false);
            }
            else
            {
                Window?.ShowError("未获取到有效订单");
            }
            Window?.HideWaiting(_page);
        }

        private List<Order> RemoveFilterTypes(List<Order> orders)
        {
            if(FilterTypes.NotNullOrEmpty() && orders.NotNullOrEmpty())
            {
                List<Order> newOrders = new List<Order>();
                var ids = FilterTypes.Select(p => p.TypeID).ToHashSet2();
                foreach(var o in orders)
                {
                    if(!ids.Contains(o.TypeId))
                    {
                        newOrders.Add(o);
                    }
                }
                return newOrders;
            }
            else
            {
                return orders;
            }
        }
        /// <summary>
        /// 由源市场目的市场订单和指定物品分组类型生成表示一个物品的实例
        /// 卖单卖单按最低价最高价顺序排序完毕
        /// </summary>
        /// <param name="sourceOrders"></param>
        /// <param name="destinationOrders"></param>
        /// <param name="marketGroupIDs"></param>
        /// <returns></returns>
        private List<ScalperItem> GetScalperItems(List<Order> sourceOrders, List<Order> destinationOrders, HashSet<int> marketGroupIDs)
        {
            try
            {
                List<ScalperItem> scalperItems = new List<ScalperItem>();
                var sourceGroups = sourceOrders.GroupBy(p => p.TypeId);
                foreach (var group in sourceGroups)
                {
                    var list = group.ToList();
                    var invType = list.First().InvType;
                    if (invType?.MarketGroupID != null)
                    {
                        if (marketGroupIDs.Contains((int)invType.MarketGroupID))
                        {
                            var item = new ScalperItem()
                            {
                                InvType = list.First().InvType,
                                SourceSellOrders = list.Where(p => !p.IsBuyOrder).OrderBy(p => p.Price).ToList(),
                                SourceBuyOrders = list.Where(p => p.IsBuyOrder).OrderByDescending(p => p.Price).ToList(),
                            };
                            scalperItems.Add(item);
                            if(item.SourceSellOrders == null || item.SourceBuyOrders == null)
                            {

                            }
                        }
                    }
                }
                var dic = destinationOrders.GroupBy(p => p.TypeId).ToDictionary(p => p.Key);
                foreach (var item in scalperItems)
                {
                    if(dic.TryGetValue(item.InvType.TypeID, out var group))
                    {
                        var list = group.ToList();
                        item.DestinationSellOrders = list.Where(p => !p.IsBuyOrder).OrderBy(p => p.Price).ToList();
                        item.DestinationBuyOrders = list.Where(p => p.IsBuyOrder).OrderBy(p => p.Price).ToList();
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
        private void SetHistory(List<ScalperItem> scalperItems, Dictionary<int, List<Core.Models.Market.Statistic>> sourceStatisticDic, Dictionary<int, List<Core.Models.Market.Statistic>> destinationStatisticDic)
        {
            foreach(var item in scalperItems)
            {
                if(sourceStatisticDic.TryGetValue(item.InvType.TypeID, out var value1))
                {
                    item.SourceStatistics = value1;
                }
                if (destinationStatisticDic.TryGetValue(item.InvType.TypeID, out var value2))
                {
                    item.DestinationStatistics = value2;
                }
            }
        }
        private async Task<List<Core.DBModels.InvMarketGroup>> GetSubGroupOfTargetGroup()
        {
            var subGroups = await Core.Services.DB.InvMarketGroupService.QuerySubGroupAsync();
            return await Task.Run(() =>
            {
                List<Core.DBModels.InvMarketGroup> targetroups = new List<Core.DBModels.InvMarketGroup>();
                var ids = SelectedInvMarketGroups.Select(p => p.MarketGroupID).ToHashSet2();
                foreach (var group in subGroups)
                {
                    var top = GetTopGroup(subGroups, group);
                    if (ids.Contains((int)top.ParentGroupID))
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

        private async Task Cal()
        {
            var items = ScalperItems.ToList();
            await Task.Run(() =>
            {
                try
                {
                    foreach(var item in items)
                    {
                        item.Suggestion = 0;
                        item.SourceSales = 0;
                        item.DestinationSales = 0;
                        item.SellPrice = 0;
                        item.BuyPrice = 0;
                        item.TargetSales = 0;
                        item.NetProfit = 0;
                        item.TargetNetProfit = 0;
                        item.ROI = 0;
                        item.HistoryPriceFluctuation = 0;
                        item.NowPriceFluctuation = 0;
                        item.Saturation = 0;
                        item.HeatValue = 0;
                    }
                    CalSales(items);
                    items = items.Where(p => p.DestinationSales > 0).ToList();
                    CalTargetSales(items);
                    CalSellPrice(items);
                    CalBuyPrice(items);
                    CalNetProfit(items);
                    CalROI(items);
                    CalPrincipal(items);
                    CalHistoryPriceFluctuation(items);
                    CalNowPriceFluctuation(items);
                    CalSaturation(items);
                    CalHeatValue(items);
                    CalSuggestion(items);
                    items = items.OrderByDescending(p => p.Suggestion).ToList();
                }
                catch(Exception ex)
                {
                    Core.Log.Error(ex);
                    Window?.ShowError(ex.Message);
                }
            });
            ScalperItems = items;
        }
        /// <summary>
        /// 计算市场日销量
        /// </summary>
        /// <param name="items"></param>
        private void CalSales(List<ScalperItem> items)
        {
            foreach (var item in items)
            {
                var history = item.SourceStatistics.Where(p=>p.Date > DateTime.Now.AddDays(- Setting.SourceSalesDay - 2)).ToList();
                if(history.NotNullOrEmpty())
                {
                    history = history.OrderBy(p => p.Volume).ToList();
                    if (history.Count > 3)
                    {
                        if (Setting.SourceRemoveExtremum)
                        {
                            history.RemoveAt(0);
                            history.RemoveAt(history.Count - 1);
                        }
                    }
                    switch(Setting.SourceSalesType)
                    {
                        case SalesType.HistoryLowest: item.SourceSales = history.FirstOrDefault().Volume; break;
                        case SalesType.HistoryHighest: item.SourceSales = history.LastOrDefault().Volume; break;
                        case SalesType.HistoryAverage: item.SourceSales = (long)Math.Ceiling(history.Sum(p => p.Volume) / (decimal)history.Count); break;
                    }
                }
                history = item.DestinationStatistics.Where(p => p.Date > DateTime.Now.AddDays(- Setting.DestinationSalesDay - 2)).ToList();
                if (history.NotNullOrEmpty())
                {
                    history = history.OrderBy(p => p.Volume).ToList();
                    if (history.Count > 3)
                    {
                        if (Setting.DestinationRemoveExtremum)
                        {
                            history.RemoveAt(0);
                            history.RemoveAt(history.Count - 1);
                        }
                    }
                    switch (Setting.DestinationSalesType)
                    {
                        case SalesType.HistoryLowest: item.DestinationSales = history.FirstOrDefault().Volume; break;
                        case SalesType.HistoryHighest: item.DestinationSales = history.LastOrDefault().Volume; break;
                        case SalesType.HistoryAverage: item.DestinationSales = (long)Math.Ceiling(history.Sum(p => p.Volume) / (decimal)history.Count); break;
                    }
                }
            }
        }

        private void CalSellPrice(List<ScalperItem> items)
        {
            CalPrice calPrice = null;
            bool isBuyOrders = true;
            switch (Setting.SellPrice)
            {
                case PriceType.SellTop5: calPrice = CalPriceTop5; isBuyOrders = false; break;
                case PriceType.SellAvailable: calPrice = CalPriceAvailable; isBuyOrders = false; break;
                case PriceType.SellTop: calPrice = CalPriceTop; isBuyOrders = false; break;
                case PriceType.BuyTop5: calPrice = CalPriceTop5; break;
                case PriceType.BuyAvailable: calPrice = CalPriceAvailable; break;
                case PriceType.BuyTop: calPrice = CalPriceTop; break;
                case PriceType.HistoryHighest: calPrice = CalPriceHistoryHighest; break;
                case PriceType.HistoryAverage: calPrice = CalPriceHistoryAverage; break;
                case PriceType.HistoryLowest: calPrice = CalPriceHistoryLowest; break;
                case PriceType.HistoryMedian: calPrice = CalPriceHistoryMedian; break;
            }
            foreach(var item in items)
            {
                try
                {
                    item.SellPrice = calPrice(isBuyOrders ? item.DestinationBuyOrders : item.DestinationSellOrders, item.DestinationStatistics, item.DestinationSales, Setting.SellHistoryDay + 2, Setting.SellPirceRemoveExtremum);
                    if(item.SellPrice <= 0 )
                    {
                        item.SellPrice = GetDefaultPrice(item);
                    }
                }
                catch(Exception ex)
                {
                    Core.Log.Error(ex);
                    item.SellPrice = GetDefaultPrice(item);
                }
                item.SellPrice *= Setting.SellPriceScale;
            }
        }
        private void CalBuyPrice(List<ScalperItem> items)
        {
            CalPrice calPrice = null;
            bool isBuyOrders = true;
            switch (Setting.BuyPrice)
            {
                case PriceType.SellTop5: calPrice = CalPriceTop5; isBuyOrders = false; break;
                case PriceType.SellAvailable: calPrice = CalPriceAvailable; isBuyOrders = false; break;
                case PriceType.SellTop: calPrice = CalPriceTop; isBuyOrders = false; break;
                case PriceType.BuyTop5: calPrice = CalPriceTop5; break;
                case PriceType.BuyAvailable: calPrice = CalPriceAvailable; break;
                case PriceType.BuyTop: calPrice = CalPriceTop; break;
                case PriceType.HistoryHighest: calPrice = CalPriceHistoryHighest; break;
                case PriceType.HistoryAverage: calPrice = CalPriceHistoryAverage; break;
                case PriceType.HistoryLowest: calPrice = CalPriceHistoryLowest; break;
                case PriceType.HistoryMedian: calPrice = CalPriceHistoryMedian; break;
            }
            foreach (var item in items)
            {
                try
                {
                    item.BuyPrice = calPrice(isBuyOrders ? item.SourceBuyOrders : item.SourceSellOrders, item.DestinationStatistics, item.DestinationSales, Setting.BuyHistoryDay + 2, Setting.BuyPirceRemoveExtremum); 
                    if (item.BuyPrice <= 0)
                    {
                        item.BuyPrice = GetDefaultPrice(item);
                    }
                }
                catch (Exception ex)
                {
                    Core.Log.Error(ex);
                    item.BuyPrice = GetDefaultPrice(item);
                }
                item.BuyPrice *= Setting.BuyPriceScale;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="orders">源市场或目的市场的卖单或买单</param>
        /// <param name="statistics">目的市场的历史记录</param>
        /// <param name="sales">目的市场日销量</param>
        /// <param name="day">买单/卖单选择为历史价格时计算的历史天数</param>
        /// <returns></returns>
        private delegate double CalPrice(List<Order> orders, List<Statistic> statistics, long sales, int day, bool removeExtremum);

        /// <summary>
        /// 最低/最高价格订单平均价格
        /// </summary>
        /// <param name="sellOrders">卖单</param>
        /// <param name="sales">销量</param>
        /// <returns></returns>
        private double CalPriceTop5(List<Order> orders, List<Statistic> statistics, long sales, int day, bool removeExtremum)
        {
            if(orders.NotNullOrEmpty())
            {
                int top5P = (int)(orders.Count * 0.05);
                if (top5P > 1)
                {
                    return (double)orders.Take(top5P).Average(p => p.Price);
                }
                else
                {
                    return (double)orders.FirstOrDefault()?.Price;
                }
            }
            else
            {
                return -1;
            }
        }
        /// <summary>
        /// 实际数量的相应价格
        /// </summary>
        /// <param name="sellOrders"></param>
        /// <param name="sales"></param>
        /// <returns></returns>
        private double CalPriceAvailable(List<Order> orders, List<Statistic> statistics, long sales, int day, bool removeExtremum)
        {
            if (orders.NotNullOrEmpty())
            {
                decimal price = 0;
                long count = 0;
                foreach (var order in orders)
                {
                    long takeVolume = count + order.VolumeRemain > sales ? sales - count : order.VolumeRemain;
                    count += takeVolume;
                    price += takeVolume * order.Price;
                    if (count == sales)
                    {
                        break;
                    }
                }
                return (double)price / count;
            }
            else
            {
                return -1;
            }
        }
        /// <summary>
        /// 最低/最高价格
        /// </summary>
        /// <param name="sellOrders"></param>
        /// <param name="sales"></param>
        /// <returns></returns>
        private double CalPriceTop(List<Order> orders, List<Statistic> statistics, long sales, int day, bool removeExtremum)
        {
            if (orders.NotNullOrEmpty())
                return (double)orders.First().Price;
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// 历史最高价格
        /// </summary>
        /// <param name="statistics"></param>
        /// <param name="day"></param>
        /// <returns></returns>
        private double CalPriceHistoryHighest(List<Order> orders, List<Statistic> statistics, long sales, int day, bool removeExtremum)
        {
            var history = statistics.Where(p => p.Date > DateTime.Now.AddDays(-day)).ToList();
            if (history.NotNullOrEmpty())
            {
                if(removeExtremum)
                {
                    return (long)Math.Ceiling((history.Sum(p => p.Highest) - history.Max(p=>p.Highest)) / (history.Count - 1));
                }
                else
                {
                    return (long)Math.Ceiling(history.Sum(p => p.Highest) / history.Count);
                }
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// 历史平均价格
        /// </summary>
        /// <param name="statistics"></param>
        /// <param name="day"></param>
        /// <returns></returns>
        private double CalPriceHistoryAverage(List<Order> orders, List<Statistic> statistics, long sales, int day, bool removeExtremum)
        {
            var history = statistics.Where(p => p.Date > DateTime.Now.AddDays(-day)).ToList();
            if (history.NotNullOrEmpty())
            {
                if(removeExtremum)
                {
                    return (long)Math.Ceiling((history.Sum(p => p.Average) - history.Max(p => p.Average)) / (history.Count - 1));
                }
                else
                {
                    return (long)Math.Ceiling(history.Sum(p => p.Average) / history.Count);
                }
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// 历史最低价格
        /// </summary>
        /// <param name="statistics"></param>
        /// <param name="day"></param>
        /// <returns></returns>
        private double CalPriceHistoryLowest(List<Order> orders, List<Statistic> statistics, long sales, int day, bool removeExtremum)
        {
            var history = statistics.Where(p => p.Date > DateTime.Now.AddDays(-day)).ToList();
            if (history.NotNullOrEmpty())
            {
                if (removeExtremum)
                {
                    return (long)Math.Ceiling((history.Sum(p => p.Lowest) - history.Max(p => p.Lowest)) / (history.Count - 1));
                }
                else
                {
                    return (long)Math.Ceiling(history.Sum(p => p.Lowest) / history.Count);
                }
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// 历史价格中位数
        /// </summary>
        /// <param name="statistics"></param>
        /// <param name="day"></param>
        /// <returns></returns>
        private double CalPriceHistoryMedian(List<Order> orders, List<Statistic> statistics, long sales, int day, bool removeExtremum)
        {
            var history = statistics.Where(p => p.Date > DateTime.Now.AddDays(-day)).ToList();
            if (history.NotNullOrEmpty())
            {
                List<decimal> decimals = history.Select(p => p.Lowest).ToList();
                decimals.AddRange(history.Select(p => p.Highest).ToList());
                decimals = decimals.OrderBy(p=>p).ToList();
                var avag = (decimals[decimals.Count / 2 - 1] + decimals[decimals.Count / 2]) / 2;
                return (double)avag;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// 无法计算出优先设定的价格时，使用历史价格代替
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private double GetDefaultPrice(ScalperItem item)
        {
            if (item.DestinationStatistics.NotNullOrEmpty())
            {
                return (double)item.DestinationStatistics.Last().Average;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// 目标销量
        /// </summary>
        /// <param name="items"></param>
        private void CalTargetSales(List<ScalperItem> items)
        {
            foreach (var item in items)
            {
                item.TargetSales = (long)Math.Ceiling(item.DestinationSales / 100f * Setting.SalesPercent);
            }
        }
        /// <summary>
        /// 净利润
        /// </summary>
        /// <param name="items"></param>
        private void CalNetProfit(List<ScalperItem> items)
        {
            foreach(var item in items)
            {
                item.NetProfit = item.SellPrice - item.BuyPrice;
                item.TargetNetProfit = item.NetProfit * item.TargetSales;
            }
        }

        /// <summary>
        /// 回报率
        /// </summary>
        /// <param name="items"></param>
        private void CalROI(List<ScalperItem> items)
        {
            foreach (var item in items)
            {
                item.ROI = item.NetProfit / item.BuyPrice * 100;
            }
        }

        /// <summary>
        /// 本金
        /// </summary>
        /// <param name="items"></param>
        private void CalPrincipal(List<ScalperItem> items)
        {
            foreach (var item in items)
            {
                item.Principal = item.BuyPrice * item.TargetSales;
            }
        }

        /// <summary>
        /// 历史价格波动
        /// 标准差/平均值
        /// </summary>
        /// <param name="items"></param>
        private void CalHistoryPriceFluctuation(List<ScalperItem> items)
        {
            foreach (var item in items)
            {
                var history = item.DestinationStatistics.Where(p => p.Date > DateTime.Now.AddDays(-Setting.HistoryPriceFluctuationDay - 2)).ToList();
                if (history.NotNullOrEmpty())
                {
                    var avg = history.Average(p=>p.Average);
                    var sum = history.Sum(p => Math.Pow((double)(p.Average - avg), 2));
                    var ret = Math.Sqrt(sum / history.Count);//标准差
                    item.HistoryPriceFluctuation = ret / (double)avg;
                }
            }
        }

        /// <summary>
        /// 当前价格波动值
        /// |(卖出价格 - 历史平均价格)|/历史平均价格
        /// </summary>
        /// <param name="items"></param>
        private void CalNowPriceFluctuation(List<ScalperItem> items)
        {
            foreach (var item in items)
            {
                var history = item.DestinationStatistics.Where(p => p.Date > DateTime.Now.AddDays(-Setting.NowPriceFluctuationDay - 2)).ToList();
                if (history.NotNullOrEmpty())
                {
                    var avg = (double)history.Average(p => p.Average);
                    item.NowPriceFluctuation = Math.Abs(item.SellPrice - avg) / avg;
                }
            }
        }

        /// <summary>
        /// 饱和度
        /// 有效价格范围内物品数量/日销量
        /// </summary>
        /// <param name="items"></param>
        private void CalSaturation(List<ScalperItem> items)
        {
            foreach (var item in items)
            {
                if(item.DestinationSales > 0 && item.DestinationSellOrders.NotNullOrEmpty())
                {
                    var validPrice = item.SellPrice * (1 + Setting.SaturationFluctuation / 100);
                    var validVolume = item.DestinationSellOrders.Where(p => (double)p.Price < validPrice).Sum(p => p.VolumeRemain);
                    item.Saturation = validVolume / item.DestinationSales;
                }
            }
        }
        /// <summary>
        /// 计算热力值
        /// </summary>
        /// <param name="items"></param>
        private void CalHeatValue(List<ScalperItem> items)
        {
            foreach (var item in items)
            {
                var history = item.DestinationStatistics.Where(p => p.Date > DateTime.Now.AddDays(-Setting.HeatValueDay - 2)).ToList();
                if (history.NotNullOrEmpty())
                {
                    foreach(var his in history)
                    {
                        item.HeatValue += his.Volume >= Setting.HeatValueThreshold ? 1 : -1;
                    }
                }
            }
        }

        private void CalSuggestion(List<ScalperItem> items)
        {
            //累计分
            int i = 1;
            double c = items.Count;
            //回报率
            foreach (var item in  items.OrderBy(p=>p.ROI))
            {
                if(item.ROI > 0)
                {
                    item.Suggestion = Setting.SuggestionROI * i / c;
                }
                i++;
            }
            //净利润
            i = 1;
            foreach (var item in items.OrderBy(p => p.TargetNetProfit))
            {
                if(item.TargetNetProfit > 0)
                {
                    item.Suggestion += Setting.SuggestionNetProfit * i / c;
                }
                i++;
            }
            //本金
            i = 1;
            foreach (var item in items.OrderBy(p => p.Principal))
            {
                item.Suggestion += Setting.SuggestionPrincipal * i / c;
                i++;
            }
            //销量
            i = 1;
            foreach (var item in items.OrderBy(p => p.DestinationSales))
            {
                item.Suggestion += Setting.SuggestionSales * i / c;
                i++;
            }
            //历史价格波动
            i = 1;
            foreach (var item in items.OrderByDescending(p => p.HistoryPriceFluctuation))
            {
                item.Suggestion += Setting.SuggestionHistoryPriceFluctuation * i / c;
                i++;
            }
            //当前价格波动
            i = 1;
            foreach (var item in items.OrderByDescending(p => p.NowPriceFluctuation))
            {
                item.Suggestion += Setting.SuggestionNowPriceFluctuation * i / c;
                i++;
            }
            //订单饱和度
            i = 1;
            foreach (var item in items.OrderByDescending(p => p.Saturation))
            {
                if(item.Saturation > 0)
                {
                    item.Suggestion += Setting.SuggestionSaturation * i / c;
                }
                i++;
            }
            //热力值
            i = 1;
            var groups = items.GroupBy(p => p.HeatValue).ToList();
            int groupCount = groups.Count;
            foreach (var group in groups.OrderBy(p => p.Key))
            {
                double s = Setting.SuggestionHeatValue * i / groupCount;
                foreach (var item in group)
                {
                    item.Suggestion += s;
                }
                i++;
            }

            i = 1;
            //转成百分比
            foreach (var item in items.OrderBy(p => p.Suggestion))
            {
                item.Suggestion = i / c * 100;
                i++;
            }
        }

        public void AddFilterTypes(List<InvType> invTypes)
        {
            if(invTypes.NotNullOrEmpty())
            {
                foreach(var invType in invTypes)
                {
                    FilterTypes.Add(invType);
                }
            }
        }

        public void RemoveFilterTypes(List<InvType> invTypes)
        {
            if (invTypes.NotNullOrEmpty())
            {
                foreach (var invType in invTypes)
                {
                    FilterTypes.Remove(invType);
                }
                Window?.ShowSuccess($"已移除{invTypes.Count}个物品");
            }
        }
    }
}
