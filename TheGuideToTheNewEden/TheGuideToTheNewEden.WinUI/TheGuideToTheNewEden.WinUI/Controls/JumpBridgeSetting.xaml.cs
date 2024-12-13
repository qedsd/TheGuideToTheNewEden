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
using TheGuideToTheNewEden.Core.Extensions;
using System.Collections.ObjectModel;
using TheGuideToTheNewEden.WinUI.Helpers;
using Newtonsoft.Json;
using SqlSugar.DistributedSystem.Snowflake;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class JumpBridgeSetting : UserControl
    {
        private ObservableCollection<Tuple<Core.DBModels.MapSolarSystem, Core.DBModels.MapSolarSystem>> _bridges = new ObservableCollection<Tuple<Core.DBModels.MapSolarSystem, Core.DBModels.MapSolarSystem>>();
        public JumpBridgeSetting()
        {
            this.InitializeComponent();
            var bridges = Services.Settings.JumpBridgeSetting.GetValue();
            if (bridges.NotNullOrEmpty())
            {
                HashSet<int> ids = new HashSet<int>();
                foreach (var bridge in bridges)
                {
                    ids.Add(bridge.System1);
                    ids.Add(bridge.System2);
                }
                var systemsDic = Core.Services.DB.MapSolarSystemService.Query(ids.ToList()).ToDictionary(p => p.SolarSystemID);
                foreach (var bridge in bridges)
                {
                    Core.DBModels.MapSolarSystem system1;
                    Core.DBModels.MapSolarSystem system2;
                    if (!systemsDic.TryGetValue(bridge.System1, out system1))
                    {
                        system1 = new Core.DBModels.MapSolarSystem() { SolarSystemID = bridge.System1, SolarSystemName = bridge.System1.ToString() };
                    }
                    if (!systemsDic.TryGetValue(bridge.System2, out system2))
                    {
                        system2 = new Core.DBModels.MapSolarSystem() { SolarSystemID = bridge.System2, SolarSystemName = bridge.System2.ToString() };
                    }
                    _bridges.Add(new Tuple<Core.DBModels.MapSolarSystem, Core.DBModels.MapSolarSystem>(system1, system2));
                }
            }
            BridgeList.ItemsSource = _bridges;
            ShowToggleSwitch.IsOn = Services.Settings.JumpBridgeSetting.IsShowBridge();
            ShowToggleSwitch.Toggled += ShowToggleSwitch_Toggled;
        }
        

        private void Button_Remove_Click(object sender, RoutedEventArgs e)
        {
            var data = (sender as FrameworkElement).DataContext as Tuple<Core.DBModels.MapSolarSystem, Core.DBModels.MapSolarSystem>;
            if(data != null)
            {
                _bridges.Remove(data);
                Services.Settings.JumpBridgeSetting.Remove(data.Item1.SolarSystemID, data.Item2.SolarSystemID);
            }
        }

        private void Button_Add_Click(object sender, RoutedEventArgs e)
        {
            AddGrid.Visibility = Visibility.Collapsed;
            AddingGrid1.Visibility = Visibility.Visible;
            AddingGrid2.Visibility = Visibility.Visible;
        }


        private void MapSystemSelectorControl_OnSelectedItemChanged_1(Core.DBModels.MapSolarSystem selectedItem)
        {
            New1.Content = selectedItem?.SolarSystemName;
            New1.DataContext = selectedItem;
        }

        private void MapSystemSelectorControl_OnSelectedItemChanged_2(Core.DBModels.MapSolarSystem selectedItem)
        {
            New2.Content = selectedItem?.SolarSystemName;
            New2.DataContext = selectedItem;
        }

        private void Button_ConfirmAdd_Click(object sender, RoutedEventArgs e)
        {
            Core.DBModels.MapSolarSystem system1 = New1.DataContext as Core.DBModels.MapSolarSystem;
            Core.DBModels.MapSolarSystem system2 = New2.DataContext as Core.DBModels.MapSolarSystem;
            if(Services.Settings.JumpBridgeSetting.Add(system1.SolarSystemID, system2.SolarSystemID))
            {
                _bridges.Add(new Tuple<Core.DBModels.MapSolarSystem, Core.DBModels.MapSolarSystem>(system1, system2));
                ResetAdd();
            }
        }

        private void Button_CancelAdd_Click(object sender, RoutedEventArgs e)
        {
            ResetAdd();
        }
        private void ResetAdd()
        {
            New1.Content = Helpers.ResourcesHelper.GetString("JumpBridgeSetting_SelectSystem");
            New1.DataContext = null;
            New2.Content = Helpers.ResourcesHelper.GetString("JumpBridgeSetting_SelectSystem");
            New2.DataContext = null;
            AddGrid.Visibility = Visibility.Visible;
            AddingGrid1.Visibility = Visibility.Collapsed;
            AddingGrid2.Visibility = Visibility.Collapsed;
        }

        private void ShowToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            Services.Settings.JumpBridgeSetting.SetShowBridge(ShowToggleSwitch.IsOn);
        }

        private async void Button_Output_Click(object sender, RoutedEventArgs e)
        {
            var win = Helpers.WindowHelper.GetWindowForElement(this);
            var file = await PickHelper.PickSaveFileAsync("JumpBridges.json", win);
            if (file != null)
            {
                File.WriteAllText(file.Path, JsonConvert.SerializeObject(Services.Settings.JumpBridgeSetting.GetValue()));
                (win as BaseWindow).ShowSuccess(Helpers.ResourcesHelper.GetString("JumpBridgeSetting_Output_Success"));
            }
        }

        private async void Button_Input_Click(object sender, RoutedEventArgs e)
        {
            var win = Helpers.WindowHelper.GetWindowForElement(this);
            try
            {
                var file = await PickHelper.PickFileAsync(win, ".json");
                if (file != null)
                {
                    string content = File.ReadAllText(file.Path);
                    if (!string.IsNullOrEmpty(content))
                    {
                        var bridges = JsonConvert.DeserializeObject<List<Services.Settings.JumpBridgeSetting.JumpBridge>>(content);
                        HashSet<int> ids = new HashSet<int>();
                        foreach (var bridge in bridges)
                        {
                            ids.Add(bridge.System1);
                            ids.Add(bridge.System2);
                        }
                        var systemsDic = Core.Services.DB.MapSolarSystemService.Query(ids.ToList()).ToDictionary(p => p.SolarSystemID);
                        foreach (var bridge in bridges)
                        {
                            Core.DBModels.MapSolarSystem system1 = systemsDic[bridge.System1];
                            Core.DBModels.MapSolarSystem system2 = systemsDic[bridge.System2];
                            if (Services.Settings.JumpBridgeSetting.Add(bridge.System1, bridge.System2))
                            {
                                _bridges.Add(new Tuple<Core.DBModels.MapSolarSystem, Core.DBModels.MapSolarSystem>(system1, system2));
                            }
                        }
                        (win as BaseWindow).ShowSuccess(Helpers.ResourcesHelper.GetString("JumpBridgeSetting_Input_Success"));
                    }
                }
            }
            catch(Exception ex)
            {
                Core.Log.Error(ex);
                (win as BaseWindow).ShowError(ex.Message);
            }
        }
    }
}
