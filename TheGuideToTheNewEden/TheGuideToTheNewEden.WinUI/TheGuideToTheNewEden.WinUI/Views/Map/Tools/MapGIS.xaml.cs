using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using static TheGuideToTheNewEden.WinUI.Controls.MapDataTypeControl;
using TheGuideToTheNewEden.Core.Models.Map;
using TheGuideToTheNewEden.Core.DBModels;
using Vanara.PInvoke;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Views.Map.Tools
{
    public sealed partial class MapGIS : Page
    {
        private Dictionary<int, ESI.NET.Models.Universe.Kills> _systemKills;
        private Dictionary<int, int> _systemJumps;
        private Dictionary<int, SovData> _sovDatas;
        private MapCanvas _mapCanvas;
        public MapGIS()
        {
            this.InitializeComponent();
            Loaded += MapGIS_Loaded;
        }

        private void MapGIS_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MapGIS_Loaded;
        }

        public async void SetData(List<MapSolarSystem> systems, List<MapRegion> regions, MapCanvas mapCanvas, Dictionary<int, ESI.NET.Models.Universe.Kills> kills, Dictionary<int, int> jumps, Dictionary<int, SovData> sovDatas)
        {
            _mapCanvas = mapCanvas;
            _systemKills = kills;
            _systemJumps = jumps;
            _sovDatas = sovDatas;
            ResultList.ItemsSource = await Task.Run(() =>
            {
                List<MapSystemInfo> mapSystemInfos = new List<MapSystemInfo>();
                var regionDic = regions.ToDictionary(p => p.RegionID);
                foreach (var system in systems)
                {
                    int shipKills = 0;
                    int podKills = 0;
                    int jump = 0;
                    MapRegion mapRegion = null;
                    SovData sovData = null;
                    if (_systemKills.TryGetValue(system.SolarSystemID, out var kill))
                    {
                        shipKills = kill.ShipKills;
                        podKills = kill.PodKills;
                    }
                    _systemJumps.TryGetValue(system.SolarSystemID, out jump);
                    regionDic.TryGetValue(system.RegionID, out mapRegion);
                    sovDatas.TryGetValue(system.SolarSystemID, out sovData);
                    if (shipKills + podKills + jump != 0)
                    {
                        mapSystemInfos.Add(new MapSystemInfo()
                        {
                            Region = mapRegion,
                            System = system,
                            ShipKills = shipKills,
                            PodKills = podKills,
                            Jumps = jump,
                            Sov = sovData?.AllianceName,
                        });
                    }
                }
                return mapSystemInfos.OrderBy(p => p.ShipKills).ToList();
            });
        }

        private void ResultList_CellDoubleTapped(object sender, Syncfusion.UI.Xaml.DataGrid.GridCellDoubleTappedEventArgs e)
        {
            var point = ResultList.SelectedItem as MapSystemInfo;
            if (point != null)
            {
                _mapCanvas.ToSystem(point.System.SolarSystemID);
            }
        }
    }
}
