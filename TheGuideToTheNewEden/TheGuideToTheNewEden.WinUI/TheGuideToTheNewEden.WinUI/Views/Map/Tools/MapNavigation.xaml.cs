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

namespace TheGuideToTheNewEden.WinUI.Views.Map.Tools
{
    public sealed partial class MapNavigation : Page
    {
        private readonly ObservableCollection<MapSolarSystem> _waypoints = new ObservableCollection<MapSolarSystem>();
        private readonly ObservableCollection<MapSolarSystem> _avoidSystems = new ObservableCollection<MapSolarSystem>();
        private readonly ObservableCollection<MapRegion> _avoidRegions = new ObservableCollection<MapRegion>();
        public MapNavigation()
        {
            this.InitializeComponent();
            WaypointsListView.ItemsSource = _waypoints;
            AvoidSystemListView.ItemsSource = _avoidSystems;
            AvoidRegionListView.ItemsSource = _avoidRegions;
            Loaded += MapNavigation_Loaded;
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
            ResultGrid.Visibility = Visibility.Visible;
            WaitingResultGrid.Visibility = Visibility.Visible;
            WaitingResultRing.IsActive = true;
            HasResultGrid.Visibility = Visibility.Collapsed;
            NoResultGrid.Visibility = Visibility.Collapsed;
            var path = await Task.Run(()=>ShortestPathHelper.CalCapitalJumpPath(_waypoints[0].SolarSystemID, _waypoints[1].SolarSystemID, (ShipTypeComboBox.SelectedItem as CapitalJumpShipInfo).MaxLY, UseStargateCheckBox.IsChecked == true, new List<int>()));
            WaitingResultGrid.Visibility = Visibility.Collapsed;
            WaitingResultRing.IsActive = false;
            if (path != null)
            {
                HasResultGrid.Visibility = Visibility.Visible;
                PassSystemCountTextBlock.Text = (path.Count + 1).ToString();
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
    }
}
