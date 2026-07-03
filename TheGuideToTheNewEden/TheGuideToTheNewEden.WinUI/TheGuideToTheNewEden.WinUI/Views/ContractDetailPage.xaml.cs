using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.Core.Extensions;
using EVEStandard;
using EVEStandard.Models.API;
using EVEStandard.Models;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class ContractDetailPage: Page
    {
        private Core.Models.Contract.ContractInfo _contractInfo;
        private EVEStandardAPI _esiClient;
        private AuthDTO _auth;
        private int _type;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="esiClient"></param>
        /// <param name="contractInfo"></param>
        /// <param name="type">0 公开 1 个人 2 军团</param>
        public ContractDetailPage(EVEStandardAPI esiClient, AuthDTO auth, Core.Models.Contract.ContractInfo contractInfo, int type)
        {
            _esiClient = esiClient;
            _auth = auth;
            _contractInfo = contractInfo;
            _type = type;
            this.InitializeComponent();
            Loaded += ContractDetailPage_Loaded;
        }

        private void ContractDetailPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ContractDetailPage_Loaded;
            LoadDefault();
            var type = Enum.Parse<Core.Models.Contract.ContractInfo.TypeEnum>(_contractInfo.Type);
            switch(_contractInfo.GetTypeEnum())
            {
                case Core.Models.Contract.ContractInfo.TypeEnum.auction:LoadAuction(); break;
                case Core.Models.Contract.ContractInfo.TypeEnum.courier: LoadCourier(); break;
                case Core.Models.Contract.ContractInfo.TypeEnum.item_exchange: LoadItemExchange(); break;
            }
            LoadItems();
        }
        private void LoadDefault()
        {
            if(!string.IsNullOrEmpty(_contractInfo.Title))
            {
                TextBlock_Title.Text = _contractInfo.Title;
            }
            else
            {
                TextBlock_Title.Visibility = Visibility.Collapsed;
            }
            TextBlock_Type.Text = _contractInfo.TypeStr;
            TextBlock_Issuer.Text = _contractInfo.IssuerName;
            if (!string.IsNullOrEmpty(_contractInfo.Availability))
            {
                TextBlock_Availability.Text = _contractInfo.Availability;
            }
            else
            {
                StackPanel_Availability.Visibility = Visibility.Collapsed;
            }
            if (!string.IsNullOrEmpty(_contractInfo.Status))
            {
                TextBlock_Status.Text = _contractInfo.Status;
            }
            else
            {
                StackPanel_Status.Visibility = Visibility.Collapsed;
            }
            TextBlock_StartLocation.Text = _contractInfo.StartLocationName;
            TextBlock_DateIssued.Text = _contractInfo.DateIssued.ToString();
            TextBlock_DateExpired.Text = _contractInfo.DateExpired.ToString();
            TextBlock_Volume.Text = _contractInfo.Volume.Value.ToString("N2");
            if (_contractInfo.DateAccepted != DateTime.MinValue)
            {
                TextBlock_DateAccepted.Text = _contractInfo.DateAccepted.ToString();
            }
            else
            {
                StackPanel_DateAccepted.Visibility = Visibility.Collapsed;
            }
            if (!string.IsNullOrEmpty(_contractInfo.AcceptorName))
            {
                TextBlock_Acceptor.Text = _contractInfo.AcceptorName;
            }
            else
            {
                StackPanel_Acceptor.Visibility = Visibility.Collapsed;
            }
        }

        private async void LoadAuction()
        {
            StackPanel_Auction.Visibility = Visibility.Visible;
            List<ContractBid> bids = new List<ContractBid>();
            int page = 1;
            ESIModelDTO<List<ContractBid>> esiResponse = null;
            while (true)
            {
                switch (_type)
                {
                    case 0:
                        {
                            esiResponse = await _esiClient.Contracts.GetPublicContractBidsAsync(_contractInfo.ContractId, page);
                        }break;
                    case 1:
                        {
                            esiResponse = await _esiClient.Contracts.GetContractBidsAsync(_auth, _contractInfo.ContractId);
                        }
                        break;
                    case 2:
                        {
                            esiResponse = await _esiClient.Contracts.GetCorporationContractBidsAsync(_auth,_contractInfo.ContractId, page);
                        }
                        break;
                }
                if (esiResponse?.Model != null)
                {
                    bids.AddRange(esiResponse.Model);
                }
                if(esiResponse.MaxPages == page)
                {
                    break;
                }
                else
                {
                    page++;
                }
            }
            
            
            if(bids.Any())
            {
                var order = bids.OrderByDescending(p => p.DateBid);
                TextBlock_CurrentBid.Text = order.First().Amount.ToString("N2");
            }
            TextBlock_StartingBid.Text = _contractInfo.Price.Value.ToString("N2");
            TextBlock_Buyout.Text = _contractInfo.Buyout.Value.ToString("N2");
        }
        private void LoadCourier()
        {
            StackPanel_Courier.Visibility = Visibility.Visible;
            TextBlock_DaysToComplet.Text = _contractInfo.DaysToComplete.ToString();
            TextBlock_Reward.Text = _contractInfo.Reward.Value.ToString("N2");
            TextBlock_Collateral.Text = _contractInfo.Collateral.Value.ToString("N2");
            TextBlock_EndLocation.Text = _contractInfo.EndLocationName;
            if (_contractInfo.DateCompleted != DateTime.MinValue)
            {
                TextBlock_DateCompleted.Text = _contractInfo.DateCompleted.ToString();
            }
            else
            {
                StackPanel_DateCompleted.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadItemExchange()
        {
            StackPanel_ItemExchange.Visibility = Visibility.Visible;
            TextBlock_Price.Text = _contractInfo.Price.Value.ToString("N2");
        }

        private async void LoadItems()
        {
            //TODO:界面区分公开合同、个人合同、军团合同后重写该部分逻辑
            //List<ESI.NET.Models.Contracts.ContractItem> items = new List<ESI.NET.Models.Contracts.ContractItem>();
            //if(_contractInfo.GetTypeEnum() != EVEStandard.Models.Contract.TypeEnum.courier)
            //{
            //    int page = 1;
                
            //    switch (_type)
            //    {
            //        case 0:
            //            {
            //                var esiResponse = await _esiClient.Contracts.GetPublicContractItemsAsync(_contractInfo.ContractId, page);
            //                while (true)
            //                {
            //                    if (esiResponse != null && esiResponse.StatusCode == System.Net.HttpStatusCode.OK && esiResponse.Data.NotNullOrEmpty())
            //                    {
            //                        items.AddRange(esiResponse.Data);
            //                        if (esiResponse.Data.Count < 5000)
            //                        {
            //                            break;
            //                        }
            //                    }
            //                    else
            //                    {
            //                        break;
            //                    }
            //                }
            //            }
            //            break;
            //        case 1:
            //            {
            //                while (true)
            //                {
            //                    var esiResponse = await _esiClient.Contracts.GetContractItemsAsync(_auth,_contractInfo.ContractId);
            //                    if (esiResponse != null && esiResponse.StatusCode == System.Net.HttpStatusCode.OK && esiResponse.Data.NotNullOrEmpty())
            //                    {
            //                        items.AddRange(esiResponse.Data);
            //                        if (esiResponse.Data.Count < 5000)
            //                        {
            //                            break;
            //                        }
            //                    }
            //                    else
            //                    {
            //                        break;
            //                    }
            //                }
            //            }
            //            break;
            //        case 2:
            //            {
            //                while (true)
            //                {
            //                    var esiResponse = await _esiClient.Contracts.CorporationContractItems(_contractInfo.ContractId, page);
            //                    if (esiResponse != null && esiResponse.StatusCode == System.Net.HttpStatusCode.OK && esiResponse.Data.NotNullOrEmpty())
            //                    {
            //                        items.AddRange(esiResponse.Data);
            //                        if (esiResponse.Data.Count < 5000)
            //                        {
            //                            break;
            //                        }
            //                    }
            //                    else
            //                    {
            //                        break;
            //                    }
            //                }
            //            }
            //            break;
            //    }
            //}
            //if (items.Any())
            //{
            //    var contractItems = items.Select(p => new Core.Models.Contract.ContractItem(p)).ToList();
            //    var types = await Core.Services.DB.InvTypeService.QueryTypesAsync(contractItems.Select(p => p.TypeId).Distinct().ToList());
            //    var dic = types.ToDictionary(p => p.TypeID);
            //    var blueGroupIds = await Core.Services.DB.InvGroupService.QueryBlueprintGroupIdsAsync();
            //    var blueGroupIdsHashSet = blueGroupIds.ToHashSet2();
            //    foreach (var item in contractItems)
            //    {
            //        if (dic.TryGetValue(item.TypeId, out var type))
            //        {
            //            item.TypeName = type.TypeName;
            //            item.IsBlueprint = blueGroupIdsHashSet.Contains(type.GroupID);
            //        }
            //        else
            //        {
            //            item.TypeName = item.TypeId.ToString();
            //        }
            //    }
            //    var gets = contractItems.Where(p => p.IsIncluded).ToList();
            //    var pays = contractItems.Where(p => !p.IsIncluded).ToList();
            //    DataGrid_WillGet.ItemsSource = gets;
            //    DataGrid_WillPay.ItemsSource = pays;
            //    if(!gets.NotNullOrEmpty())
            //    {
            //        DataPivot.Items.RemoveAt(0);
            //    }
            //    if(!pays.NotNullOrEmpty())
            //    {
            //        DataPivot.Items.RemoveAt(1);
            //    }
            //}
            //else
            //{
            //    DataPivot.Items.Clear();
            //}
        }
    }
}
