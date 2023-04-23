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
    public sealed partial class OverviewPage2 : Page
    {
        private EsiClient _esiClient;
        private AuthorizedCharacterData _characterData;
        public OverviewPage2()
        {
            this.InitializeComponent();
        }

        public void Set(EsiClient esiClient, AuthorizedCharacterData characterData)
        {
            if (characterData != _characterData)
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
            ESI.NET.Models.Corporation.Corporation corporation = null;
            ESI.NET.Models.Alliance.Alliance alliance = null;
            var tasks = new List<Task>
            {
                _esiClient.Location.Online().ContinueWith((p)=>
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
                _esiClient.Location.Location().ContinueWith((p)=>
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
                _esiClient.Location.Ship().ContinueWith((p)=>
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
                _esiClient.Corporation.Information(_characterData.CorporationID).ContinueWith((p) =>
                {
                    if (p?.Result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        corporation = p.Result.Data;
                    }
                    else
                    {
                        Log.Error(p?.Result.Message);
                    }
                }),
            };
            //if (_characterData.CorporationID > 0)
            //{
            //    tasks.Add(new Task(() =>
            //    {
            //        _esiClient.Corporation.Information(_characterData.CorporationID).ContinueWith((p) =>
            //        {
            //            if (p?.Result.StatusCode == System.Net.HttpStatusCode.OK)
            //            {
            //                corporation = p.Result.Data;
            //            }
            //            else
            //            {
            //                Log.Error(p?.Result.Message);
            //            }
            //        });
            //    }));
            //}
            //if (_characterData.AllianceID > 0)
            //{
            //    tasks.Add(new Task(() =>
            //    {
            //        _esiClient.Alliance.Information(_characterData.AllianceID).ContinueWith((p) =>
            //        {
            //            if (p?.Result.StatusCode == System.Net.HttpStatusCode.OK)
            //            {
            //                alliance = p.Result.Data;
            //            }
            //            else
            //            {
            //                Log.Error(p?.Result.Message);
            //            }
            //        });
            //    }));
            //}
            await Task.WhenAll(tasks);
            if (corporation != null)
            {
                CorpStackPanel.Visibility = Visibility.Visible;
                Image_Corporation.Source = Converters.GameImageConverter.GetImageUri(_characterData.CorporationID, Converters.GameImageConverter.ImgType.Corporation);
                TextBlock_CorpName.Visibility = Visibility.Visible;
                TextBlock_CorpName.Text = corporation.Name;
                TextBlock_CorpTicker.Text = corporation.Ticker;
            }
            else
            {
                CorpStackPanel.Visibility = Visibility.Visible;
            }
            if (alliance != null)
            {
                AllianceStackPanel.Visibility = Visibility.Visible;
                Image_Alliance.Source = Converters.GameImageConverter.GetImageUri(_characterData.AllianceID, Converters.GameImageConverter.ImgType.Alliance);
                TextBlock_AllianceName.Visibility = Visibility.Visible;
                TextBlock_AllianceName.Text = alliance.Name;
                TextBlock_AllianceTicker.Text = alliance.Ticker;
            }
            else
            {
                AllianceStackPanel.Visibility = Visibility.Visible;
            }

            var solarSystem = await Core.Services.DB.MapSolarSystemService.QueryAsync(location.SolarSystemId);
            TextBlock_LocationSystemLevel.Text = solarSystem?.Security.ToString();
            TextBlock_LocationSystemName.Text = solarSystem?.SolarSystemName;

            if (location.StationId > 0)
            {
                var station = await Core.Services.DB.StaStationService.QueryAsync(location.StationId);
                if (station != null)
                {
                    TextBlock_LocationSataionName.Text = station.StationName;
                }
            }
            else if (location.StructureId > 0)
            {
                var structureRsp = await _esiClient.Universe.Structure(location.StructureId);
                if (structureRsp?.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    TextBlock_LocationSataionName.Text = structureRsp.Data.Name;
                }
                else
                {
                    Log.Error(structureRsp?.Message);
                }
            }
            else
            {
                TextBlock_LocationSataionName.Text = string.Empty;
            }

            if (onlineStatus != null)
            {
                Grid_OnlineStatus.Background = new SolidColorBrush(onlineStatus.Online ? Microsoft.UI.Colors.LightSeaGreen : Microsoft.UI.Colors.OrangeRed);
                TextBlock_lastLogin.Text = onlineStatus.LastLogin.ToString("g");
                TextBlock_LastLogout.Text = onlineStatus.LastLogout.ToString("g");
                TextBlock_LoginCount.Text = onlineStatus.Logins.ToString();
            }
        }
    }
}
