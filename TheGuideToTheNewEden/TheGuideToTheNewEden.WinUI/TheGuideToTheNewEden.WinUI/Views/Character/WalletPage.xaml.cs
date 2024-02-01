using CommunityToolkit.WinUI.UI.Controls.TextToolbarSymbols;
using ESI.NET;
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

namespace TheGuideToTheNewEden.WinUI.Views.Character
{
    public sealed partial class WalletPage : Page, ICharacterPage
    {
        private EsiClient _esiClient;
        private BaseWindow _window;
        public WalletPage()
        {
            this.InitializeComponent();
            Loaded += WalletPage_Loaded;
        }

        private void WalletPage_Loaded(object sender, RoutedEventArgs e)
        {
            _window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
            MainPivot.SelectionChanged += Pivot_SelectionChanged;
            if (!_isLoaded)
            {
                Refresh();
                _isLoaded = true;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _esiClient = e.Parameter as EsiClient;
        }
        private bool _isLoaded = false;
        public void Clear()
        {
            _isLoaded = false;
        }
        public void Refresh()
        {
            _window.ShowWaiting();
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
            _window.HideWaiting();
        }

        private async Task<List<Core.Models.Wallet.JournalEntry>> GetAllJournalsAsync(bool corp)
        {
            List<Core.Models.Wallet.JournalEntry> data = new List<Core.Models.Wallet.JournalEntry>();
            for(int page = 1; ;page++)
            {
                var list = await GetJournalsAsync(corp, page);
                if(list.NotNullOrEmpty())
                {
                    data.AddRange(list);
                    if(list.Count < 1000)
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
            return data;
        }
        private async Task<List<Core.Models.Wallet.TransactionEntry>> GetAllTransactionsAsync(bool corp)
        {
            List<Core.Models.Wallet.TransactionEntry> data = new List<Core.Models.Wallet.TransactionEntry>();
            for (int page = 1; ; page++)
            {
                var list = await GetTransactionsAsync(corp, page);
                if (list.NotNullOrEmpty())
                {
                    data.AddRange(list);
                    if (list.Count < 1000)
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
            return data;
        }
        private async Task<List<Core.Models.Wallet.JournalEntry>> GetJournalsAsync(bool corp, int page = 1)
        {
            EsiResponse<List<ESI.NET.Models.Wallet.JournalEntry>> esiResponse;
            if(corp)
            {
                esiResponse = await _esiClient.Wallet.CorporationJournal(page);
            }
            else
            {
                esiResponse = await _esiClient.Wallet.CharacterJournal(page);
            }
            if(esiResponse != null)
            {
                if(esiResponse.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    return esiResponse.Data.Select(p=>new Core.Models.Wallet.JournalEntry(p)).ToList();
                }
                else
                {
                    Log.Error(esiResponse.Message);
                }
            }
            else
            {
                _window.ShowError("None");
            }
            return null;
        }
        private async Task<List<Core.Models.Wallet.TransactionEntry>> GetTransactionsAsync(bool corp, int page = 1)
        {
            EsiResponse<List<ESI.NET.Models.Wallet.Transaction>> esiResponse;
            if (corp)
            {
                //esiResponse = await _esiClient.Wallet.CorporationTransactions(page);
                esiResponse = null;
            }
            else
            {
                esiResponse = await _esiClient.Wallet.CharacterTransactions(page);
            }
            if (esiResponse != null)
            {
                if (esiResponse.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var list = esiResponse.Data.Select(p=>new Core.Models.Wallet.TransactionEntry(p)).ToList();
                    if(list.NotNullOrEmpty())
                    {
                        await SetNames(list);
                    }
                    return list;
                }
                else
                {
                    Log.Error(esiResponse.Message);
                }
            }
            else
            {
                _window.ShowError("None");
            }
            return null;
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
                    if(invTypesDic.TryGetValue(transaction.Transaction.TypeId, out var invType))
                    {
                        transaction.InvType = invType;
                    }
                    else
                    {
                        transaction.InvType = new Core.DBModels.InvType()
                        {
                            TypeName = transaction.Transaction.TypeId.ToString(),
                            TypeID = transaction.Transaction.TypeId
                        };
                    }
                }
            }
            //赋值卖家信息
            var clientIds = transactions.Select(p=>p.Transaction.ClientId).ToList();
            if(clientIds.NotNullOrEmpty())
            {
                var resp = await _esiClient.Universe.Names(clientIds.Distinct().ToList());
                if(resp != null)
                {
                    if(resp.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        var namesDic = resp.Data.ToDictionary(p => p.Id);
                        foreach(var transaction in transactions)
                        {
                            if(namesDic.TryGetValue(transaction.Transaction.ClientId, out var name))
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
                        _window.ShowError(resp.Message);
                        Log.Error(resp.Message);
                    }
                }
                else
                {
                    _window.ShowError("None");
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
                    var structuresResp = await _esiClient.Universe.Names(structures.Select(p => (int)p).ToList());
                    if(structuresResp != null && structuresResp.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        foreach (var data in structuresResp.Data)
                        {
                            locationNames.Add(data.Id, data.Name);
                        }
                    }
                    else
                    {
                        Log.Error(structuresResp?.Message);
                        _window.ShowError(structuresResp?.Message);
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
            _window.ShowWaiting();
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
                            NavigatePageControl_CharacterTransaction_OnPageChanged(1);
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
                            NavigatePageControl_CorpTransaction_OnPageChanged(1);
                        }
                    } break;
            }
            _window.HideWaiting();
        }

        private async void NavigatePageControl_CharacterJournal_OnPageChanged(int page)
        {
            _window?.ShowWaiting();
            DataGrid_CharacterJournal.ItemsSource = await GetJournalsAsync(false, page);
            _window?.HideWaiting();
        }

        private async void NavigatePageControl_CharacterTransaction_OnPageChanged(int page)
        {
            _window?.ShowWaiting();
            DataGrid_CharacterTransaction.ItemsSource = await GetTransactionsAsync(false, page);
            _window?.HideWaiting();
        }

        private async void NavigatePageControl_CorpJournal_OnPageChanged(int page)
        {
            _window?.ShowWaiting();
            DataGrid_CorpJournal.ItemsSource = await GetJournalsAsync(true, page);
            _window?.HideWaiting();
        }

        private async void NavigatePageControl_CorpTransaction_OnPageChanged(int page)
        {
            _window?.ShowWaiting();
            DataGrid_CorpTransaction.ItemsSource = await GetTransactionsAsync(true, page);
            _window?.HideWaiting();
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
