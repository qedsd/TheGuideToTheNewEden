using CommunityToolkit.Mvvm.Input;
using ESI.NET;
using ESI.NET.Models.Character;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
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
        private int page = 1;
        public int Page
        {
            get => page;
            set
            {
                if (SetProperty(ref page, value))
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

        private int sellAmount;
        public int SellAmount
        {
            get => sellAmount;
            set => SetProperty(ref sellAmount, value);
        }

        private int buyAmount;
        public int BuyAmount
        {
            get => buyAmount;
            set => SetProperty(ref buyAmount, value);
        }
        private BitmapImage selectedInvTypeIcon;
        public BitmapImage SelectedInvTypeIcon { get => selectedInvTypeIcon; set => SetProperty(ref selectedInvTypeIcon, value); }
        private ESI.NET.EsiClient _esiClient = Core.Services.ESIService.GetDefaultEsi();
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
            if (SelectedMarketTypeIndex == 0)
            {
                if (SelectedRegion == null)
                {
                    Window.ShowError("未选择星域");
                    return;
                }
                GetRegionOrders();
            }
            else if(SelectedMarketTypeIndex == 1)
            {
                if(SelectedStructure == null)
                {
                    Window.ShowError("未选择建筑");
                    return;
                }
                var character = Services.CharacterService.CharacterOauths.FirstOrDefault(p => p.CharacterID == SelectedStructure.CharacterId);
                if(character == null)
                {
                    Window.ShowError($"未找到角色{character.CharacterID}");
                    return;
                }
                _esiClient.SetCharacterData(character);
                if (!character.IsTokenValid())
                {
                    Window?.ShowWaiting("刷新角色Token...");
                    if (!await character.RefreshTokenAsync())
                    {
                        Window?.HideWaiting();
                        Window?.ShowError("Token已过期，尝试刷新失败");
                        return;
                    }
                }
                GetSructureOrders();
            }
        }

        private async void GetRegionOrders()
        {
            Window?.ShowWaiting("获取订单中...");
            List<Core.Models.Market.Order> orders = new List<Core.Models.Market.Order>();
            int page = 1;
            while(true)
            {
                var resp = await Core.Services.ESIService.Current.EsiClient.Market.RegionOrders(SelectedRegion.RegionID, ESI.NET.Enumerations.MarketOrderType.All, page++, SelectedInvType.TypeID);
                if (resp != null && resp.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    if(resp.Data.Any())
                    {
                        orders.AddRange(resp.Data.Select(p=> new Core.Models.Market.Order(p)));
                        if(orders.Count < 1000)
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
                    Window?.ShowError(resp?.Message);
                    Core.Log.Error(resp?.Message);
                    break;
                }
            }
            if(orders.Any())
            {
                #region 赋值物品、星系信息
                //var types = await Core.Services.DB.InvTypeService.QueryTypesAsync(orders.Select(p=>p.TypeId).Distinct().ToList());
                //var typesDic = types.ToDictionary(p => p.TypeID);
                var systems = await Core.Services.DB.MapSolarSystemService.QueryAsync(orders.Select(p => (int)p.SystemId).Distinct().ToList());
                var systemsDic = systems.ToDictionary(p => p.SolarSystemID);
                foreach(var order in orders)
                {
                    //if(typesDic.TryGetValue(order.TypeId, out var type))
                    //{
                    //    order.InvType = type;
                    //}
                    order.InvType = SelectedInvType;
                    if (systemsDic.TryGetValue((int)order.SystemId, out var system))
                    {
                        order.SolarSystem = system;
                    }
                }
                #endregion
                #region 赋值空间站、建筑星系
                var stationOrders = orders.Where(p => p.IsStation).ToList();
                var structureOrders = orders.Where(p => !p.IsStation).ToList();
                if(stationOrders.NotNullOrEmpty())
                {
                    var stations = await Core.Services.DB.StaStationService.QueryAsync(stationOrders.Select(p=>(int)p.LocationId).ToList());
                    var stationsDic = stations.ToDictionary(p => p.StationID);
                    foreach(var order in stationOrders)
                    {
                        if (stationsDic.TryGetValue((int)order.LocationId, out var station))
                        {
                            order.LocationName = station.StationName;
                        }
                    }
                }
                if(structureOrders.NotNullOrEmpty())
                {
                    _esiClient.SetCharacterData(await Services.CharacterService.GetDefaultCharacterAsync());
                    var result = await Core.Helpers.ThreadHelper.RunAsync(structureOrders.Select(p=>p.LocationId).Distinct(), GetStructure);
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
                #endregion
            }
            BuyOrders = orders.Where(p => p.IsBuyOrder).OrderByDescending(p=>p.Price)?.ToObservableCollection();
            SellOrders = orders.Where(p => !p.IsBuyOrder).OrderBy(p=>p.Price)?.ToObservableCollection();
            SetOrderStatisticalInfo(SellOrders, BuyOrders);
            Window?.HideWaiting();
        }
        private async Task<Core.Models.Universe.Structure> GetStructure(long id)
        {
            try
            {
                var resp = await _esiClient.Universe.Structure(id);
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
            catch(Exception ex)
            {
                Core.Log.Error(ex);
                return new Core.Models.Universe.Structure()
                {
                    Id = id,
                    Name = id.ToString(),
                };
            }
        }

        private async void GetSructureOrders()
        {
            //TODO:
            await GetAllStructureOrders();
        }
        private async Task<List<Core.Models.Market.Order>> GetAllStructureOrders()
        {
            List<Core.Models.Market.Order> orders = new List<Core.Models.Market.Order>();
            int page = 1;
            while (true)
            {
                var resp = await _esiClient.Market.StructureOrders(SelectedStructure.Id, page++);
                Window?.HideWaiting();
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
                    Window?.ShowError(resp?.Message);
                    Core.Log.Error(resp?.Message);
                    break;
                }
            }
            if (orders.Any())
            {
                foreach (var order in orders)
                {
                    order.InvType = SelectedInvType;
                    order.LocationName = SelectedStructure.Name;
                }
            }
            return orders;
        }
        private void SetOrderStatisticalInfo(IEnumerable<Core.Models.Market.Order> sellOrders, IEnumerable<Core.Models.Market.Order> buyOrders)
        {
            if(sellOrders.NotNullOrEmpty())
            {
                int top5P = (int)(sellOrders.Count() * 0.05);
                //int top5P = (int)(sellOrders.Sum(p=>p.VolumeRemain) * 0.05);
                //decimal top5POrdersPrice = 0;
                //int caledOrders = 0;
                //foreach (var order in sellOrders)
                //{
                //    int remainOrder = top5P - caledOrders;
                //    int takeOrder = remainOrder > order.VolumeRemain ? order.VolumeRemain : remainOrder;
                //    caledOrders += takeOrder;
                //    top5POrdersPrice += takeOrder * order.Price;
                //    if(caledOrders >= top5P)
                //    {
                //        break;
                //    }
                //}
                if (top5P > 1)
                {
                    Sell5P = (double)sellOrders.Take(top5P).Average(p => p.Price);
                }
                else
                {
                    Sell5P = (double)sellOrders.FirstOrDefault()?.Price;
                }
                SellMean = (double)sellOrders.Average(p => p.Price);
                SellAmount = sellOrders.Sum(p => p.VolumeRemain);
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
                BuyAmount = buyOrders.Sum(p => p.VolumeRemain);
            }
            else
            {
                Buy5P = 0;
                BuyMean = 0;
                BuyAmount = 0;
            }
        }
        public ICommand StarCommand => new RelayCommand(() =>
        {
            //TODO:收藏列表
            Stared = !Stared;
        });
        public ICommand GetSructureOrdersCommand => new RelayCommand(async() =>
        {
            //TODO:保存数据库
            Window?.ShowWaiting("获取建筑订单中...");
            await GetAllStructureOrders();
            Window?.HideWaiting();
        });
    }
}
