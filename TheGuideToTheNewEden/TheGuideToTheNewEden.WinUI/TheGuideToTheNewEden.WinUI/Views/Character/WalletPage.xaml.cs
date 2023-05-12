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

namespace TheGuideToTheNewEden.WinUI.Views.Character
{
    public sealed partial class WalletPage : Page, ICharacterPage
    {
        private EsiClient _esiClient;
        private BaseWindow _window;
        private ObservableCollection<Core.Models.Wallet.JournalEntry> _characterJournals = new ObservableCollection<Core.Models.Wallet.JournalEntry>();
        private ObservableCollection<Core.Models.Wallet.JournalEntry> _corpJournals = new ObservableCollection<Core.Models.Wallet.JournalEntry>();
        private ObservableCollection<Core.Models.Wallet.TransactionEntry> _characterTransactions = new ObservableCollection<Core.Models.Wallet.TransactionEntry>();
        private ObservableCollection<Core.Models.Wallet.TransactionEntry> _corpTransactions = new ObservableCollection<Core.Models.Wallet.TransactionEntry>();
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
            if(MainPivot.SelectedIndex == 0)
            {
                Pivot_SelectionChanged(MainPivot,null);
            }
            else
            {
                MainPivot.SelectedIndex = 0;
            }
            _window.HideWaiting();
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
                    _window.ShowError(esiResponse.Message);
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
                esiResponse = await _esiClient.Wallet.CorporationTransactions(page);
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
                    _window.ShowError(esiResponse.Message);
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
            var clientIds = transactions.Select(p=>(long)p.Transaction.ClientId).ToList();
            if(clientIds.NotNullOrEmpty())
            {
                var resp = await _esiClient.Universe.Names(clientIds);
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
            //TODO:LocationId是什么
            //var clientIds = transactions.Select(p => p.Transaction.LocationId).ToList();
        }


        private bool _characterJournalsLoaded = false;
        private bool _corpJournalsLoaded = false;
        private bool _characterTransactionsLoaded = false;
        private bool _corpTransactionsLoaded = false;
        private async void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _window.ShowWaiting();
            switch((sender as Pivot).SelectedIndex)
            {
                case 0: 
                    {
                        if(!_characterJournalsLoaded)
                        {
                            _characterJournalsLoaded = true;
                            var data = await GetJournalsAsync(false,1);
                            if(data.NotNullOrEmpty())
                            {
                                foreach(var d in data)
                                {
                                    _characterJournals.Add(d);
                                }
                            }
                            else
                            {
                                _characterJournals.Clear();
                            }
                            DataGrid_CharacterJournal.ItemsSource = _characterJournals;
                        }
                    }break;
                case 1: 
                    {
                        if (!_characterTransactionsLoaded)
                        {
                            _characterTransactionsLoaded = true;
                            var data = await GetTransactionsAsync(false, 1);
                            await SetNames(data);
                            if (data.NotNullOrEmpty())
                            {
                                foreach (var d in data)
                                {
                                    _characterTransactions.Add(d);
                                }
                            }
                            else
                            {
                                _characterTransactions.Clear();
                            }
                            DataGrid_CharacterTransaction.ItemsSource = _characterTransactions;
                        }
                    } break;
                case 2: 
                    {
                        if (!_corpJournalsLoaded)
                        {
                            _corpJournalsLoaded = true;
                            var data = await GetJournalsAsync(true, 1);
                            if (data.NotNullOrEmpty())
                            {
                                foreach (var d in data)
                                {
                                    _corpJournals.Add(d);
                                }
                            }
                            else
                            {
                                _corpJournals.Clear();
                            }
                            DataGrid_CorpJournal.ItemsSource = _corpJournals;
                        }
                    } break;
                case 3:
                    {
                        if (!_corpTransactionsLoaded)
                        {
                            _corpTransactionsLoaded = true;
                            var data = await GetTransactionsAsync(true, 1);
                            await SetNames(data);
                            if (data.NotNullOrEmpty())
                            {
                                foreach (var d in data)
                                {
                                    _corpTransactions.Add(d);
                                }
                            }
                            else
                            {
                                _corpTransactions.Clear();
                            }
                            DataGrid_CorpTransaction.ItemsSource = _corpTransactions;
                        }
                    } break;
            }
            _window.HideWaiting();
        }
    }
}
