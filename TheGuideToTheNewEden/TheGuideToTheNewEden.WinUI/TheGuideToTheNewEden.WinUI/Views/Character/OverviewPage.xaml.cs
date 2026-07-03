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
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core;
using System.Text.RegularExpressions;
using TheGuideToTheNewEden.WinUI.Converters;
using Newtonsoft.Json;
using EVEStandard;
using EVEStandard.Models.API;
using EVEStandard.Enumerations;
using TheGuideToTheNewEden.Core.Models.Character;

namespace TheGuideToTheNewEden.WinUI.Views.Character
{
    public sealed partial class OverviewPage : Page,ICharacterPage
    {
        private EVEStandardAPI _esiClient;
        private AuthDTO _auth;
        private Core.Models.Character.AuthorizedCharacterData _characterData;
        public OverviewPage()
        {
            this.InitializeComponent();
            Loaded += OverviewPage_Loaded;
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var paras = e.Parameter as object[];
            if(paras != null && paras.Length == 2)
            {
                _esiClient = paras[0] as EVEStandardAPI;
                _characterData = paras[1] as Core.Models.Character.AuthorizedCharacterData;
            }
        }
        private void OverviewPage_Loaded(object sender, RoutedEventArgs e)
        {
            if(!_isLoaded)
            {
                Refresh();
                _isLoaded = true;
            }
        }

        public async void Refresh()
        {
            EVEStandard.Models.CharacterOnline onlineStatus = null;
            EVEStandard.Models.Location location = null;
            EVEStandard.Models.CharacterShip ship = null;
            EVEStandard.Models.CorporationInfo corporation = null;
            EVEStandard.Models.Alliance alliance = null;
            List<EVEStandard.Models.SkillQueue> skillQueueItems = null;
            string locationError = string.Empty;
            var tasks = new List<Task>
            {
                _esiClient.Location.GetCharacterOnlineAsync(_auth).ContinueWith((p)=>
                {
                    if(p.Result.Model != null)
                    {
                        onlineStatus = p.Result.Model;
                    }
                    else
                    {
                        Log.Error("GetCharacterOnlineAsync Failed");
                    }
                }),
                _esiClient.Location.GetCharacterLocationAsync(_auth).ContinueWith((p)=>
                {
                    //if(p.Result.Model != null)
                    //{
                    //    location = p.Result.Model;
                    //}
                    //else
                    //{
                    //    Log.Error("GetCharacterLocationAsync Failed");
                    //}
                }),
                _esiClient.Location.GetCurrentShipAsync(_auth).ContinueWith((p)=>
                {
                    if(p.Result.Model != null)
                    {
                        ship = p.Result.Model;
                        if(ship.ShipName.StartsWith("u'"))
                        {
                            ship.ShipName = Regex.Unescape(ship.ShipName.Substring(2,ship.ShipName.Length - 3));
                        }
                    }
                    else
                    {
                        Log.Error("GetCurrentShipAsync Failed");
                    }
                }),
                _esiClient.Corporation.GetCorporationInfoAsync(_characterData.CorporationID).ContinueWith((p) =>
                {
                    if (p.Result.Model != null)
                    {
                        corporation = p.Result.Model;
                    }
                    else
                    {
                        Log.Error("GetCorporationInfoAsync Failed");
                    }
                }),
                _esiClient.Skills.GetCharacterSkillQueueAsync(_auth).ContinueWith((p) =>
                {
                    if (p.Result.Model != null)
                    {
                        skillQueueItems = p.Result.Model;
                    }
                    else
                    {
                        Log.Error("GetCharacterSkillQueueAsync Failed");
                    }
                }),
            };
            if (_characterData.AllianceID > 0)
            {
                var allianceTask = new Task(() =>
                {
                    _esiClient.Alliance.GetAllianceInfoAsync(_characterData.AllianceID).ContinueWith((p) =>
                    {
                        if (p.Result.Model != null)
                        {
                            alliance = p.Result.Model;
                        }
                        else
                        {
                            Log.Error("GetAllianceInfoAsync Failed");
                        }
                    });
                });
                allianceTask.Start();
                tasks.Add(allianceTask);
            }
            await Task.WhenAll(tasks);
            if (corporation != null)
            {
                CorpStackPanel.Visibility = Visibility.Visible;
                Image_Corporation.Source = Converters.GameImageConverter.GetImageUri(_characterData.CorporationID, Converters.GameImageConverter.ImgType.Corporation, 64);
                TextBlock_CorpName.Visibility = Visibility.Visible;
                TextBlock_CorpName.Text = corporation.Name;
                TextBlock_CorpTicker.Text = corporation.Ticker;
            }
            else
            {
                CorpStackPanel.Visibility = Visibility.Collapsed;
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
                AllianceStackPanel.Visibility = Visibility.Collapsed;
            }
            if(ship != null)
            {
                Image_Ship.Source = Converters.GameImageConverter.GetImageUri(ship.ShipTypeId, Converters.GameImageConverter.ImgType.Type);
                TextBlock_ShipName.Text = ship.ShipName;
                var shipItem = await Core.Services.DB.InvTypeService.QueryTypeAsync(ship.ShipTypeId);
                if(shipItem != null)
                {
                    TextBlock_ShipTypeName.Text = shipItem.TypeName;
                }
            }
            if(location != null)
            {
                LocationSystemInfoPanel.Visibility = Visibility.Visible;
                //var solarSystem = await Core.Services.DB.MapSolarSystemService.QueryAsync(location.);
                //TextBlock_LocationSystemLevel.Text = solarSystem?.Security.ToString("N2");
                //TextBlock_LocationSystemName.Content = solarSystem?.SolarSystemName;
                if (location.LocationId > 0)
                {
                    if(location.LocationType == LocationType.station.ToString())
                    {
                        var station = await Core.Services.DB.StaStationService.QueryAsync(location.LocationId.Value);
                        if (station != null)
                        {
                            TextBlock_LocationSataionName.Text = station.StationName;
                        }
                    }
                    else
                    {
                        var structureRsp = await _esiClient.Universe.GetStructureInfoAsync(_auth, location.LocationId.Value);
                        if (structureRsp.Model != null)
                        {
                            TextBlock_LocationSataionName.Text = structureRsp.Model.Name;
                        }
                        else
                        {
                            TextBlock_LocationSataionName.Text = location.LocationId.ToString();
                            Log.Error("GetStructureInfoAsync Failed");
                        }
                    }
                }
                else
                {
                    TextBlock_LocationSataionName.Text = string.Empty;
                }
            }
            else
            {
                TextBlock_LocationSataionName.Text = locationError;
                LocationSystemInfoPanel.Visibility = Visibility.Collapsed;
            }
            

            if (onlineStatus != null)
            {
                Grid_Online.Visibility = onlineStatus.Online ? Visibility.Visible: Visibility.Collapsed;
                Grid_Outline.Visibility = onlineStatus.Online ? Visibility.Collapsed : Visibility.Visible;
                var duration = DateTime.UtcNow - (DateTime)onlineStatus.LastLogin;
                if (duration.TotalDays > 365)
                {
                    TextBlock_lastLogin.Text = $"{duration.TotalDays / 365:N0}y {duration.TotalDays % 365 / 30:N1}mo";
                }
                else
                {
                    if (duration.TotalDays > 30)
                    {
                        TextBlock_lastLogin.Text = $"{duration.TotalDays / 30:N0}mo {duration.TotalDays % 30:N1}d";
                    }
                    else
                    {
                        if (duration.TotalDays > 1)
                        {
                            TextBlock_lastLogin.Text = $"{duration.Days}d {duration.Hours + duration.Minutes / 60.0:N1}h";
                        }
                        else
                        {
                            TextBlock_lastLogin.Text = $"{duration.Hours}h {duration.Minutes:N0}min";
                        }
                    }
                }
                ToolTip toolTip = new ToolTip();
                toolTip.Content = UTCToLocalTimeConverter.Convert(onlineStatus.LastLogin.Value);
                ToolTipService.SetToolTip(TextBlock_lastLogin, toolTip);
                TextBlock_LoginCount.Text = onlineStatus.Logins.ToString();
            }

            if(skillQueueItems.NotNullOrEmpty())
            {
                List<Core.Models.Character.SkillQueueItem> skills = new List<Core.Models.Character.SkillQueueItem>();
                var types = await Core.Services.DB.InvTypeService.QueryTypesAsync(skillQueueItems.Select(p => p.SkillId).ToList());
                if(types.NotNullOrEmpty())
                {
                    var dic = types.ToDictionary(p => (long)p.TypeID);
                    foreach (var skill in skillQueueItems)
                    {
                        if(dic.TryGetValue(skill.SkillId, out var invType))
                        {
                            Core.Models.Character.SkillQueueItem skillQueueItem = new Core.Models.Character.SkillQueueItem(skill);
                            skillQueueItem.SkillName = invType.TypeName;
                            skills.Add(skillQueueItem);
                        }
                    }
                }
                if(skills.Any())
                {
                    ListView_SkillQueue.ItemsSource = skills.OrderBy(p=>p.QueuePosition).ToList();
                }
            }
        }
        private bool _isLoaded = false;
        public void Clear()
        {
            _isLoaded = false;
        }
    }
}
