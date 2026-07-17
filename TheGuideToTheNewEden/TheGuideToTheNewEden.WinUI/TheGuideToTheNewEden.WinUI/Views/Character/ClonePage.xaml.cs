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
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.WinUI.Extensions;
using EVEStandard;
using EVEStandard.Models.API;

namespace TheGuideToTheNewEden.WinUI.Views.Character
{
    public sealed partial class ClonePage : Page, ICharacterPage
    {
        private EVEStandardAPI _esiClient;
        private AuthDTO _auth;
        private Core.Models.Character.AuthorizedCharacterData _characterData;
        public ClonePage()
        {
            this.InitializeComponent();
            Loaded += ClonePage_Loaded;
        }

        private void ClonePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded)
            {
                Refresh();
                _isLoaded = true;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var paras = e.Parameter as object[];
            if (paras != null && paras.Length == 2)
            {
                _esiClient = paras[0] as EVEStandardAPI;
                _characterData = paras[1] as Core.Models.Character.AuthorizedCharacterData;
                _auth = _characterData.ToAuthDTO();
            }
        }
        private bool _isLoaded = false;
        public void Clear()
        {
            _isLoaded = false;
        }
        public async void Refresh()
        {
            this.ShowWaiting();
            var result = await _esiClient.Clones.GetClonesAsync(_auth);
            this.HideWaiting();
            if (result?.Model != null)
            {
                var result2 = await _esiClient.Clones.GetActiveImplantsAsync(_auth);
                if (result2?.Model != null)
                {
                    #region 基地等信息
                    if (result.Model.HomeLocation.LocationType == "station")
                    {
                        var staStation = await Core.Services.DB.StaStationService.QueryAsync(result.Model.HomeLocation.LocationId.Value);
                        if (staStation != null)
                        {
                            TextBlock_HomeLocation.Text = staStation.StationName;
                        }
                        else
                        {
                            TextBlock_HomeLocation.Text = result.Model.HomeLocation.LocationId.ToString();
                        }
                    }
                    else
                    {
                        var structureRsp = await _esiClient.Universe.GetStructureInfoAsync(_auth, result.Model.HomeLocation.LocationId.Value);
                        if (structureRsp?.Model != null)
                        {
                            TextBlock_HomeLocation.Text = structureRsp.Model.Name;
                        }
                        else
                        {
                            this.ShowError($"GetStructureInfoAsync Failed:{result.Model.HomeLocation.LocationId.Value}", true);
                        }
                    }
                    TextBlock_LastStationChangeDate.Text = result.Model.LastStationChangeDate == DateTime.MinValue ? "None" : result.Model.LastStationChangeDate.Value.ToLocalTime().ToString();
                    TextBlock_JumpClonesCount.Text = (result.Model.JumpClones.Count + 1).ToString();
                    TextBlock_LastCloneJumpDate.Text = result.Model.LastCloneJumpDate == DateTime.MinValue ? "None" : result.Model.LastCloneJumpDate.Value.ToLocalTime().ToString();
                    #endregion

                    var jumpClones = result.Model.JumpClones;
                    var currentCloneImplants = result2.Model;
                    ;
                    List<Core.Models.Clone.JumpClone> allJumpClones = new List<Core.Models.Clone.JumpClone>();
                    if (currentCloneImplants.NotNullOrEmpty())
                    {
                        string activeCloneStr = "Active clone";
                        if (Application.Current.Resources.TryGetValue("ClonePage_ActiveClone", out var str))
                        {
                            activeCloneStr = str.ToString();
                        }
                        allJumpClones.Add(new Core.Models.Clone.JumpClone()
                        {
                            Clone = new EVEStandard.Models.JumpClone()
                            {
                                Name = activeCloneStr,
                                Implants = currentCloneImplants
                            }
                        });
                    }
                    if (jumpClones.NotNullOrEmpty())
                    {
                        foreach (var jumpClone in jumpClones)
                        {
                            allJumpClones.Add(new Core.Models.Clone.JumpClone()
                            {
                                Clone = jumpClone
                            });
                        }
                    }

                    #region 赋值植入体信息
                    List<long> implantsIds = new List<long>();
                    if (allJumpClones.NotNullOrEmpty())
                    {
                        allJumpClones.ForEach(p => implantsIds.AddRange(p.Clone.Implants));
                    }
                    if (implantsIds.Any())
                    {
                        var implantsTypes = await Core.Services.DB.InvTypeService.QueryTypesAsync(implantsIds.Distinct().ToList());
                        if (implantsTypes.NotNullOrEmpty())
                        {
                            var implantsTypesDic = implantsTypes.ToDictionary(p => (long)p.TypeID);
                            foreach (var clone in allJumpClones)
                            {
                                if (clone.Clone.Implants.NotNullOrEmpty())
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
                                                TypeID = (int)implant,
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
                    if (allJumpClones.Any())
                    {
                        var stations = allJumpClones.Where(p => p.Clone.LocationType == "station").ToList();
                        var structures = allJumpClones.Where(p => p.Clone.LocationType == "structure").ToList();
                        if (stations.NotNullOrEmpty())
                        {
                            var staStations = await Core.Services.DB.StaStationService.QueryAsync(stations.Select(p => (int)p.Clone.LocationId).ToList());
                            if (staStations.NotNullOrEmpty())
                            {
                                foreach (var station in stations)
                                {
                                    var foundStation = staStations.FirstOrDefault(p => p.StationID == station.Clone.LocationId);
                                    if (foundStation != null)
                                    {
                                        station.LocationName = foundStation.StationName;
                                    }
                                }
                            }
                        }
                        if (structures.NotNullOrEmpty())
                        {
                            foreach (var structure in structures)
                            {
                                var structureRsp = await _esiClient.Universe.GetStructureInfoAsync(_auth, structure.Clone.LocationId);
                                if (structureRsp?.Model != null)
                                {
                                    structure.LocationName = structureRsp.Model.Name;
                                }
                                else
                                {
                                    structure.LocationName = structure.Clone.LocationId.ToString();
                                    this.ShowError($"GetStructureInfoAsync Failed:{structure.Clone.LocationId}", true);
                                }
                            }
                        }
                    }
                    #endregion

                    ListView_Clones.ItemsSource = allJumpClones;
                }
                else
                {
                    this.ShowError("GetActiveImplantsAsync Failed", true);
                }
            }
            else
            {
                this.ShowError("GetClonesAsync Failed", true);
            }
        }
    }
}
