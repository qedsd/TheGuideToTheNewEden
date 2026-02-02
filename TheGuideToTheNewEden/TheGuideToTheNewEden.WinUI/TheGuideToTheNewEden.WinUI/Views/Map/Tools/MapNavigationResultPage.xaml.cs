using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Map;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static TheGuideToTheNewEden.WinUI.Views.Map.MapCanvas;


namespace TheGuideToTheNewEden.WinUI.Views.Map.Tools
{
    public sealed partial class MapNavigationResultPage : Page
    {
        private MapCanvas _mapCanvas;
        private ObservableCollection<MapSolarSystem> _waypoints;
        private List<MapNavigationPoint> _path;
        public MapNavigationResultPage(MapCanvas mapCanvas, ObservableCollection<MapSolarSystem> waypoints, List<MapNavigationPoint> path)
        {
            _mapCanvas = mapCanvas;
            _waypoints = waypoints;
            _path = path;
            Loaded += MapNavigationResultPage_Loaded;
            this.InitializeComponent();
        }

        private void MapNavigationResultPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MapNavigationResultPage_Loaded;
            var startSys = _waypoints.First();
            var endSys = _waypoints.Last();
            var m = Math.Sqrt(Math.Pow(startSys.X - endSys.X, 2) + Math.Pow(startSys.Y - endSys.Y, 2) + Math.Pow(startSys.Z - endSys.Z, 2));
            PassSystemCountTextBlock.Text = _path.Count.ToString();
            LinearDistanceTextBlock.Text = (m / 9460730472580800).ToString("N2");
            PassStargateCountTextBlock.Text = _path.Where(p => p.NavType == 1).Count().ToString();
            CapitalJumpCountTextBlock.Text = _path.Where(p => p.NavType == 2).Count().ToString();
            RequireFuelTextBlock.Text = _path.Sum(p => p.Fuel).ToString("N2");
            CapitalJumpDistanceTextBlock.Text = _path.Where(p => p.NavType == 2).Sum(p => p.Distance).ToString("N2");
            ResultList.ItemsSource = _path;
        }
        private void ResultList_CellDoubleTapped(object sender, Syncfusion.UI.Xaml.DataGrid.GridCellDoubleTappedEventArgs e)
        {
            var point = ResultList.SelectedItem as MapNavigationPoint;
            if (point != null)
            {
                _mapCanvas.ToSystem(point.System.SolarSystemID);
            }
        }
        private List<MapGraphBase> _mapGraphs = new List<MapGraphBase>();
        public void ShowInMap()
        {
            RemoveFromMap();
            for (int i = 0; i < _path.Count; i++)
            {
                _mapGraphs.Add(new CircleMapGraph()
                {
                    CenterDataId = _path[i].System.SolarSystemID,
                });
            }
            for (int i = 0; i < _path.Count - 1;)
            {
                _mapGraphs.Add(new LineMapGraph()
                {
                    Data1Id = _path[i].System.SolarSystemID,
                    Data2Id = _path[++i].System.SolarSystemID,
                });
            }
            _mapCanvas.AddMapGraph(_mapGraphs);
        }
        public void RemoveFromMap()
        {
            if (_mapGraphs.Count > 0)
            {
                _mapCanvas.RemoveMapGraph(_mapGraphs);
                _mapGraphs.Clear();
            }
        }
        public void Close()
        {
            RemoveFromMap();
        }

        public List<MapNavigationPoint> GetResult()
        {
            return _path;
        }

        public event EventHandler<List<MapNavigationPoint>> OnShowInGameRequested;

        private void AddPathInGame_Click(object sender, RoutedEventArgs e)
        {
            int index = ResultList.SelectedIndex;
            if (index >= 0)
            {
                List<MapNavigationPoint> path = new List<MapNavigationPoint>()
                {
                    _path[index]
                };
                for (int i = index - 1; i >= 0; i--)
                {
                    if (_path[i].NavType == 1)//往回找通过星门导航的点
                    {
                        path.Add(_path[i]);
                    }
                    else
                    {
                        break;
                    }
                }
                path.Reverse();
                OnShowInGameRequested?.Invoke(this, path);
            }
        }

        private void SetDestinationInGame_Click(object sender, RoutedEventArgs e)
        {
            int index = ResultList.SelectedIndex;
            if (index >= 0)
            {
                List<MapNavigationPoint> path = new List<MapNavigationPoint>()
                {
                    _path[index]
                };
                OnShowInGameRequested?.Invoke(this, path);
            }
        }
    }
}
