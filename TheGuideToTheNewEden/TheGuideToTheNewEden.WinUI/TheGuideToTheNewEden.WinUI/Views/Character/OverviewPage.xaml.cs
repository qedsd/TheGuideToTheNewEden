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
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core;
using ESI.NET.Models.Skills;
using System.Text.RegularExpressions;

namespace TheGuideToTheNewEden.WinUI.Views.Character
{
    public sealed partial class OverviewPage : Page,ICharacterPage
    {
        private EsiClient _esiClient;
        private AuthorizedCharacterData _characterData;
        public OverviewPage()
        {
            this.InitializeComponent();
            Loaded += OverviewPage_Loaded;
            TextBlock_lastLoginText.Text = "最近上线：";
            TextBlock_LoginCountText.Text = "上线次数：";
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var paras = e.Parameter as object[];
            if(paras != null && paras.Length == 2)
            {
                _esiClient = paras[0] as EsiClient;
                _characterData = paras[1] as AuthorizedCharacterData;
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
            ESI.NET.Models.Location.Activity onlineStatus = null;
            ESI.NET.Models.Location.Location location = null;
            ESI.NET.Models.Location.Ship ship = null;
            ESI.NET.Models.Corporation.Corporation corporation = null;
            ESI.NET.Models.Alliance.Alliance alliance = null;
            List<ESI.NET.Models.Skills.SkillQueueItem> skillQueueItems = null;
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
                        if(ship.ShipName.StartsWith("u'"))
                        {
                            ship.ShipName = Regex.Unescape(ship.ShipName.Substring(2,ship.ShipName.Length - 3));
                        }
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
                _esiClient.Skills.Queue().ContinueWith((p) =>
                {
                    if (p?.Result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        skillQueueItems = p.Result.Data;
                    }
                    else
                    {
                        Log.Error(p?.Result.Message);
                    }
                }),
            };
            if (_characterData.AllianceID > 0)
            {
                var allianceTask = new Task(() =>
                {
                    _esiClient.Alliance.Information(_characterData.AllianceID).ContinueWith((p) =>
                    {
                        if (p?.Result.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            alliance = p.Result.Data;
                        }
                        else
                        {
                            Log.Error(p?.Result.Message);
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
                var solarSystem = await Core.Services.DB.MapSolarSystemService.QueryAsync(location.SolarSystemId);
                TextBlock_LocationSystemLevel.Text = solarSystem?.Security.ToString("N2");
                TextBlock_LocationSystemName.Content = solarSystem?.SolarSystemName;
            }

            if (location?.StationId > 0)
            {
                var station = await Core.Services.DB.StaStationService.QueryAsync(location.StationId);
                if (station != null)
                {
                    TextBlock_LocationSataionName.Text = station.StationName;
                }
            }
            else if (location?.StructureId > 0)
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
                Grid_Online.Visibility = onlineStatus.Online ? Visibility.Visible: Visibility.Collapsed;
                Grid_Outline.Visibility = onlineStatus.Online ? Visibility.Collapsed : Visibility.Visible;
                TextBlock_lastLogin.Text = onlineStatus.LastLogin.ToString("g");
                TextBlock_LoginCount.Text = onlineStatus.Logins.ToString();
            }

            if(skillQueueItems.NotNullOrEmpty())
            {
                List<Core.Models.Character.SkillQueueItem> skills = new List<Core.Models.Character.SkillQueueItem>();
                var types = await Core.Services.DB.InvTypeService.QueryTypesAsync(skillQueueItems.Select(p => p.SkillId).ToList());
                if(types.NotNullOrEmpty())
                {
                    var dic = types.ToDictionary(p => p.TypeID);
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
