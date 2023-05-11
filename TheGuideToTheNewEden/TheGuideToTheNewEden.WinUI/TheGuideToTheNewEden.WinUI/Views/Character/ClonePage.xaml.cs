using ESI.NET;
using ESI.NET.Models.Skills;
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
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.Core.Extensions;
using ESI.NET.Models.Location;
using ESI.NET.Models.Universe;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.WinUI.Views.Character
{
    public sealed partial class ClonePage : Page, ICharacterPage
    {
        private BaseWindow _window;
        private EsiClient _esiClient;
        public ClonePage()
        {
            this.InitializeComponent();
            Loaded += ClonePage_Loaded;
        }

        private void ClonePage_Loaded(object sender, RoutedEventArgs e)
        {
            _window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
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
        public async void Refresh()
        {
            _window.ShowWaiting();
            var result = await _esiClient.Clones.List();
            if (result.StatusCode != System.Net.HttpStatusCode.OK)
            {
                _window.HideWaiting();
                _window.ShowError(result.Message);
                Log.Error(result.Message);
            }
            else
            {
                var result2 = await _esiClient.Clones.Implants();
                if (result2.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    _window.HideWaiting();
                    _window.ShowError(result2.Message);
                    Log.Error(result2.Message);
                }
                else
                {
                    #region 基地等信息
                    if(result.Data.HomeLocation.LocationType == "station")
                    {
                        var staStation = await Core.Services.DB.StaStationService.QueryAsync((int)result.Data.HomeLocation.LocationId);
                        if (staStation != null)
                        {
                            TextBlock_HomeLocation.Text = staStation.StationName;
                        }
                        else
                        {
                            TextBlock_HomeLocation.Text = result.Data.HomeLocation.LocationId.ToString();
                        }
                    }
                    else
                    {
                        var structureRsp = await _esiClient.Universe.Structure(result.Data.HomeLocation.LocationId);
                        if (structureRsp?.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            TextBlock_HomeLocation.Text = structureRsp.Data.Name;
                        }
                        else
                        {
                            Log.Error(structureRsp?.Message);
                            _window.ShowError(structureRsp?.Message, true);
                        }
                    }
                    TextBlock_LastStationChangeDate.Text = result.Data.LastStationChangeDate == DateTime.MinValue ? "None" : result.Data.LastStationChangeDate.ToLocalTime().ToString();
                    TextBlock_JumpClonesCount.Text = (result.Data.JumpClones.Count + 1).ToString();
                    TextBlock_LastCloneJumpDate.Text = result.Data.LastCloneJumpDate == DateTime.MinValue ? "None" : result.Data.LastCloneJumpDate.ToLocalTime().ToString();
                    #endregion


                    var jumpClones = result.Data.JumpClones;
                    var currentCloneImplants = result2.Data;
                   ;
                    List<Core.Models.Clone.JumpClone> allJumpClones = new List<Core.Models.Clone.JumpClone>();
                    if(currentCloneImplants.NotNullOrEmpty())
                    {
                        string activeCloneStr = "Active clone";
                        if (Application.Current.Resources.TryGetValue("ClonePage_ActiveClone", out var str))
                        {
                            activeCloneStr = str.ToString();
                        }
                        allJumpClones.Add(new Core.Models.Clone.JumpClone()
                        {
                            Clone = new ESI.NET.Models.Clones.JumpClone()
                            {
                                Name = activeCloneStr,
                                Implants = currentCloneImplants
                            }
                        });
                    }
                    if (jumpClones.NotNullOrEmpty())
                    { 
                        foreach(var jumpClone in jumpClones)
                        {
                            allJumpClones.Add(new Core.Models.Clone.JumpClone()
                            {
                                Clone = jumpClone
                            });
                        }
                    }

                    #region 赋值植入体信息
                    List<int> implantsIds = new List<int>();
                    if(allJumpClones.NotNullOrEmpty())
                    {
                        allJumpClones.ForEach(p => implantsIds.AddRange(p.Clone.Implants));
                    }
                    if(implantsIds.Any())
                    {
                        var implantsTypes = await Core.Services.DB.InvTypeService.QueryTypesAsync(implantsIds.Distinct().ToList());
                        if(implantsTypes.NotNullOrEmpty())
                        {
                            var implantsTypesDic = implantsTypes.ToDictionary(p => p.TypeID);
                            foreach (var clone in allJumpClones)
                            {
                                if(clone.Clone.Implants.NotNullOrEmpty())
                                {
                                    clone.CloneImplants = new List<Core.DBModels.InvType>();
                                    foreach (var implant in clone.Clone.Implants)
                                    {
                                        if (implantsTypesDic.TryGetValue(implant, out var invType))
                                        {
                                            clone.CloneImplants.Add(invType);
                                        }
                                        else
                                        {
                                            clone.CloneImplants.Add(new Core.DBModels.InvType()
                                            {
                                                TypeID = implant,
                                                TypeName = implant.ToString()
                                            });
                                        }
                                    }
                                }
                            }
                        } 
                    }
                    #endregion

                    #region 赋值位置名称
                    if(allJumpClones.Any())
                    {
                        var stations = allJumpClones.Where(p => p.Clone.LocationType == "station").ToList();
                        var structures = allJumpClones.Where(p => p.Clone.LocationType == "structure").ToList();
                        if(stations.NotNullOrEmpty())
                        {
                            var staStations = await Core.Services.DB.StaStationService.QueryAsync(stations.Select(p=>(int)p.Clone.LocationId).ToList());
                            if(staStations.NotNullOrEmpty())
                            {
                                foreach(var station in stations)
                                {
                                    var foundStation = staStations.FirstOrDefault(p => p.StationID == station.Clone.LocationId);
                                    if(foundStation != null)
                                    {
                                        station.LocationName = foundStation.StationName;
                                    }
                                }
                            }
                        }
                        if(structures.NotNullOrEmpty())
                        {
                            foreach (var structure in structures)
                            {
                                var structureRsp = await _esiClient.Universe.Structure(structure.Clone.LocationId);
                                if (structureRsp?.StatusCode == System.Net.HttpStatusCode.OK)
                                {
                                    structure.LocationName = structureRsp.Data.Name;
                                }
                                else
                                {
                                    structure.LocationName = structure.Clone.LocationId.ToString();
                                    Log.Error(structureRsp?.Message);
                                    _window.ShowError(structureRsp?.Message, true);
                                }
                            }
                        }
                    }
                    #endregion

                    ListView_Clones.ItemsSource = allJumpClones;
                    _window.HideWaiting();
                }
            }
        }
    }
}
