using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.Core.DBModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.Core.Extensions;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.EVEHelpers;
using TheGuideToTheNewEden.Core.Models.Map;
using static TheGuideToTheNewEden.WinUI.Controls.MapDataTypeControl;
using Vanara.PInvoke;
using SqlSugar.DistributedSystem.Snowflake;

namespace TheGuideToTheNewEden.WinUI.Views.Map.Tools
{
    public sealed partial class MapNavigation : Page
    {
        private readonly ObservableCollection<MapSolarSystem> _waypoints = new ObservableCollection<MapSolarSystem>();
        private readonly ObservableCollection<MapSolarSystem> _avoidSystems = new ObservableCollection<MapSolarSystem>();
        private readonly ObservableCollection<MapRegion> _avoidRegions = new ObservableCollection<MapRegion>();
        private Dictionary<int, ESI.NET.Models.Universe.Kills> _systemKills;
        private Dictionary<int, int> _systemJumps;
        private Dictionary<int, SovData> _sovDatas;
        public MapNavigation()
        {
            this.InitializeComponent();
            WaypointsListView.ItemsSource = _waypoints;
            AvoidSystemListView.ItemsSource = _avoidSystems;
            AvoidRegionListView.ItemsSource = _avoidRegions;
            Loaded += MapNavigation_Loaded;
        }
        public void SetData(Dictionary<int, ESI.NET.Models.Universe.Kills> kills, Dictionary<int, int> jumps, Dictionary<int, SovData> sovDatas)
        {
            _systemKills = kills;
            _systemJumps = jumps;
            _sovDatas = sovDatas;
        }
        private void MapNavigation_Loaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= MapNavigation_Loaded;
            InitCapitalJumpShipInfos();
            NavTypeComboBox.SelectionChanged += NavTypeComboBox_SelectionChanged;
        }

        private void MapSystemSelector_OnSelectedItemChanged(Core.DBModels.MapSolarSystem selectedItem)
        {
            if(selectedItem != null)
                _waypoints.Add(selectedItem);
        }

        private void RemoveWaypointButton_Click(object sender, RoutedEventArgs e)
        {
            _waypoints.Remove((sender as Button).DataContext as MapSolarSystem);
        }

        private void AvoidSystemSelector_OnSelectedItemChanged(MapSolarSystem selectedItem)
        {
            if (selectedItem != null)
            {
                _avoidSystems.Add(selectedItem);
                AvoidSystemButton.Content = _avoidSystems.Count.ToString();
            }
        }

        private void RemoveAvoidSystemButton_Click(object sender, RoutedEventArgs e)
        {
            _avoidSystems.Remove((sender as Button).DataContext as MapSolarSystem);
            AvoidSystemButton.Content = _avoidSystems.Count.ToString();
        }

        private void AvoidRegionSelector_OnSelectedItemChanged(MapRegion selectedItem)
        {
            if (selectedItem != null)
            {
                _avoidRegions.Add(selectedItem);
                AvoidRegionButton.Content = _avoidRegions.Count.ToString();
            }
        }
        private void RemoveAvoidRegionButton_Click(object sender, RoutedEventArgs e)
        {
            _avoidRegions.Remove((sender as Button).DataContext as MapRegion);
            AvoidRegionButton.Content = _avoidRegions.Count.ToString();
        }

        //private async void GetCanCapitalJumpShips()
        //{
        //    IEnumerable<InvType> ships = null;
        //    await Task.Run(() =>
        //    {
        //        //分类在旗舰1381和黑隐1075下的所有分类下的船
        //        //循环找分组的子分组，直到没有子分组
        //        List<InvMarketGroup> findLastSubGroup(InvMarketGroup inputGroup)
        //        {
        //            var subGroup = Core.Services.DB.InvMarketGroupService.QuerySubGroupId(inputGroup.MarketGroupID);
        //            if (subGroup.NotNullOrEmpty())//还有子分组
        //            {
        //                List<InvMarketGroup> subGroups = new List<InvMarketGroup>();
        //                foreach (var group in subGroup)
        //                {
        //                    subGroups.AddRange(findLastSubGroup(group));
        //                }
        //                return subGroups;
        //            }
        //            else
        //            {
        //                return new List<InvMarketGroup>() { inputGroup };
        //            }
        //        }
        //        var capGroup = findLastSubGroup(Core.Services.DB.InvMarketGroupService.Query(1381));
        //        var capShips = Core.Services.DB.InvTypeService.QueryTypesInGroup(capGroup.Select(p=>p.MarketGroupID).ToList());
        //        var blackOpsGroup = findLastSubGroup(Core.Services.DB.InvMarketGroupService.Query(1075));
        //        var blackOpsShips = Core.Services.DB.InvTypeService.QueryTypesInGroup(blackOpsGroup.Select(p => p.MarketGroupID).ToList());
        //        ships = capShips.Union(blackOpsShips).ToList();//所有支持旗舰跳的船
        //    });
        //    ShipTypeComboBox.ItemsSource = ships;
        //}

        private void InitCapitalJumpShipInfos()
        {
            var json = System.IO.File.ReadAllText(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "Configs", "CapitalJumpShipInfo.json"));
            if (json != null)
            {
                var infos = JsonConvert.DeserializeObject<List<CapitalJumpShipInfo>>(json);
                foreach(var info in infos)
                {
                    info.InvMarketGroup = Core.Services.DB.InvMarketGroupService.Query(info.GroupID);
                    info.InvType = Core.Services.DB.InvTypeService.QueryType(info.TypeID);
                }
                ShipTypeComboBox.ItemsSource = infos;
            }
        }

        private async void StartNavigateButton_Click(object sender, RoutedEventArgs e)
        {
            MainPivot.SelectedIndex = 1;
            WaitingResultGrid.Visibility = Visibility.Visible;
            WaitingResultRing.IsActive = true;
            HasResultGrid.Visibility = Visibility.Collapsed;
            NoResultGrid.Visibility = Visibility.Collapsed;
            //TODO:计算避开星系
            List<int> avoid = new List<int>();
            CapitalJumpShipInfo ship = ShipTypeComboBox.SelectedItem as CapitalJumpShipInfo;
            double maxLY = ship.MaxLY * Math.Pow(1.2, (int)JumpDriveCalibrationNumberBox.Value);
            double perLyFuel = ship.PerLYFuel * Math.Pow(1.1, (int)JumpFuelConservationNumberBox.Value);//未算上战略货舰技能加成
            if(ship.GroupID == 1089)//战略货舰
            {
                perLyFuel *= Math.Pow(1.1, (int)JumpFreightersNumberBox.Value);
            }
            var path = await CalCapitalJumpPath(_waypoints.Select(p => p.SolarSystemID).ToList(), maxLY, UseStargateCheckBox.IsChecked == true, avoid, perLyFuel);
            WaitingResultGrid.Visibility = Visibility.Collapsed;
            WaitingResultRing.IsActive = false;
            if (path != null)
            {
                HasResultGrid.Visibility = Visibility.Visible;
                PassSystemCountTextBlock.Text = (path.Count + 1).ToString();
                ResultList.ItemsSource = path;
                var startSys = _waypoints.First();
                var endSys = _waypoints.Last();
                var m = Math.Sqrt(Math.Pow(startSys.X - endSys.X, 2) + Math.Pow(startSys.Y - endSys.Y, 2) + Math.Pow(startSys.Z - endSys.Z, 2));
                LinearDistanceTextBlock.Text = (m / 9460730472580800).ToString("N2");
                PassStargateCountTextBlock.Text = path.Where(p => p.NavType == 1).Count().ToString();
                CapitalJumpCountTextBlock.Text = path.Where(p => p.NavType == 2).Count().ToString();
            }
            else
            {
                NoResultGrid.Visibility = Visibility.Visible;
            }
        }

        private void NavTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CapitalJumpGrid.Visibility = NavTypeComboBox.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task<List<MapNavigationPoint>> CalCapitalJumpPath(List<int> path, double maxLY, bool useStargate, List<int> avoidSys, double perLyFuel)
        {
            return await Task.Run(() =>
            {
                List<int> resultPath = new List<int>();
                for (int i = 0; i < path.Count; i += 2)
                {
                    var pathIds = ShortestPathHelper.CalCapitalJumpPath(path[i], path[i + 1], maxLY, useStargate, avoidSys);
                    if (pathIds != null)
                    {
                        resultPath.AddRange(pathIds);
                    }
                }
                if (resultPath.Any())
                {
                    return GetMapNavigationPoints(resultPath, perLyFuel);
                }
                else
                {
                    return null;
                }
            });
        }
        private List<MapNavigationPoint> GetMapNavigationPoints(List<int> path, double perLyFuel)
        {
            if (path.NotNullOrEmpty())
            {
                try
                {
                    List<MapNavigationPoint> mapNavigationPoints = new List<MapNavigationPoint>();
                    var systemDic = Core.Services.DB.MapSolarSystemService.Query(path).ToDictionary(p => p.SolarSystemID);
                    var regionDic = Core.Services.DB.MapRegionService.Query(systemDic.Values.Select(p => p.RegionID).Distinct().ToList()).ToDictionary(p => p.RegionID);
                    MapNavigationPoint createPoint(int sysId)
                    {
                        MapNavigationPoint mapNavigationPoint = new MapNavigationPoint()
                        {
                            Id = mapNavigationPoints.Count
                        };
                        if (systemDic.TryGetValue(sysId, out var system))
                        {
                            mapNavigationPoint.System = system;
                        }
                        if (mapNavigationPoint.System != null && regionDic.TryGetValue(mapNavigationPoint.System.RegionID, out var region))
                        {
                            mapNavigationPoint.Region = region;
                        }
                        if (_systemKills.TryGetValue(sysId, out var sysKill))
                        {
                            mapNavigationPoint.PodKills = sysKill.PodKills;
                            mapNavigationPoint.ShipKills = sysKill.ShipKills;
                        }
                        if (_systemJumps.TryGetValue(sysId, out var sysJump))
                        {
                            mapNavigationPoint.Jumps = sysJump;
                        }
                        if (_sovDatas.TryGetValue(sysId, out var sov))
                        {
                            mapNavigationPoint.Sov = sov.AllianceName;
                        }
                        return mapNavigationPoint;
                    }
                    var firstP = createPoint(path.Last());
                    mapNavigationPoints.Add(firstP);
                    for(int i = path.Count - 2;i >= 0;i--)
                    {
                        var point = createPoint(path[i]);
                        var prevP = mapNavigationPoints.Last();
                        var m = Math.Sqrt(Math.Pow(point.System.X - prevP.System.X, 2) + Math.Pow(point.System.Y - prevP.System.Y, 2) + Math.Pow(point.System.Z - prevP.System.Z, 2));
                        point.Distance = m / 9460730472580800;
                        point.Fuel = perLyFuel * point.Distance;
                        var prevPJumpTo = SolarSystemPosHelper.GetJumpTo(prevP.System.SolarSystemID);
                        if (prevPJumpTo != null && prevPJumpTo.Contains(path[i]))
                        {
                            point.NavType = 1;
                        }
                        else
                        {
                            point.NavType = 2;
                        }
                        mapNavigationPoints.Add(point);
                    }
                    return mapNavigationPoints;
                }
                catch(Exception ex)
                {
                    Core.Log.Error(ex);
                    return null;
                }
            }
            else
            {
                return null;
            }
        }
    }
}
