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
using ESI.NET.Models.Market;
using ESI.NET;
using ESI.NET.Models.Contracts;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class ContractDetailPage: Page
    {
        private Core.Models.Contract.ContractInfo _contractInfo;
        private ESI.NET.EsiClient _esiClient;
        private int _type;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="esiClient"></param>
        /// <param name="contractInfo"></param>
        /// <param name="type">0 公开 1 个人 2 军团</param>
        public ContractDetailPage(ESI.NET.EsiClient esiClient,Core.Models.Contract.ContractInfo contractInfo, int type)
        {
            _esiClient = esiClient;
            _contractInfo = contractInfo;
            _type = type;
            this.InitializeComponent();
            Loaded += ContractDetailPage_Loaded;
        }

        private void ContractDetailPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadDefault();
            switch(_contractInfo.Type)
            {
                case ESI.NET.Enumerations.ContractType.Auction:LoadAuction(); break;
                case ESI.NET.Enumerations.ContractType.Courier:LoadCourier(); break;
                case ESI.NET.Enumerations.ContractType.ItemExchange: LoadItemExchange(); break;
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
            TextBlock_Volume.Text = _contractInfo.Volume.ToString("N2");
        }

        private async void LoadAuction()
        {
            StackPanel_Auction.Visibility = Visibility.Visible;
            List<ESI.NET.Models.Contracts.Bid> bids = new List<ESI.NET.Models.Contracts.Bid>();
            int page = 1;
            switch (_type)
            {
                case 0:
                    {
                        while(true)
                        {
                            var esiResponse = await _esiClient.Contracts.ContractBids(_contractInfo.ContractId, page);
                            if(esiResponse != null && esiResponse.StatusCode == System.Net.HttpStatusCode.OK && esiResponse.Data.NotNullOrEmpty())
                            {
                                bids.AddRange(esiResponse.Data);
                                if(esiResponse.Data.Count < 5000)
                                {
                                    break;
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    break;
                case 1:
                    {
                        while (true)
                        {
                            var esiResponse = await _esiClient.Contracts.CharacterContractBids(_contractInfo.ContractId, page);
                            if (esiResponse != null && esiResponse.StatusCode == System.Net.HttpStatusCode.OK && esiResponse.Data.NotNullOrEmpty())
                            {
                                bids.AddRange(esiResponse.Data);
                                if (esiResponse.Data.Count < 5000)
                                {
                                    break;
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    break;
                case 2:
                    {
                        while (true)
                        {
                            var esiResponse = await _esiClient.Contracts.CorporationContractBids(_contractInfo.ContractId, page);
                            if (esiResponse != null && esiResponse.StatusCode == System.Net.HttpStatusCode.OK && esiResponse.Data.NotNullOrEmpty())
                            {
                                bids.AddRange(esiResponse.Data);
                                if (esiResponse.Data.Count < 5000)
                                {
                                    break;
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    break;
            }
            if(bids.Any())
            {
                var order = bids.OrderByDescending(p => p.DateBid);
                TextBlock_CurrentBid.Text = order.First().Amount.ToString("N2");
            }
            TextBlock_StartingBid.Text = _contractInfo.Price.ToString("N2");
            TextBlock_Buyout.Text = _contractInfo.Buyout.ToString("N2");
        }
        private void LoadCourier()
        {
            StackPanel_Courier.Visibility = Visibility.Visible;
            TextBlock_DaysToComplet.Text = _contractInfo.DaysToComplete.ToString();
            TextBlock_Reward.Text = _contractInfo.Reward.ToString("N2");
            TextBlock_Collateral.Text = _contractInfo.Collateral.ToString("N2");
            TextBlock_EndLocation.Text = _contractInfo.EndLocationName;
        }

        private void LoadItemExchange()
        {
            StackPanel_ItemExchange.Visibility = Visibility.Visible;
            TextBlock_Price.Text = _contractInfo.Price.ToString("N2");
        }

        private async void LoadItems()
        {
            List<ESI.NET.Models.Contracts.ContractItem> items = new List<ESI.NET.Models.Contracts.ContractItem>();
            int page = 1;
            switch (_type)
            {
                case 0:
                    {
                        while (true)
                        {
                            var esiResponse = await _esiClient.Contracts.ContractItems(_contractInfo.ContractId, page);
                            if (esiResponse != null && esiResponse.StatusCode == System.Net.HttpStatusCode.OK && esiResponse.Data.NotNullOrEmpty())
                            {
                                items.AddRange(esiResponse.Data);
                                if (esiResponse.Data.Count < 5000)
                                {
                                    break;
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    break;
                case 1:
                    {
                        while (true)
                        {
                            var esiResponse = await _esiClient.Contracts.CharacterContractItems(_contractInfo.ContractId, page);
                            if (esiResponse != null && esiResponse.StatusCode == System.Net.HttpStatusCode.OK && esiResponse.Data.NotNullOrEmpty())
                            {
                                items.AddRange(esiResponse.Data);
                                if (esiResponse.Data.Count < 5000)
                                {
                                    break;
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    break;
                case 2:
                    {
                        while (true)
                        {
                            var esiResponse = await _esiClient.Contracts.CorporationContractItems(_contractInfo.ContractId, page);
                            if (esiResponse != null && esiResponse.StatusCode == System.Net.HttpStatusCode.OK && esiResponse.Data.NotNullOrEmpty())
                            {
                                items.AddRange(esiResponse.Data);
                                if (esiResponse.Data.Count < 5000)
                                {
                                    break;
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    break;
            }
            if (items.Any())
            {
                var contractItems = items.Select(p => new Core.Models.Contract.ContractItem(p)).ToList();
                var types = await Core.Services.DB.InvTypeService.QueryTypesAsync(contractItems.Select(p => p.TypeId).Distinct().ToList());
                var dic = types.ToDictionary(p => p.TypeID);
                var blueGroupIds = await Core.Services.DB.InvGroupService.QueryBlueprintGroupIdsAsync();
                var blueGroupIdsHashSet = blueGroupIds.ToHashSet2();
                foreach (var item in contractItems)
                {
                    if (dic.TryGetValue(item.TypeId, out var type))
                    {
                        item.TypeName = type.TypeName;
                        item.IsBlueprint = blueGroupIdsHashSet.Contains(type.GroupID);
                    }
                    else
                    {
                        item.TypeName = item.TypeId.ToString();
                    }
                }
                var gets = contractItems.Where(p => p.IsIncluded).ToList();
                var pays = contractItems.Where(p => !p.IsIncluded).ToList();
                DataGrid_WillGet.ItemsSource = gets;
                DataGrid_WillPay.ItemsSource = pays;
                if(!gets.NotNullOrEmpty())
                {
                    DataPivot.Items.RemoveAt(0);
                }
                if(!pays.NotNullOrEmpty())
                {
                    DataPivot.Items.RemoveAt(1);
                }
            }
            else
            {
                DataPivot.Items.Clear();
            }
        }
    }
}
