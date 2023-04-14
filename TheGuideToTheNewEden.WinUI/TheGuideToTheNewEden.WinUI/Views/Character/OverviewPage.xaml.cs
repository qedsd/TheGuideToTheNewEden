using ESI.NET;
using ESI.NET.Models.SSO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views.Character
{
    public sealed partial class OverviewPage : Page
    {
        private EsiClient _esiClient;
        private AuthorizedCharacterData _characterData;
        public OverviewPage()
        {
            this.InitializeComponent();
        }
        
        public void Set(EsiClient esiClient, AuthorizedCharacterData characterData)
        {
            if(characterData != _characterData)
            {
                _characterData = characterData;
                _esiClient = esiClient;
                Refresh();
            }
        }

        public async void Refresh()
        {
            ESI.NET.Models.Location.Activity onlineStatus = null;
            ESI.NET.Models.Location.Location location = null;
            ESI.NET.Models.Location.Ship ship = null;
            List<ESI.NET.Models.Universe.ResolvedInfo> resolvedInfo = null;
            List<long> ids = new List<long>();
            if (_characterData.CorporationID > 0)
            {
                ids.Add(_characterData.CorporationID);
            }
            if (_characterData.AllianceID > 0)
            {
                ids.Add(_characterData.AllianceID);
            }
            var tasks = new Task[]
            {
                ESIService.Current.EsiClient.Location.Online().ContinueWith((p)=>
                {
                    if(p?.Result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        onlineStatus = p.Result.Data;
                    }
                    else
                    {
                        Log.Error(p?.Result.Message);
                    }
                }),
                ESIService.Current.EsiClient.Location.Location().ContinueWith((p)=>
                {
                    if(p?.Result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        location = p.Result.Data;
                    }
                    else
                    {
                        Log.Error(p?.Result.Message);
                    }
                }),
                ESIService.Current.EsiClient.Location.Ship().ContinueWith((p)=>
                {
                    if(p?.Result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        ship = p.Result.Data;
                    }
                    else
                    {
                        Log.Error(p?.Result.Message);
                    }
                }),
                ESIService.Current.EsiClient.Universe.Names(ids).ContinueWith((p)=>
                {
                    if(p?.Result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        resolvedInfo = p.Result.Data;
                    }
                    else
                    {
                        Log.Error(p?.Result.Message);
                    }
                }),
            };
            await Task.WhenAll(tasks);
            if (_characterData.CorporationID > 0)
            {
                Image_Corporation.Source = Converters.GameImageConverter.GetImageUri(_characterData.CorporationID, Converters.GameImageConverter.ImgType.Corporation);
                var name = resolvedInfo.FirstOrDefault(p => p.Id == _characterData.CorporationID);
                if (name != null)
                {
                    TextBlock_CorpName.Visibility = Visibility.Visible;
                    TextBlock_CorpName.Text = name.Name;
                }
                else
                {
                    TextBlock_CorpName.Visibility = Visibility.Collapsed;
                }
            }
            if (_characterData.AllianceID > 0)
            {
                AllianceStackPanel.Visibility = Visibility.Visible;
                Image_Alliance.Source = Converters.GameImageConverter.GetImageUri(_characterData.AllianceID, Converters.GameImageConverter.ImgType.Alliance);
                var name = resolvedInfo.FirstOrDefault(p => p.Id == _characterData.AllianceID);
                if (name != null)
                {
                    TextBlock_AllianceName.Visibility = Visibility.Visible;
                    TextBlock_AllianceName.Text = name.Name;
                }
                else
                {
                    TextBlock_AllianceName.Visibility = Visibility.Collapsed;
                }
            }

            if(location.StationId > 0)
            {
                var station = await Core.Services.DB.StaStationService.QueryAsync(location.StationId);
                if(station != null)
                {

                }
            }
        }
    }
}
