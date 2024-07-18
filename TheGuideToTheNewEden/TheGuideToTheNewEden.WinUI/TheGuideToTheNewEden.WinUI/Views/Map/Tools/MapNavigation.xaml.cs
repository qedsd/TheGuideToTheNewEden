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
using ESI.NET.Models.Location;
using Vanara.Extensions.Reflection;
using static TheGuideToTheNewEden.WinUI.Views.Map.MapCanvas;

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
        private MapCanvas _mapCanvas;
        public MapNavigation()
        {
            this.InitializeComponent();
            WaypointsListView.ItemsSource = _waypoints;
            AvoidSystemListView.ItemsSource = _avoidSystems;
            AvoidRegionListView.ItemsSource = _avoidRegions;
            Loaded += MapNavigation_Loaded;
        }
        public void SetData(MapCanvas mapCanvas,Dictionary<int, ESI.NET.Models.Universe.Kills> kills, Dictionary<int, int> jumps, Dictionary<int, SovData> sovDatas)
        {
            _mapCanvas = mapCanvas;
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

        private void InitCapitalJumpShipInfos()
        {
            ShipTypeComboBox.ItemsSource = Core.EVEHelpers.CapitalJumpShipInfoHelper.GetInfos();
        }

        private async void StartNavigateButton_Click(object sender, RoutedEventArgs e)
        {
            _mapCanvas.RemoveTemporary();
            _mapCanvas.ClearMapGraph();
            MainPivot.SelectedIndex = 1;
            WaitingResultGrid.Visibility = Visibility.Visible;
            WaitingResultRing.IsActive = true;
            HasResultGrid.Visibility = Visibility.Collapsed;
            NoResultGrid.Visibility = Visibility.Collapsed;
            List<int> avoid = await GetAvoidSystems();
            List<MapNavigationPoint> path = null;
            if(NavTypeComboBox.SelectedIndex == 1)//旗舰跳
            {
                CapitalJumpShipInfo ship = ShipTypeComboBox.SelectedItem as CapitalJumpShipInfo;
                double maxLY = GetShipMaxJump(ship);
                double perLyFuel = GetShipPerLyFuel(ship);
                path = await CalCapitalJumpPath(_waypoints.Select(p => p.SolarSystemID).ToList(), maxLY, UseStargateCheckBox.IsChecked == true, avoid, perLyFuel, PreferPathComboBox.SelectedIndex);
            }
            else//走星门
            {
                path = await CalStargatePath(_waypoints.Select(p => p.SolarSystemID).ToList(), avoid);
            }
            
            WaitingResultGrid.Visibility = Visibility.Collapsed;
            WaitingResultRing.IsActive = false;
            if (path != null)
            {
                HasResultGrid.Visibility = Visibility.Visible;
                PassSystemCountTextBlock.Text = path.Count.ToString();
                ResultList.ItemsSource = path;
                var startSys = _waypoints.First();
                var endSys = _waypoints.Last();
                var m = Math.Sqrt(Math.Pow(startSys.X - endSys.X, 2) + Math.Pow(startSys.Y - endSys.Y, 2) + Math.Pow(startSys.Z - endSys.Z, 2));
                LinearDistanceTextBlock.Text = (m / 9460730472580800).ToString("N2");
                PassStargateCountTextBlock.Text = path.Where(p => p.NavType == 1).Count().ToString();
                CapitalJumpCountTextBlock.Text = path.Where(p => p.NavType == 2).Count().ToString();
                RequireFuelTextBlock.Text = path.Sum(p => p.Fuel).ToString("N2");
                CapitalJumpDistanceTextBlock.Text = path.Where(p=>p.NavType == 2).Sum(p => p.Distance).ToString("N2");

                if(MapOnlyShowPathCheckBox.IsChecked == true)
                {
                    _mapCanvas.TemporaryEnableData(path.Select(p => p.System.SolarSystemID).ToList());
                }
                List<MapGraphBase> mapGraphs = new List<MapGraphBase>();
                for(int i = 0; i< path.Count;i++)
                {
                    mapGraphs.Add(new CircleMapGraph()
                    {
                        CenterDataId = path[i].System.SolarSystemID,
                    });
                }
                for (int i = 0; i < path.Count - 1;)
                {
                    mapGraphs.Add(new LineMapGraph()
                    {
                        Data1Id = path[i].System.SolarSystemID,
                        Data2Id = path[++i].System.SolarSystemID,
                    });
                }
                _mapCanvas.AddMapGraph(mapGraphs);
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
        private async Task<List<int>> GetAvoidSystems()
        {
            return await Task.Run(() =>
            {
                List<int> avoid = _avoidSystems.Select(p => p.SolarSystemID).ToList();
                foreach (var region in _avoidRegions)
                {
                    var systems = Core.Services.DB.MapSolarSystemService.QueryByRegionID(region.RegionID);
                    avoid.AddRange(systems.Select(p => p.SolarSystemID));
                }
                return avoid;
            });
        }
        private async Task<List<MapNavigationPoint>> CalCapitalJumpPath(List<int> path, double maxLY, bool useStargate, List<int> avoidSys, double perLyFuel,int mode)
        {
            return await Task.Run(() =>
            {
                List<int> resultPath = new List<int>();
                for (int i = 0; i < path.Count - 1; i ++)
                {
                    //ShortestPathHelper返回的数据从终点到起点
                    //需要将数据反转
                    var pathIds = ShortestPathHelper.CalCapitalJumpPath(path[i], path[i + 1], maxLY, useStargate, avoidSys, mode);
                    if (pathIds != null)
                    {
                        for(int j = pathIds.Count - 1; j >= 1; j--)
                        {
                            resultPath.Add(pathIds[j]);
                        }
                        if(i == path.Count - 2)//最后一个航点需要添加回最后航点
                        {
                            resultPath.Add(pathIds[0]);
                        }
                    }
                }
                if (resultPath.Any())
                {
                    return GetMapNavigationPoints(resultPath, perLyFuel,useStargate);
                }
                else
                {
                    return null;
                }
            });
        }
        private async Task<List<MapNavigationPoint>> CalStargatePath(List<int> path, List<int> avoidSys)
        {
            return await Task.Run(() =>
            {
                List<int> resultPath = new List<int>();
                for (int i = 0; i < path.Count - 1; i++)
                {
                    //ShortestPathHelper返回的数据从终点到起点
                    //需要将数据反转
                    var pathIds = ShortestPathHelper.CalStargatePath(path[i], path[i + 1], avoidSys);
                    if (pathIds != null)
                    {
                        for (int j = pathIds.Count - 1; j >= 1; j--)
                        {
                            resultPath.Add(pathIds[j]);
                        }
                        if (i == path.Count - 2)//最后一个航点需要添加回最后航点
                        {
                            resultPath.Add(pathIds[0]);
                        }
                    }
                }
                if (resultPath.Any())
                {
                    return GetMapNavigationPoints(resultPath, 0, true);
                }
                else
                {
                    return null;
                }
            });
        }
        private List<MapNavigationPoint> GetMapNavigationPoints(List<int> path, double perLyFuel, bool useStargate)
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
                    var firstP = createPoint(path[0]);
                    mapNavigationPoints.Add(firstP);
                    for(int i = 1;i <= path.Count - 1;i++)
                    {
                        var point = createPoint(path[i]);
                        var prevP = mapNavigationPoints.Last();
                        var m = Math.Sqrt(Math.Pow(point.System.X - prevP.System.X, 2) + Math.Pow(point.System.Y - prevP.System.Y, 2) + Math.Pow(point.System.Z - prevP.System.Z, 2));
                        point.Distance = m / 9460730472580800;
                        if(!useStargate)
                        {
                            point.NavType = 2;
                            point.Fuel = perLyFuel * point.Distance;
                        }
                        else
                        {
                            var prevPJumpTo = SolarSystemPosHelper.GetJumpTo(prevP.System.SolarSystemID);
                            if (prevPJumpTo != null && prevPJumpTo.Contains(path[i]))
                            {
                                point.NavType = 1;
                            }
                            else
                            {
                                point.NavType = 2;
                                point.Fuel = perLyFuel * point.Distance;
                            }
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

        private void ShipTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CapitalJumpShipInfo ship = ShipTypeComboBox.SelectedItem as CapitalJumpShipInfo;
            if (ship?.GroupID == 1089)
            {
                JumpFreightersGrid.Visibility = Visibility.Visible;
            }
            else
            {
                JumpFreightersGrid.Visibility = Visibility.Collapsed;
            }
            UpdateShipMaxJumpAndFuel();
        }
        private void UpdateShipMaxJumpAndFuel()
        {
            CapitalJumpShipInfo ship = ShipTypeComboBox.SelectedItem as CapitalJumpShipInfo;
            if(ship != null)
            {
                MaxJumpTextBlock.Text = GetShipMaxJump(ship).ToString("N2");
                PerLyFuelTextBlock.Text = GetShipPerLyFuel(ship).ToString("N2");
            }
        }
        private double GetShipMaxJump(CapitalJumpShipInfo ship)
        {
            return ship.MaxLY + ship.MaxLY * 0.2 * (int)JumpDriveCalibrationNumberBox.Value;
        }
        private double GetShipPerLyFuel(CapitalJumpShipInfo ship)
        {
            double basePerLyFuel = ship.PerLYFuel;
            if (ship.GroupID == 1089)//战略货舰
            {
                basePerLyFuel = ship.PerLYFuel - ship.PerLYFuel * 0.1 * (int)JumpFreightersNumberBox.Value;
            }
            double perLyFuel = basePerLyFuel - basePerLyFuel * 0.1 * (int)JumpFuelConservationNumberBox.Value;//未算上战略货舰技能加成
            return perLyFuel;
        }

        private void JumpDriveCalibrationNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            UpdateShipMaxJumpAndFuel();
        }

        private void JumpFuelConservationNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            UpdateShipMaxJumpAndFuel();
        }

        private void JumpFreightersNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            UpdateShipMaxJumpAndFuel();
        }

        private void MaxContentButton_Click(object sender, RoutedEventArgs e)
        {
            this.MaxWidth = double.MaxValue;
            MaxContentButton.Visibility = Visibility.Collapsed;
            MinContentButton.Visibility = Visibility.Visible;
        }

        private void MinContentButton_Click(object sender, RoutedEventArgs e)
        {
            this.MaxWidth = 656;
            MaxContentButton.Visibility = Visibility.Visible;
            MinContentButton.Visibility = Visibility.Collapsed;
        }

        private void ResultList_CellDoubleTapped(object sender, Syncfusion.UI.Xaml.DataGrid.GridCellDoubleTappedEventArgs e)
        {
            var point = ResultList.SelectedItem as MapNavigationPoint;
            if(point != null)
            {
                _mapCanvas.ToSystem(point.System.SolarSystemID);
            }
        }
    }
}
