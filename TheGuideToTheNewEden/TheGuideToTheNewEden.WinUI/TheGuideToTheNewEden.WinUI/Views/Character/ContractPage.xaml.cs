// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

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
using Windows.Foundation;
using Windows.Foundation.Collections;

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

        public async void Refresh()
        {
            _window?.ShowWaiting();
            var resp = await _esiClient.Contracts.CharacterContracts();
            if (resp != null && resp.StatusCode == System.Net.HttpStatusCode.OK)
            {
                //TODO:
                var groups = resp.Data.GroupBy(p => p.Type).ToList();
                foreach (var group in groups)
                {

                }
            }
            else
            {
                Core.Log.Error(resp?.Message);
                _window.ShowError(resp?.Message);
            }
            _window?.HideWaiting();
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

                        }
                    }
                    break;
                case 1:
                    {
                        if (!_corpLoaded)
                        {
                            _corpLoaded = true;
                        }
                    }
                    break;
            }
        }
    }
}
