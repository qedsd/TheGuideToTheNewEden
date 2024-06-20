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
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class SystemFilterControl : UserControl
    {
        private List<Core.DBModels.MapSolarSystem> _mapSolarSystems;
        private List<Core.DBModels.MapRegion> _mapRegions;
        private List<ToggleButton> _regionToggleButton = new List<ToggleButton>();
        private HashSet<int> _selectedRegions = new HashSet<int>();
        private HashSet<int> _selectedSystems = new HashSet<int>();
        public SystemFilterControl()
        {
            this.InitializeComponent();
            Init();
        }
        private async void Init()
        {
            _mapSolarSystems = await Core.Services.DB.MapSolarSystemService.QueryAllAsync();
            _mapRegions = await Core.Services.DB.MapRegionService.QueryAllAsync();
            GridView_Region.ItemsSource = _mapRegions;
            TextBlock_AllRegionCount.Text = _mapRegions.Count.ToString();
            TextBlock_SelectedRegionCount.Text = _mapRegions.Count.ToString();
            foreach (var region in _mapRegions)
            {
                _selectedRegions.Add(region.RegionID);
            }
            ListView_Systems.ItemsSource = new ObservableCollection<Core.DBModels.MapSolarSystem>();
        }

        private void Button_SelecteAllRegion_Click(object sender, RoutedEventArgs e)
        {
            bool b = (sender as ToggleButton).IsChecked == true;
            foreach (var button in _regionToggleButton)
            {
                button.IsChecked = b;
            }
            if(b)
            {
                foreach(var region in _mapRegions)
                {
                    _selectedRegions.Add(region.RegionID);
                }
            }
            else
            {
                _selectedRegions.Clear();
            }
            TextBlock_SelectedRegionCount.Text = _selectedRegions.Count.ToString();
        }

        private void Button_Region_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as ToggleButton;
            var region = button.DataContext as Core.DBModels.MapRegion;
            if (button.IsChecked == true)
            {
                _selectedRegions.Add(region.RegionID);
            }
            else
            {
                _selectedRegions.Remove(region.RegionID);
            }
            TextBlock_SelectedRegionCount.Text = _selectedRegions.Count.ToString();
        }

        private void ToggleButton_Region_Loaded(object sender, RoutedEventArgs e)
        {
            _regionToggleButton.Add(sender as ToggleButton);
        }

        private void MapSystemSelector_OnSelectedItemChanged(Core.DBModels.MapSolarSystem selectedItem)
        {
            if(selectedItem != null)
            {
                _selectedSystems.Add(selectedItem.SolarSystemID);
                (ListView_Systems.ItemsSource as ObservableCollection<Core.DBModels.MapSolarSystem>).Add(selectedItem);
            }
        }

        private void Button_SystemInList_Remove_Click(object sender, RoutedEventArgs e)
        {
            var system = (sender as FrameworkElement).DataContext as Core.DBModels.MapSolarSystem;
            _selectedSystems.Remove(system.SolarSystemID);
            (ListView_Systems.ItemsSource as ObservableCollection<Core.DBModels.MapSolarSystem>).Remove(system);
        }
    }
}
