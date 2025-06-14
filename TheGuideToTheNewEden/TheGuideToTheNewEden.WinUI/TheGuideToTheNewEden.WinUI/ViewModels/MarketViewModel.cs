﻿using CommunityToolkit.Mvvm.Input;
using ESI.NET;
using ESI.NET.Models.Character;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.Universe;
using TheGuideToTheNewEden.WinUI.Services;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    public class MarketViewModel: BaseViewModel
    {
        private int selectedMarketTypeIndex;
        public int SelectedMarketTypeIndex
        {
            get => selectedMarketTypeIndex;
            set
            {
                if(SetProperty(ref selectedMarketTypeIndex, value))
                {
                    //if (value == 0)
                    //{
                    //    SelectedStructure = null;
                    //}
                    //else if (value == 1)
                    //{
                    //    SelectedRegion = null;
                    //}
                    //else
                    //{
                    //    SelectedStructure = null;
                    //    SelectedRegion = null;
                    //}
                }
            }
        }
        private string selectedMarketName = Helpers.ResourcesHelper.GetString("MarketPage_SelecteMarket");
        public string SelectedMarketName
        {
            get => selectedMarketName;
            set
            {
                SetProperty(ref selectedMarketName, value);
            }
        }

        private MapRegion selectedRegion;
        public MapRegion SelectedRegion
        {
            get => selectedRegion;
            set
            {
                if(SetProperty(ref selectedRegion, value))
                {
                    if (value != null)
                    {
                        SelectedStructure = null;
                        SetSelectedInvType();
                        SelectedMarketName = value.RegionName;
                    }
                }
            }
        }
        private Structure selectedStructure;
        public Structure SelectedStructure
        {
            get => selectedStructure;
            set
            {
                if(SetProperty(ref selectedStructure, value))
                {
                    if (value != null)
                    {
                        SelectedRegion = null;
                        SetSelectedInvType();
                        SelectedMarketName = value.Name;
                    }
                }
            }
        }

        private InvType selectedInvType;
        public InvType SelectedInvType
        {
            get => selectedInvType;
            set
            {
                if(SetProperty(ref selectedInvType, value))
                {
                    SetSelectedInvType();
                }
            }
        }

        private bool stared = false;
        public bool Stared
        {
            get => stared;
            set
            {
                SetProperty(ref stared, value);
            }
        }

        private bool locationFilterH;
        public bool LocationFilterH { get => locationFilterH; set => SetProperty(ref locationFilterH, value); }
        private bool locationFilterL;
        public bool LocationFilterL { get => locationFilterL; set => SetProperty(ref locationFilterL, value); }
        private bool locationFilterN;
        public bool LocationFilterN { get => locationFilterN; set => SetProperty(ref locationFilterN, value); }

        private ObservableCollection<Core.Models.Market.Order> sellOrders;
        public ObservableCollection<Core.Models.Market.Order> SellOrders
        {
            get => sellOrders;
            set => SetProperty(ref sellOrders, value);
        }

        private ObservableCollection<Core.Models.Market.Order> buyOrders;
        public ObservableCollection<Core.Models.Market.Order> BuyOrders
        {
            get => buyOrders;
            set => SetProperty(ref buyOrders, value);
        }

        private List<ESI.NET.Models.Market.Statistic> statistics;
        public List<ESI.NET.Models.Market.Statistic> Statistics
        {
            get => statistics;
            set => SetProperty(ref statistics, value);
        }
        private List<ESI.NET.Models.Market.Statistic> statisticsForShow;
        public List<ESI.NET.Models.Market.Statistic> StatisticsForShow
        {
            get => statisticsForShow;
            set => SetProperty(ref statisticsForShow, value);
        }

        private int historyRangeIndex = 1;
        /// <summary>
        /// 0:一个月 1:三个月 2:半年 3:一年 4:全部
        /// </summary>
        public int HistoryRangeIndex
        {
            get => historyRangeIndex;
            set
            {
                SetProperty(ref historyRangeIndex, value);
                SetStatistics();
            }
        }

        private double sell5P;
        public double Sell5P
        {
            get => sell5P;
            set => SetProperty(ref sell5P, value);
        }

        private double buy5P;
        public double Buy5P
        {
            get => buy5P;
            set => SetProperty(ref buy5P, value);
        }

        private double sellMean;
        public double SellMean
        {
            get => sellMean;
            set => SetProperty(ref sellMean, value);
        }

        private double buyMean;
        public double BuyMean
        {
            get => buyMean;
            set => SetProperty(ref buyMean, value);
        }

        private long sellAmount;
        public long SellAmount
        {
            get => sellAmount;
            set => SetProperty(ref sellAmount, value);
        }

        private long buyAmount;
        public long BuyAmount
        {
            get => buyAmount;
            set => SetProperty(ref buyAmount, value);
        }
        private BitmapImage selectedInvTypeIcon;
        public BitmapImage SelectedInvTypeIcon { get => selectedInvTypeIcon; set => SetProperty(ref selectedInvTypeIcon, value); }

        public MarketViewModel() 
        {
            SelectedRegion = Core.Services.DB.MapRegionService.Query(10000002);
        }
        private async void SetSelectedInvType()
        {
            if(SelectedInvType == null)
            {
                return;
            }
            SelectedInvTypeIcon = new BitmapImage(new Uri(Converters.GameImageConverter.GetImageUri(SelectedInvType.TypeID, Converters.GameImageConverter.ImgType.Type, 64)));
            if (SelectedRegion == null && SelectedStructure == null)
            {
                Window.ShowError("未选择市场");
                return;
            }
            if (SelectedRegion != null)
            {
                await GetRegionOrders();
            }
            else if(SelectedStructure != null)
            {
                await GetSructureOrders();
            }
            Stared = MarketStarService.Current.IsStared(SelectedInvType.TypeID);

            SetBuyPrice();
            SetSellPrice();
            Cal();
        }

        private async Task GetRegionOrders()
        {
            BuyOrders?.Clear();
            SellOrders?.Clear();
            StatisticsForShow?.Clear();
            Window?.ShowWaiting("获取订单中...");
            try
            {
                List<Core.Models.Market.Order> orders = await Services.MarketOrderService.Current.GetRegionOrdersAsync(SelectedInvType.TypeID, SelectedRegion.RegionID);
                BuyOrders = orders?.Where(p => p.IsBuyOrder).OrderByDescending(p => p.Price)?.ToObservableCollection();
                SellOrders = orders?.Where(p => !p.IsBuyOrder).OrderBy(p => p.Price)?.ToObservableCollection();
                SetOrderStatisticalInfo(SellOrders, BuyOrders);
            }
            catch (Exception ex)
            {
                Window?.HideWaiting();
                Window?.ShowError(ex.Message);
                Core.Log.Error(ex);
                return;
            }
            Window?.ShowWaiting("获取历史记录中...");
            try
            {
                Statistics = await Services.MarketOrderService.Current.GetHistoryAsync(SelectedInvType.TypeID, SelectedRegion.RegionID);
                SetStatistics();
            }
            catch(Exception ex)
            {
                Window?.ShowError(ex.Message);
                Core.Log.Error(ex);
                StatisticsForShow = null;
                Statistics = null;
            }
            Window?.HideWaiting();
        }

        private async Task GetSructureOrders()
        {
            Window?.ShowWaiting("获取订单中...");
            List<Core.Models.Market.Order> orders = await Services.MarketOrderService.Current.GetStructureOrdersAsync(SelectedStructure.Id, SelectedInvType.TypeID); ;
            BuyOrders = orders?.Where(p => p.IsBuyOrder).OrderByDescending(p => p.Price)?.ToObservableCollection();
            SellOrders = orders?.Where(p => !p.IsBuyOrder).OrderBy(p => p.Price)?.ToObservableCollection();
            SetOrderStatisticalInfo(SellOrders, BuyOrders);
            Window?.ShowWaiting("获取历史记录中...");
            try
            {
                Statistics = await Services.MarketOrderService.Current.GetHistoryAsync(SelectedInvType.TypeID, SelectedStructure.RegionId);
                SetStatistics();
            }
            catch (Exception ex)
            {
                Window?.ShowError(ex.Message);
                Core.Log.Error(ex);
                StatisticsForShow = null;
                Statistics = null;
            }
            Window?.HideWaiting();
        }
        private void SetOrderStatisticalInfo(IEnumerable<Core.Models.Market.Order> sellOrders, IEnumerable<Core.Models.Market.Order> buyOrders)
        {
            if(sellOrders.NotNullOrEmpty())
            {
                int top5P = (int)(sellOrders.Count() * 0.05);
                if (top5P > 1)
                {
                    Sell5P = (double)sellOrders.Take(top5P).Average(p => p.Price);
                }
                else
                {
                    Sell5P = (double)sellOrders.FirstOrDefault()?.Price;
                }
                SellMean = (double)sellOrders.Average(p => p.Price);
                SellAmount = sellOrders.Sum(p => (long)p.VolumeRemain);
            }
            else
            {
                Sell5P = 0;
                SellMean = 0;
                SellAmount = 0;
            }

            if (buyOrders.NotNullOrEmpty())
            {
                int top5P = (int)(buyOrders.Count() * 0.05);
                if (top5P > 1)
                {
                    Buy5P = (double)buyOrders.Take(top5P).Average(p => p.Price);
                }
                else
                {
                    Buy5P = (double)buyOrders.FirstOrDefault()?.Price;
                }
                BuyMean = (double)buyOrders.Average(p => p.Price);
                BuyAmount = buyOrders.Sum(p => (long)p.VolumeRemain);
            }
            else
            {
                Buy5P = 0;
                BuyMean = 0;
                BuyAmount = 0;
            }
        }
        private void SetStatistics()
        {
            switch(HistoryRangeIndex)
            {
                case 0:
                    {
                        StatisticsForShow = Statistics.Where(p => p.Date > DateTime.Now.AddMonths(-1)).ToList();
                    }break;
                case 1:
                    {
                        StatisticsForShow = Statistics.Where(p => p.Date > DateTime.Now.AddMonths(-3)).ToList();
                    }
                    break;
                case 2:
                    {
                        StatisticsForShow = Statistics.Where(p => p.Date > DateTime.Now.AddMonths(-6)).ToList();
                    }
                    break;
                case 3:
                    {
                        StatisticsForShow = Statistics.Where(p => p.Date > DateTime.Now.AddMonths(-12)).ToList();
                    }
                    break;
                case 4:
                    {
                        StatisticsForShow = Statistics;
                    }
                    break;
            }
        }

        #region 收藏
        public ICommand StarCommand => new RelayCommand(() =>
        {
            if(MarketStarService.Current.IsStared(SelectedInvType.TypeID))
            {
                if (MarketStarService.Current.Remove(SelectedInvType.TypeID))
                {
                    Window.ShowSuccess("已取消收藏");
                    Stared = false;
                }
                else
                {
                    Window.ShowError("取消收藏失败");
                }
            }
            else
            {
                if (MarketStarService.Current.Add(SelectedInvType.TypeID))
                {
                    Window.ShowSuccess("已收藏");
                    Stared = true;
                }
                else
                {
                    Window.ShowError("添加收藏失败");
                }
            }
        });
        #endregion

        #region 计算器
        #region 卖出
        private double calSalesTax = 3.6;
        /// <summary>
        /// 销售税
        /// </summary>
        public double CalSalesTax
        {
            get => calSalesTax;
            set
            {
                if (SetProperty(ref calSalesTax, value))
                {
                    Cal();
                }
            }
        }
        private double calAgentTax = 1;
        /// <summary>
        /// 中介税
        /// </summary>
        public double CalAgentTax
        {
            get => calAgentTax;
            set
            {
                if (SetProperty(ref calAgentTax, value))
                {
                    Cal();
                }
            }
        }
        private double calSellAmount = 1;
        /// <summary>
        /// 卖出价格
        /// </summary>
        public double CalSellAmount
        {
            get => calSellAmount;
            set
            {
                if (SetProperty(ref calSellAmount, value))
                {
                    Cal();
                }
            }
        }
        private double calSellResult;
        /// <summary>
        /// 总卖出价格
        /// </summary>
        public double CalSellResult
        {
            get => calSellResult;
            set
            {
                if (SetProperty(ref calSellResult, value))
                {
                    Cal();
                }
            }
        }
        private double calSellPrice;
        /// <summary>
        /// 单个卖出价格
        /// </summary>
        public double CalSellPrice
        {
            get => calSellPrice;
            set
            {
                if (SetProperty(ref calSellPrice, value))
                {
                    Cal();
                }
            }
        }
        private bool calSelImmediately = false;
        /// <summary>
        /// 立即卖出
        /// </summary>
        public bool CalSellImmediately
        {
            get => calSelImmediately;
            set
            {
                if (SetProperty(ref calSelImmediately, value))
                {
                    SetSellPrice();
                    Cal();
                }
            }
        }
        #endregion

        #region 买入
        private double calBuyAmount = 1;
        /// <summary>
        /// 买入数量
        /// </summary>
        public double CalBuyAmount
        {
            get => calBuyAmount;
            set
            {
                if (SetProperty(ref calBuyAmount, value))
                {
                    Cal();
                }
            }
        }
        private double calBuyResult;
        /// <summary>
        /// 买入总价格
        /// </summary>
        public double CalBuyResult
        {
            get => calBuyResult;
            set
            {
                SetProperty(ref calBuyResult, value);
            }
        }
        private bool autoSetBuyPrice = true;
        /// <summary>
        /// 自动算买入价格
        /// </summary>
        public bool AutoSetBuyPrice
        {
            get => autoSetBuyPrice;
            set
            {
                if (SetProperty(ref autoSetBuyPrice, value))
                {
                    Cal();
                }
            }
        }
        private double calBuyPrice;
        /// <summary>
        /// 单个买入价格
        /// </summary>
        public double CalBuyPrice
        {
            get => calBuyPrice;
            set
            {
                if (SetProperty(ref calBuyPrice, value))
                {
                    Cal();
                }
            }
        }
        #endregion
        
        private void SetBuyPrice()
        {
            if (SellOrders.NotNullOrEmpty())
                CalBuyPrice = (double)SellOrders.First().Price;
            else
                CalBuyPrice = 0;
        }
        private void SetSellPrice()
        {
            if (CalSellImmediately)
            {
                if (BuyOrders.NotNullOrEmpty())
                    CalSellPrice = (double)BuyOrders.First().Price;
                else
                    CalSellPrice = 0;
            }
            else
            {
                if (SellOrders.NotNullOrEmpty())
                    CalSellPrice = (double)SellOrders.First().Price;//TODO:自动递增
                else
                {
                    CalSellPrice = 0;
                }
            }
        }
        private void Cal()
        {
            if(SellOrders.NotNullOrEmpty())
            {
                if (AutoSetBuyPrice)
                {
                    decimal totalPrice = 0;//总计算价格
                    long remainCount = (long)CalBuyAmount;//剩余计算数量
                    foreach (var sellOrder in SellOrders)
                    {
                        long takeCount = sellOrder.VolumeRemain > remainCount ? remainCount : sellOrder.VolumeRemain;
                        totalPrice += takeCount * sellOrder.Price;
                        remainCount -= takeCount;
                        if (remainCount == 0)
                            break;
                    }
                    if (remainCount > 0)//卖单无法满足数量，剩余数量从最高卖单取
                    {
                        Window?.ShowError(Helpers.ResourcesHelper.GetString("MarketPage_Cal_Buy_NoEnoughSellOrder"));
                        totalPrice += SellOrders.First().Price * remainCount;
                    }
                    CalBuyResult = (double)totalPrice;
                }
                else
                {
                    CalBuyResult = CalBuyPrice * CalBuyAmount;
                }
            }
            double totalSellPrice = CalSellPrice * CalSellAmount;
            if (CalSellImmediately)//只算销售税
            {
                totalSellPrice *= (100 - CalSalesTax) / 100.0;
            }
            else//中介税+销售税
            {
                totalSellPrice *= (100 - CalSalesTax - CalAgentTax) / 100.0;
            }
            CalSellResult = totalSellPrice;
        }
        #endregion

        #region 外部调用
        public void SelectType(int typeID)
        {
            var type = Core.Services.DB.InvTypeService.QueryType(typeID);
            SelectedInvType = type;
        }
        #endregion
    }
}
