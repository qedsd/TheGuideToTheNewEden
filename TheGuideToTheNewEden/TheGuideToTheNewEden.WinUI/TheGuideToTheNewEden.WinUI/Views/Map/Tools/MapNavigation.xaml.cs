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

namespace TheGuideToTheNewEden.WinUI.Views.Map.Tools
{
    public sealed partial class MapNavigation : Page
    {
        private readonly ObservableCollection<MapSolarSystem> _waypoints = new ObservableCollection<MapSolarSystem>();
        public MapNavigation()
        {
            this.InitializeComponent();
            WaypointsListView.ItemsSource = _waypoints;
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
    }
}
