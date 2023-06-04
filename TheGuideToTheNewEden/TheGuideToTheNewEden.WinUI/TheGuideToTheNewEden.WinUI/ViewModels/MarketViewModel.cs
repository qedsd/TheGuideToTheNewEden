using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        private int selectedRegionId;
        public int SelectedRegionId
        {
            get => selectedRegionId;
            set
            {
                SetProperty(ref selectedRegionId, value);
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
        private ESI.NET.EsiClient _esiClient = Core.Services.ESIService.GetDefaultEsi();
        public MarketViewModel() 
        {
            SelectedRegionId = 10000002;
        }
        private async void SetSelectedInvType()
        {
            if(SelectedInvType == null)
            {
                return;
            }
            ESI.NET.EsiResponse<List<ESI.NET.Models.Market.Order>> resp;
            ESI.NET.EsiClient esiClient = null;
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
                esiClient = Core.Services.ESIService.Current.EsiClient;
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
                esiClient = _esiClient;
            }
            if(esiClient ==null)
            {
                Window?.ShowError("ESI为空");
                return;
            }
            Window?.ShowWaiting("获取订单中...");
            resp = await esiClient.Market.RegionOrders(SelectedRegion.RegionID, ESI.NET.Enumerations.MarketOrderType.All, Page, SelectedInvType.TypeID);
            Window?.HideWaiting();
            if (resp != null && resp.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var buys = resp.Data.Where(p => p.IsBuyOrder)?.ToList();
                var sells = resp.Data.Where(p => !p.IsBuyOrder)?.ToList();
            }
            else
            {
                Window?.ShowError(resp?.Message);
                Core.Log.Error(resp?.Message);
            }
        }
    }
}
