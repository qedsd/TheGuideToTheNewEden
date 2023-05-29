using ESI.NET;
using ESI.NET.Models.Opportunities;
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
using TheGuideToTheNewEden.WinUI.Helpers;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.WinUI.Views.Character
{
    public sealed partial class ContractPage : Page, ICharacterPage
    {
        private BaseWindow _window;
        private EsiClient _esiClient;
        public ContractPage()
        {
            this.InitializeComponent();
            Loaded += ContractPage_Loaded;
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _esiClient = e.Parameter as EsiClient;
        }
        private void ContractPage_Loaded(object sender, RoutedEventArgs e)
        {
            _window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
            if (!_isLoaded)
            {
                Refresh();
                _isLoaded = true;
            }
        }
        private bool _isLoaded = false;
        public void Clear()
        {
            _isLoaded = false;
        }

        public void Refresh()
        {
            if(MainPivot.SelectedIndex == 0)
            {
                GetCharacterContractInfos(1);
            }
            else
            {
                MainPivot.SelectedIndex = 0;
            }
        }

        private bool _characterLoaded = false;
        private bool _corpLoaded = false;
        private void MainPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch ((sender as Pivot).SelectedIndex)
            {
                case 0:
                    {
                        if (!_characterLoaded)
                        {
                            _characterLoaded = true;
                            GetCharacterContractInfos(1);
                        }
                    }
                    break;
                case 1:
                    {
                        if (!_corpLoaded)
                        {
                            _corpLoaded = true;
                            GetCorpContractInfos(1);
                        }
                    }
                    break;
            }
        }

        private async void GetCharacterContractInfos(int page)
        {
            _window?.ShowWaiting();
            var resp = await Core.Services.ESIService.Current.EsiClient.Contracts.CharacterContracts(page);
            if (resp != null && resp.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var datas = resp.Data.Select(p => new Core.Models.Contract.ContractInfo(p)).ToList();
                if (datas.NotNullOrEmpty())
                {
                    await ContractInfoHelper.CompleteinfoAsync(datas);
                }
                DataGrid_Character.ItemsSource = datas;
            }
            else
            {
                Core.Log.Error(resp?.Message);
                _window?.ShowError(resp?.Message, true);
            }
            _window?.HideWaiting();
        }
        private async void GetCorpContractInfos(int page)
        {
            _window?.ShowWaiting();
            var resp = await Core.Services.ESIService.Current.EsiClient.Contracts.CorporationContracts(page);
            if (resp != null && resp.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var datas = resp.Data.Select(p => new Core.Models.Contract.ContractInfo(p)).ToList();
                if (datas.NotNullOrEmpty())
                {
                    await ContractInfoHelper.CompleteinfoAsync(datas);
                }
                DataGrid_Corp.ItemsSource = datas;
            }
            else
            {
                Core.Log.Error(resp?.Message);
                _window?.ShowError(resp?.Message, true);
            }
            _window?.HideWaiting();
        }

        private void NavigatePageControl_Corp_OnPageChanged(int page)
        {
            GetCorpContractInfos(page);
        }

        private void NavigatePageControl_Character_OnPageChanged(int page)
        {
            GetCharacterContractInfos(page);
        }

        private void DataGrid_Character_SelectionChanged(object sender, Syncfusion.UI.Xaml.Grids.GridSelectionChangedEventArgs e)
        {
            new Wins.ContractDetailWindow(_esiClient, DataGrid_Character.SelectedItem as Core.Models.Contract.ContractInfo, 1).Activate();
        }

        private void DataGrid_Corp_SelectionChanged(object sender, Syncfusion.UI.Xaml.Grids.GridSelectionChangedEventArgs e)
        {
            new Wins.ContractDetailWindow(_esiClient, DataGrid_Corp.SelectedItem as Core.Models.Contract.ContractInfo, 2).Activate();
        }
    }
}
