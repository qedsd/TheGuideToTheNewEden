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
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.Core.Extensions;
using System.Collections.ObjectModel;
using TheGuideToTheNewEden.Core.DBModels;
using Syncfusion.UI.Xaml.DataGrid;
using TheGuideToTheNewEden.WinUI.Extensions;
using EVEStandard.Models.API;
using EVEStandard;

namespace TheGuideToTheNewEden.WinUI.Views.Character
{
    public sealed partial class WalletPage : Page, ICharacterPage
    {
        private EVEStandardAPI _esiClient;
        private AuthDTO _auth;
        private Core.Models.Character.AuthorizedCharacterData _characterData;
        public WalletPage()
        {
            this.InitializeComponent();
            Loaded += WalletPage_Loaded;
        }

        private void WalletPage_Loaded(object sender, RoutedEventArgs e)
        {
            MainPivot.SelectionChanged += Pivot_SelectionChanged;
            if (!_isLoaded)
            {
                Refresh();
                _isLoaded = true;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var paras = e.Parameter as object[];
            if (paras != null && paras.Length == 2)
            {
                _esiClient = paras[0] as EVEStandardAPI;
                _characterData = paras[1] as Core.Models.Character.AuthorizedCharacterData;
                _auth = _characterData.ToAuthDTO();
            }
        }
        private bool _isLoaded = false;
        public void Clear()
        {
            _isLoaded = false;
        }
        public void Refresh()
        {
            this.ShowWaiting();
            _characterJournalsLoaded = false;
            _corpJournalsLoaded = false;
            _characterTransactionsLoaded = false;
            _corpTransactionsLoaded = false;
            if (MainPivot.SelectedIndex == 0)
            {
                Pivot_SelectionChanged(MainPivot,null);
            }
            else
            {
                MainPivot.SelectedIndex = 0;
            }
            this.HideWaiting();
        }

        private async Task<List<Core.Models.Wallet.JournalEntry>> GetCharacterJournalsAsync(int page)
        {
            var esiResponse = await _esiClient.Wallet.GetCharacterWalletJournalAsync(_auth,page);
            return GetDatas(esiResponse);
        }
        private async Task<List<Core.Models.Wallet.JournalEntry>> GetCorpJournalsAsync(int division,int page)
        {
            var esiResponse = await _esiClient.Wallet.GetCorporationWalletJournalAsync(_auth,_characterData.CorporationID,division, page);
            return GetDatas(esiResponse);
        }
        private List<Core.Models.Wallet.JournalEntry> GetDatas(ESIModelDTO<List<EVEStandard.Models.CharacterWalletJournal>> esiResponse)
        {
            if (esiResponse?.Model != null)
            {
                return esiResponse.Model.Select(p => new Core.Models.Wallet.JournalEntry(p)).ToList();
            }
            else
            {
                this.ShowError("CharacterWalletJournal is null");
                return null;
            }
        }
        private List<Core.Models.Wallet.JournalEntry> GetDatas(ESIModelDTO<List<EVEStandard.Models.CorporationWalletJournal>> esiResponse)
        {
            if (esiResponse?.Model != null)
            {
                return esiResponse.Model.Select(p => new Core.Models.Wallet.JournalEntry(p)).ToList();
            }
            else
            {
                this.ShowError("CorporationWalletJournal is null");
                return null;
            }
        }
        private async Task<List<Core.Models.Wallet.TransactionEntry>> GetCharacterTransactionsAsync(int fromId)
        {
            var esiResponse = await _esiClient.Wallet.GetCharacterWalletTransactionsAsync(_auth, fromId);
            return await GetDatas(esiResponse);
        }
        private async Task<List<Core.Models.Wallet.TransactionEntry>> GetCorpTransactionsAsync(int division, int fromId)
        {
            var esiResponse = await _esiClient.Wallet.GetCorporationWalletTransactionsAsync(_auth, _characterData.CorporationID,division, fromId);
            return await GetDatas(esiResponse);
        }
        private async Task<List<Core.Models.Wallet.TransactionEntry>> GetDatas(ESIModelDTO<List<EVEStandard.Models.WalletTransaction>> esiResponse)
        {
            if (esiResponse?.Model != null)
            {
                var list = esiResponse.Model.Select(p => new Core.Models.Wallet.TransactionEntry(p)).ToList();
                if (list.NotNullOrEmpty())
                {
                    await SetNames(list);
                }
                return list;
            }
            else
            {
                this.ShowError("WalletTransaction is null");
                return null;
            }
        }
        private async Task SetNames(List<Core.Models.Wallet.TransactionEntry> transactions)
        {
            //赋值物品信息
            var invTypes = await Core.Services.DB.InvTypeService.QueryTypesAsync(transactions.Select(p => p.Transaction.TypeId).ToList());
            if(invTypes.NotNullOrEmpty())
            {
                var invTypesDic = invTypes.ToDictionary(p => p.TypeID);
                foreach(var transaction in transactions)
                {
                    if(invTypesDic.TryGetValue((int)transaction.Transaction.TypeId, out var invType))
                    {
                        transaction.InvType = invType;
                    }
                    else
                    {
                        transaction.InvType = new Core.DBModels.InvType()
                        {
                            TypeName = transaction.Transaction.TypeId.ToString(),
                            TypeID = (int)transaction.Transaction.TypeId
                        };
                    }
                }
            }
            //赋值卖家信息
            var clientIds = transactions.Select(p=>p.Transaction.ClientId).ToList();
            if(clientIds.NotNullOrEmpty())
            {
                var resp = await _esiClient.Universe.GetNamesAndCategoriesFromIdsAsync(clientIds.Distinct().ToList());
                if(resp?.Model != null)
                {
                    var namesDic = resp.Model.ToDictionary(p => p.Id);
                    foreach (var transaction in transactions)
                    {
                        if (namesDic.TryGetValue(transaction.Transaction.ClientId, out var name))
                        {
                            transaction.ClientName = name.Name;
                        }
                        else
                        {
                            transaction.ClientName = transaction.Transaction.ClientId.ToString();
                        }
                    }
                }
                else
                {
                    this.ShowError("GetNamesAndCategoriesFromIdsAsync Failed", true);
                }
            }

            //位置
            //LocationId是空间站或者建筑
            var allLocationIds = transactions.Select(p => p.Transaction.LocationId).ToList();
            if(allLocationIds.NotNullOrEmpty())
            {
                var stations = allLocationIds.Where(p => p > 70000000).ToList();
                var structures = allLocationIds.Except(stations).ToList();
                Dictionary<long, string> locationNames = new Dictionary<long, string>();
                if(stations.NotNullOrEmpty())
                {
                    var staStaions = await Core.Services.DB.StaStationService.QueryAsync(stations);
                    if(staStaions.NotNullOrEmpty())
                    {
                        foreach(var staSta in staStaions)
                        {
                            locationNames.Add(staSta.StationID, staSta.StationName);
                        }
                    }
                }
                if(structures.NotNullOrEmpty())
                {
                    var structuresResp = await _esiClient.Universe.GetNamesAndCategoriesFromIdsAsync(structures.Select(p => p).ToList());
                    if(structuresResp?.Model != null)
                    {
                        foreach (var data in structuresResp.Model)
                        {
                            locationNames.Add(data.Id, data.Name);
                        }
                    }
                    else
                    {
                        this.ShowError("GetNamesAndCategoriesFromIdsAsync Failed", true);
                    }
                }
                foreach(var t in transactions)
                {
                    if(locationNames.TryGetValue(t.Transaction.LocationId, out var value))
                    {
                        t.LocationName = value;
                    }
                    else
                    {
                        t.LocationName = t.Transaction.LocationId.ToString();
                    }
                }
            }
        }


        private bool _characterJournalsLoaded = false;
        private bool _corpJournalsLoaded = false;
        private bool _characterTransactionsLoaded = false;
        private bool _corpTransactionsLoaded = false;
        private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.ShowWaiting();
            switch((sender as Pivot).SelectedIndex)
            {
                case 0: 
                    {
                        if(!_characterJournalsLoaded)
                        {
                            _characterJournalsLoaded = true;
                            NavigatePageControl_CharacterJournal_OnPageChanged(1);
                        }
                    }break;
                case 1: 
                    {
                        if (!_characterTransactionsLoaded)
                        {
                            _characterTransactionsLoaded = true;
                            LoadCharacterTransactionsAsync();
                        }
                    } break;
                case 2: 
                    {
                        if (!_corpJournalsLoaded)
                        {
                            _corpJournalsLoaded = true;
                            NavigatePageControl_CorpJournal_OnPageChanged(1);
                        }
                    } break;
                case 3:
                    {
                        if (!_corpTransactionsLoaded)
                        {
                            _corpTransactionsLoaded = true;
                            LoadCorpTransactionsAsync();
                        }
                    } break;
            }
            this.HideWaiting();
        }

        private async void NavigatePageControl_CharacterJournal_OnPageChanged(int page)
        {
            if (page < 1)
            {
                DataGrid_CharacterJournal.ItemsSource = null;
                return;
            }
            this.ShowWaiting();
            DataGrid_CharacterJournal.ItemsSource = await GetCharacterJournalsAsync(page);
            this.HideWaiting();
        }
        private async void NavigatePageControl_CorpJournal_OnPageChanged(int page)
        {
            if(page < 1)
            {
                DataGrid_CorpJournal.ItemsSource = null;
                return;
            }
            this.ShowWaiting();
            DataGrid_CorpJournal.ItemsSource = await GetCorpJournalsAsync((int)NumberBox_CorpJournal.Value, page);
            this.HideWaiting();
        }
        private async void LoadCharacterTransactionsAsync()
        {
            this.ShowWaiting();
            DataGrid_CharacterTransaction.ItemsSource = await GetCharacterTransactionsAsync(0);
            this.HideWaiting();
        }
        private async void LoadCorpTransactionsAsync()
        {
            if(_esiClient != null)
            {
                this.ShowWaiting();
                DataGrid_CorpTransaction.ItemsSource = await GetCorpTransactionsAsync((int)NumberBox_CorpTransaction.Value, 0);
                this.HideWaiting();
            }
        }
        private void NumberBox_CorpJournal_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if(NavigatePageControl_CorpJournal != null)
            {
                NavigatePageControl_CorpJournal.Page = 0;
                NavigatePageControl_CorpJournal.Page = 1;
            }
        }

        private void NumberBox_CorpTransaction_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            LoadCorpTransactionsAsync();
        }
    }
    public class JournalEntryCellStyleSelector : StyleSelector
    {
        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            var data = item as Core.Models.Wallet.JournalEntry;
            var mappingName = (container as GridCell).ColumnBase.GridColumn.MappingName;

            if (mappingName == "Amount")
            {
                if (data.Amount < 0)
                {
                    return Helpers.ResourcesHelper.Get("RedForegroundCellStyle") as Style;
                }
                else
                {
                    return Helpers.ResourcesHelper.Get("GreenForegroundCellStyle") as Style;
                }
            }
            return base.SelectStyleCore(item, container);
        }
    }
    public class TransactionEntryCellStyleSelector : StyleSelector
    {
        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            var data = item as Core.Models.Wallet.TransactionEntry;
            var mappingName = (container as GridCell).ColumnBase.GridColumn.MappingName;

            if (mappingName == "TotalPrice")
            {
                if (!data.Transaction.IsBuy)
                {
                    return Helpers.ResourcesHelper.Get("RedForegroundCellStyle") as Style;
                }
                else
                {
                    return Helpers.ResourcesHelper.Get("GreenForegroundCellStyle") as Style;
                }
            }
            return base.SelectStyleCore(item, container);
        }
    }
}
