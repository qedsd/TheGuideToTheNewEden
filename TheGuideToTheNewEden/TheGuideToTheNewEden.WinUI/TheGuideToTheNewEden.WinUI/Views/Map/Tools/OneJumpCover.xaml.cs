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
using TheGuideToTheNewEden.Core.EVEHelpers;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.Map;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static TheGuideToTheNewEden.WinUI.Controls.MapDataTypeControl;
using TheGuideToTheNewEden.Core.Extensions;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using Microsoft.UI.Xaml.Shapes;
using static TheGuideToTheNewEden.WinUI.Views.Map.MapCanvas;

namespace TheGuideToTheNewEden.WinUI.Views.Map.Tools
{
    public sealed partial class OneJumpCover : Page
    {
        private Dictionary<int, ESI.NET.Models.Universe.Kills> _systemKills;
        private Dictionary<int, int> _systemJumps;
        private Dictionary<int, SovData> _sovDatas;
        private MapCanvas _mapCanvas;
        public OneJumpCover()
        {
            this.InitializeComponent();
            Loaded += OneJumpCover_Loaded;
        }
        public void SetData(MapCanvas mapCanvas, Dictionary<int, ESI.NET.Models.Universe.Kills> kills, Dictionary<int, int> jumps, Dictionary<int, SovData> sovDatas)
        {
            _mapCanvas = mapCanvas;
            _systemKills = kills;
            _systemJumps = jumps;
            _sovDatas = sovDatas;
        }

        private void OneJumpCover_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OneJumpCover_Loaded;
            ShipTypeComboBox.ItemsSource = Core.EVEHelpers.CapitalJumpShipInfoHelper.GetInfos();
        }

        #region 燃料距离计算
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
        private void UpdateShipMaxJumpAndFuel()
        {
            CapitalJumpShipInfo ship = ShipTypeComboBox.SelectedItem as CapitalJumpShipInfo;
            if (ship != null)
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
        #endregion
        private void ResultList_CellDoubleTapped(object sender, Syncfusion.UI.Xaml.DataGrid.GridCellDoubleTappedEventArgs e)
        {
            var point = ResultList.SelectedItem as MapNavigationPoint;
            if (point != null)
            {
                _mapCanvas.ToSystem(point.System.SolarSystemID);
            }
        }
        private List<MapNavigationPoint> GetMapNavigationPoints(MapSolarSystem centerSystem, List<MapSolarSystem> path, double perLyFuel)
        {
            if (path.NotNullOrEmpty())
            {
                try
                {
                    List<MapNavigationPoint> mapNavigationPoints = new List<MapNavigationPoint>();
                    var systemDic = path.ToDictionary(p => p.SolarSystemID);
                    var regionDic = Core.Services.DB.MapRegionService.Query(systemDic.Values.Select(p => p.RegionID).Distinct().ToList()).ToDictionary(p => p.RegionID);
                    MapNavigationPoint createPoint(int sysId)
                    {
                        MapNavigationPoint mapNavigationPoint = new MapNavigationPoint();
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
                    foreach(var system in path)
                    {
                        var point = createPoint(system.SolarSystemID);
                        var m = Math.Sqrt(Math.Pow(point.System.X - centerSystem.X, 2) + Math.Pow(point.System.Y - centerSystem.Y, 2) + Math.Pow(point.System.Z - centerSystem.Z, 2));
                        point.Distance = m / 9460730472580800;
                        point.NavType = 2;
                        point.Fuel = perLyFuel * point.Distance;
                        mapNavigationPoints.Add(point);
                    }
                    int id = 0;
                    mapNavigationPoints = mapNavigationPoints.OrderBy(p => p.Distance).ToList();
                    mapNavigationPoints.ForEach(p => { p.Id = ++id; });
                    return mapNavigationPoints;
                }
                catch (Exception ex)
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

        private void MapSystemSelector_OnSelectedItemChanged(Core.DBModels.MapSolarSystem selectedItem)
        {
            StartButton.IsEnabled = selectedItem != null;
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            ProgressBar.IsIndeterminate = true;
            ProgressBar.Visibility = Visibility.Visible;
            _mapCanvas.RemoveTemporary();
            _mapCanvas.ClearMapGraph();
            try
            {
                CapitalJumpShipInfo ship = ShipTypeComboBox.SelectedItem as CapitalJumpShipInfo;
                double maxLY = GetShipMaxJump(ship);
                double perLyFuel = GetShipPerLyFuel(ship);
                var system = MapSystemSelector.SelectedItem.SolarSystemID;
                var list = await Task.Run(() => ShortestPathHelper.CalOneJumpCover(system, maxLY));
                if (list != null)
                {
                    ResultList.ItemsSource = GetMapNavigationPoints(MapSystemSelector.SelectedItem, list, perLyFuel);
                }
                _mapCanvas.TemporaryEnableData(list.Select(p => p.SolarSystemID).ToList());
                List<MapGraphBase> mapGraphs = new List<MapGraphBase>();
                for (int i = 0; i < list.Count; i++)
                {
                    mapGraphs.Add(new CircleMapGraph()
                    {
                        CenterDataId = list[i].SolarSystemID,
                    });
                }
                _mapCanvas.AddMapGraph(mapGraphs);
                _mapCanvas.ToSystem(system);
            }
            catch
            {
                throw;
            }
            finally
            {
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private void MaxContentButton_Click(object sender, RoutedEventArgs e)
        {
            this.MaxWidth = double.MaxValue;
            MaxContentButton.Visibility = Visibility.Collapsed;
            MinContentButton.Visibility = Visibility.Visible;
        }

        private void MinContentButton_Click(object sender, RoutedEventArgs e)
        {
            this.MaxWidth = 560;
            MaxContentButton.Visibility = Visibility.Visible;
            MinContentButton.Visibility = Visibility.Collapsed;
        }
    }
}
