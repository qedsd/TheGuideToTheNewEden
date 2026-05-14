using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using ESI.NET.Models.Universe;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.Core.Models.Map;
using TheGuideToTheNewEden.WinUI.Controls;
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.Models.Map;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.ViewModels;
using Vanara;
using Windows.Foundation;
using Windows.Foundation.Collections;
using ZKB.NET.Models.KillStream;
using static TheGuideToTheNewEden.WinUI.Controls.MapDataTypeControl;
using TheGuideToTheNewEden.Core.Extensions;
using DevWinUI;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics;
using TheGuideToTheNewEden.WinUI.Wins;

namespace TheGuideToTheNewEden.WinUI.Views.Map.Tools
{
    public sealed partial class IntelTool : Page
    {
        private MapCanvas _mapCanvas;
        private ToolWindow _window;
        public IntelTool(MapCanvas mapCanvas, Dictionary<int, MapData> systemDatas, Dictionary<int, SovData> sovDatas)
        {
            _mapCanvas = mapCanvas;
            this.InitializeComponent();
            VM.Init(mapCanvas, systemDatas, sovDatas);
            Loaded += IntelTool_Loaded;
        }

        private void IntelTool_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= IntelTool_Loaded;
            ExclusionsIdNameSearchBox.SetFilterTypes(new List<Core.DBModels.IdName.CategoryEnum>()
            {
                Core.DBModels.IdName.CategoryEnum.Character,
                Core.DBModels.IdName.CategoryEnum.Corporation,
                Core.DBModels.IdName.CategoryEnum.Alliance,
                Core.DBModels.IdName.CategoryEnum.SolarSystem,
                Core.DBModels.IdName.CategoryEnum.Region,
                Core.DBModels.IdName.CategoryEnum.InventoryType,
            }); 
            ExclusionsIdNameSearchBox.OnSelectedItemChanged += ExclusionsIdNameSearchBox_OnSelectedItemChanged;

            InclusionsIdNameSearchBox.SetFilterTypes(new List<Core.DBModels.IdName.CategoryEnum>()
            {
                Core.DBModels.IdName.CategoryEnum.Character,
                Core.DBModels.IdName.CategoryEnum.Corporation,
                Core.DBModels.IdName.CategoryEnum.Alliance,
                Core.DBModels.IdName.CategoryEnum.SolarSystem,
                Core.DBModels.IdName.CategoryEnum.Region,
                Core.DBModels.IdName.CategoryEnum.InventoryType,
            });
            InclusionsIdNameSearchBox.OnSelectedItemChanged += InclusionsIdNameSearchBox_OnSelectedItemChanged;

            _window = this.GetWindow() as  ToolWindow;
            _window.Closed += IntelTool_Closed;
            VM.SetWindow(this.GetWindow() as ToolWindow);
        }

        private void IntelTool_Closed(object sender, WindowEventArgs args)
        {
            VM.Dispose();
        }

        #region 过滤列表
        private void ExclusionsIdNameSearchBox_OnSelectedItemChanged(object sender, Core.DBModels.IdName e)
        {
            VM.Config.Exclusions.Add(e);
        }

        private void DeleteExclusions_Click(object sender, RoutedEventArgs e)
        {
            var data = (sender as Button)?.DataContext as Core.DBModels.IdName;
            if(data != null)
            {
                VM.Config.Exclusions.Remove(data);
            }
        }
        private void InclusionsIdNameSearchBox_OnSelectedItemChanged(object sender, Core.DBModels.IdName e)
        {
            VM.Config.Inclusions.Add(e);
        }

        private void DeleteInclusions_Click(object sender, RoutedEventArgs e)
        {
            var data = (sender as Button)?.DataContext as Core.DBModels.IdName;
            if (data != null)
            {
                VM.Config.Inclusions.Remove(data);
            }
        }
        #endregion


        private async void AttackerMain_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button).DataContext as MapIntelAttacker;
            if (item != null)
            {
                _window.ShowWaiting();
                try
                {
                    await ClientServiceHelper.GetRequiredService<KBNavigationService>().NavigationTo(item.Main, true);
                }
                catch (Exception ex)
                {
                    _window.ShowError(ex.Message);
                }
                finally
                {
                    _window.HideWaiting();
                }
            }
        }

        private async void AttackerCharacter_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button).DataContext as MapIntelAttacker;
            if (item != null)
            {
                _window.ShowWaiting();
                try
                {
                    await ClientServiceHelper.GetRequiredService<KBNavigationService>().NavigationTo(item.Attacker, true);
                }
                catch (Exception ex)
                {
                    _window.ShowError(ex.Message);
                }
                finally
                {
                    _window.HideWaiting();
                }
            }
        }

        private async void AttackerFaction_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button).DataContext as MapIntelAttacker;
            if (item != null)
            {
                _window.ShowWaiting();
                try
                {
                    await ClientServiceHelper.GetRequiredService<KBNavigationService>().NavigationTo(item.Faction, true);
                }
                catch (Exception ex)
                {
                    _window.ShowError(ex.Message);
                }
                finally
                {
                    _window.HideWaiting();
                }
            }
        }

        private async void Victim_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button).DataContext as MapIntelMsg;
            if (item != null)
            {
                _window.ShowWaiting();
                try
                {
                    await ClientServiceHelper.GetRequiredService<KBNavigationService>().NavigationTo(item.KB.Victim, true);
                }
                catch (Exception ex)
                {
                    _window.ShowError(ex.Message);
                }
                finally
                {
                    _window.HideWaiting();
                }
            }
        }

        private async void VictimShip_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button).DataContext as MapIntelMsg;
            if (item != null)
            {
                _window.ShowWaiting();
                try
                {
                    await ClientServiceHelper.GetRequiredService<KBNavigationService>().NavigationTo(new IdName(item.KB.Type.TypeID, item.KB.Type.TypeName, IdName.CategoryEnum.InventoryType), true);
                }
                catch (Exception ex)
                {
                    _window.ShowError(ex.Message);
                }
                finally
                {
                    _window.HideWaiting();
                }
            }
        }

        private async void VictimFaction_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button).DataContext as MapIntelMsg;
            if (item != null)
            {
                _window.ShowWaiting();
                try
                {
                    await ClientServiceHelper.GetRequiredService<KBNavigationService>().NavigationTo(item.KB.VictimFctionName, true);
                }
                catch (Exception ex)
                {
                    _window.ShowError(ex.Message);
                }
                finally
                {
                    _window.HideWaiting();
                }
            }
        }

        private void MsgListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var msg = e.ClickedItem as MapIntelMsg;
            if (msg?.Type == MapIntelType.KB)
            {
                ClientServiceHelper.GetRequiredService<KBNavigationService>().NavigateToKM(msg.KB, true);
            }
            else if(msg?.Type == MapIntelType.Channel)
            {
                var channelMsg = msg as MapIntelChannelMsg;
                if (channelMsg != null)
                {
                    var hwnd = Helpers.WindowHelper.GetGameHwndByCharacterName(channelMsg.ChhannelContent.Listener);
                    if (hwnd != IntPtr.Zero)
                    {
                        Helpers.WindowHelper.SetForegroundWindow_Click(hwnd);
                    }
                }
            }
        }

        private async void SOV_Click(object sender, RoutedEventArgs e)
        {
            var msg = (sender as Button).DataContext as MapIntelMsg;
            if(msg != null)
            {
                _window.ShowWaiting();
                try
                {
                    await ClientServiceHelper.GetRequiredService<KBNavigationService>().NavigationTo(msg.Sov.AllianceId, ZKB.NET.EntityType.AllianceID, msg.Sov.AllianceName, true);
                }
                catch (Exception ex)
                {
                    _window.ShowError(ex.Message);
                }
                finally
                {
                    _window.HideWaiting();
                }
            }
        }

        private void System_Click(object sender, RoutedEventArgs e)
        {
            var msg = (sender as Button).DataContext as MapIntelMsg;
            if (msg != null)
            {
                Helpers.WindowHelper.MainWindow.Activate();
                _mapCanvas.ToSystem(msg.System.SolarSystemID);
            }
        }

        private void Region_Click(object sender, RoutedEventArgs e)
        {
            var msg = (sender as Button).DataContext as MapIntelMsg;
            if (msg != null)
            {
                Helpers.WindowHelper.MainWindow.Activate();
                _mapCanvas.ToRegion(msg.System.RegionID);
            }
        }

        private void Msg_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var msg = (sender as FrameworkElement).DataContext as MapIntelMsg;
            if (msg != null)
            {
                Helpers.WindowHelper.MainWindow.Activate();
                _mapCanvas.ToSystem(msg.System.SolarSystemID);
            }
        }

        private void Setting_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            SettingGrid.Visibility = Visibility.Collapsed;
            LiveDataGrid.Visibility = Visibility.Collapsed;
            TextBlock_LiveDataTip.Visibility = Visibility.Collapsed;
            if (sender.SelectedItem != null)
            {
                switch (sender.SelectedItem.Tag.ToString())
                {
                    case "0": SettingGrid.Visibility = Visibility.Visible; break;
                    case "1": LiveDataGrid.Visibility = Visibility.Visible; TextBlock_LiveDataTip.Visibility = Visibility.Visible; break;
                }
            }
        }

        private void Setting2_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            IntelFromSetting.Visibility = Visibility.Collapsed;
            FilterSetting.Visibility = Visibility.Collapsed;
            if (sender.SelectedItem != null)
            {
                switch (sender.SelectedItem.Tag.ToString())
                {
                    case "0": IntelFromSetting.Visibility = Visibility.Visible; break;
                    case "1": FilterSetting.Visibility = Visibility.Visible; break;
                }
            }
        }
    }
    public class MapIntelMsg : ObservableObject
    {
        public string GUID = Guid.NewGuid().ToString();
        public MapIntelType Type { get; set; }
        public DateTime Time { get; set; }
        public string Msg { get; set; }
        public Core.DBModels.MapSolarSystem System { get; set; }
        public Core.DBModels.MapRegion Region { get; set; }
        public SovData Sov { get; set; }
        public Core.Models.KB.KBItemInfo KB { get; set; }
        public List<MapIntelAttacker> Attackers { get; set; }

        private double _elapsed;
        public double Elapsed
        {
            get => _elapsed;
            set
            {
                SetProperty(ref _elapsed, value);
                ElapsedStr = $"{value} min";
            }
        }

        private string _elapsedStr;
        public string ElapsedStr { get => _elapsedStr; set => SetProperty(ref _elapsedStr, value); }
    }
    public class MapIntelChannelMsg : MapIntelMsg
    {
        public EarlyWarningContent ChhannelContent { get; set; }
    }
    public class MapIntelAttacker
    {
        /// <summary>
        /// 0 单人
        /// 1 舰船统计
        /// 2 势力统计
        /// </summary>
        public int Type {  get; set; }
        public IdName Attacker { get; set; }
        public IdName Faction { get; set; }
        public string FactionIconURL { get; set; }
        public string AttackerURL { get; set; }
        public IdName Main { get; set; }
        public string MainIconURL { get; set; }
        public string MainContent { get; set; }
        public int StatisticCount {  get; set; }
        public List<MapIntelAttacker> Attackers { get; set; }
    }
    public enum MapIntelType
    {
        KB,
        Channel
    }
    public class IntelToolMsgTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ChannelTemplate { get; set; }

        public DataTemplate KBTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            if ((item as MapIntelMsg).Type == MapIntelType.KB)
            {
                return KBTemplate;
            }
            else
            {
                return ChannelTemplate;
            }
        }
    }
    public class MapIntelAttackerTemplateSelector : DataTemplateSelector
    {
        public DataTemplate AttackerTemplate { get; set; }

        public DataTemplate StatisticTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            if ((item as MapIntelAttacker).Type != 0)
            {
                return StatisticTemplate;
            }
            else
            {
                return AttackerTemplate;
            }
        }
    }
}
