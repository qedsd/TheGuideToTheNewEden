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
using TheGuideToTheNewEden.WinUI.Extensions;
using EVEStandard;
using EVEStandard.API;
using EVEStandard.Models.API;

namespace TheGuideToTheNewEden.WinUI.Views.Character
{
    public sealed partial class ContractPage : Page, ICharacterPage, IPage
    {
        private EVEStandardAPI _esiClient;
        private AuthDTO _auth;
        private Core.Models.Character.AuthorizedCharacterData _characterData;
        public ContractPage()
        {
            this.InitializeComponent();
            Loaded += ContractPage_Loaded;
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var paras = e.Parameter as object[];
            if (paras != null && paras.Length == 2)
            {
                _esiClient = paras[0] as EVEStandardAPI;
                _characterData = paras[1] as Core.Models.Character.AuthorizedCharacterData;
                _auth = _characterData?.Auth;
            }
        }
        private void ContractPage_Loaded(object sender, RoutedEventArgs e)
        {
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
            _characterLoaded = false;
            _corpLoaded = false;
            if (MainPivot.SelectedIndex == 0)
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
            this.ShowWaiting();
            var resp = await _esiClient.Contracts.GetContractsAsync(_auth, page);
            if (resp.Model != null)
            {
                var datas = resp.Model.Select(p => new Core.Models.Contract.ContractInfo(p)).ToList();
                if (datas.NotNullOrEmpty())
                {
                    await ContractInfoHelper.CompleteinfoAsync(datas);
                }
                DataGrid_Character.ItemsSource = datas;
            }
            else
            {
                this.ShowError("GetCharacterContractInfos Failed");
            }
            this.HideWaiting();
        }
        private async void GetCorpContractInfos(int page)
        {
            this.ShowWaiting();
            var resp = await _esiClient.Contracts.GetCorporationContractsAsync(_auth, _characterData.CorporationID,page);
            if (resp.Model != null)
            {
                var datas = resp.Model.Select(p => new Core.Models.Contract.ContractInfo(p)).ToList();
                if (datas.NotNullOrEmpty())
                {
                    await ContractInfoHelper.CompleteinfoAsync(datas);
                }
                DataGrid_Corp.ItemsSource = datas;
            }
            else
            {
                this.ShowError("GetCorpContractInfos Failed");
            }
            this.HideWaiting();
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
            if(DataGrid_Character.SelectedItem == null)
            {
                return;
            }
            new Wins.ContractDetailWindow(_esiClient, _auth, DataGrid_Character.SelectedItem as Core.Models.Contract.ContractInfo, 1).Activate();
        }

        private void DataGrid_Corp_SelectionChanged(object sender, Syncfusion.UI.Xaml.Grids.GridSelectionChangedEventArgs e)
        {
            if (DataGrid_Corp.SelectedItem == null)
            {
                return;
            }
            new Wins.ContractDetailWindow(_esiClient, _auth, DataGrid_Corp.SelectedItem as Core.Models.Contract.ContractInfo, 2).Activate();
        }

        public void Close()
        {
            
        }
        public void NavigatedTo(object parameter)
        {

        }
    }
}
