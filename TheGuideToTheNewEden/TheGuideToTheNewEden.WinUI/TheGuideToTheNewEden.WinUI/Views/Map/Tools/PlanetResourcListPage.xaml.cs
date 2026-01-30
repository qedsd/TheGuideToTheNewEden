using System;
using System.Collections.Generic;
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
using TheGuideToTheNewEden.WinUI.Models.Map;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views.Map.Tools
{
    public sealed partial class PlanetResourcListPage : Page
    {
        private Dictionary<int, Core.Models.PlanetResources.SolarSystemResources> _systemResourcesDic;
        private Dictionary<int, Core.Models.PlanetResources.RegionResources> _regionResourcesDic;
        private List<Core.Models.PlanetResources.Upgrade> _upgrades;
        private Dictionary<int, MapData> _systemDatas;
        public PlanetResourcListPage(Dictionary<int, Core.Models.PlanetResources.RegionResources> regionResourcesDic, Dictionary<int, Core.Models.PlanetResources.SolarSystemResources> systemResourcesDic, List<Core.Models.PlanetResources.Upgrade> upgrades,Dictionary<int, MapData> systemDatas)
        {
            _regionResourcesDic = regionResourcesDic;
            _systemResourcesDic = systemResourcesDic;
            _upgrades = upgrades;
            _systemDatas = systemDatas;
            this.InitializeComponent();
            RegionPlanetResourcList.ItemsSource = _regionResourcesDic.Values;
            UpgradeList.ItemsSource = _upgrades;
            Loaded += PlanetResourcListPage_Loaded;
        }

        private void PlanetResourcListPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded += PlanetResourcListPage_Loaded;
            UpdataSystemPlanetResourcList(0);
            Tool_SystemPlanetResourcList_Mode.SelectionChanged += Tool_SystemPlanetResourcList_Mode_SelectionChanged;
        }

        private void SelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            if (sender.SelectedItem != null)
            {
                RegionPlanetResourcList.Visibility = Visibility.Collapsed;
                SystemPlanetResourcListGrid.Visibility = Visibility.Collapsed;
                UpgradeList.Visibility = Visibility.Collapsed;
                switch (sender.SelectedItem.Tag.ToString())
                {
                    case "RegionPlanetResource": RegionPlanetResourcList.Visibility = Visibility.Visible; break;
                    case "SystemPlanetResource": SystemPlanetResourcListGrid.Visibility = Visibility.Visible; break;
                    case "Upgrade": UpgradeList.Visibility = Visibility.Visible; break;
                }
            }
        }
        private void Tool_SystemPlanetResourcList_Mode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_systemResourcesDic == null)
                return;
            UpdataSystemPlanetResourcList((sender as ComboBox).SelectedIndex);
        }
        private void UpdataSystemPlanetResourcList(int mode)
        {
            if (mode == 1)
            {
                List<Core.Models.PlanetResources.SolarSystemResources> solarSystemResources = new List<Core.Models.PlanetResources.SolarSystemResources>();
                foreach (var v in _systemResourcesDic)
                {
                    if (_systemDatas.TryGetValue(v.Key, out var data))
                    {
                        if (data.Enable)
                        {
                            solarSystemResources.Add(v.Value);
                        }
                    }
                }
                SystemPlanetResourcList.ItemsSource = solarSystemResources;
                Tool_SystemPlanetResourcList_Count.Text = solarSystemResources.Count.ToString();
            }
            else
            {
                SystemPlanetResourcList.ItemsSource = _systemResourcesDic.Values;
                Tool_SystemPlanetResourcList_Count.Text = _systemResourcesDic.Count.ToString();
            }
        }
    }
}
