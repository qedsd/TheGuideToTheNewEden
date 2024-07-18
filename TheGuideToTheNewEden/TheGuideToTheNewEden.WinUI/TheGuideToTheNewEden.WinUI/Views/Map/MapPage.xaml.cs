using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Timers;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.EVEHelpers;
using TheGuideToTheNewEden.Core.Models.Map;
using TheGuideToTheNewEden.Core.Models.PlanetResources;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WinUI.Controls;
using TheGuideToTheNewEden.WinUI.Converters;
using TheGuideToTheNewEden.WinUI.Dialogs;
using TheGuideToTheNewEden.WinUI.Models.Map;
using TheGuideToTheNewEden.WinUI.Views.Map.Tools;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static TheGuideToTheNewEden.WinUI.Controls.MapDataTypeControl;
using static TheGuideToTheNewEden.WinUI.Extensions.PageExtension;
using static Vanara.PInvoke.Kernel32;

namespace TheGuideToTheNewEden.WinUI.Views.Map
{
    public sealed partial class MapPage : Page, IPage
    {
        private const int SuperionicIceID = 81144;
        private const int MagmaticGasID = 81143;

        private BaseWindow _window;
        private Dictionary<int, MapData> _systemDatas;
        private Dictionary<int, MapSolarSystem> _mapSolarSystems;
        private Dictionary<int, MapRegion> _mapRegions;
        /// <summary>
        /// key为星系id
        /// </summary>
        private Dictionary<int, SovData> _sovDatas;
        private Dictionary<int, Core.Models.PlanetResources.SolarSystemResources> _systemResourcesDic;
        private Dictionary<int, Core.Models.PlanetResources.RegionResources> _regionResourcesDic;
        private List<Core.Models.PlanetResources.Upgrade> _upgrades;
        private Dictionary<int, ESI.NET.Models.Universe.Kills> _systemKills;
        private Dictionary<int, int> _systemJumps;
        public MapPage()
        {
            this.InitializeComponent();
            Loaded += MapPage_Loaded;
        }

        #region 初始化数据
        private void MapPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MapPage_Loaded;
            _window = this.GetBaseWindow();
            SearchTypeComboBox.SelectionChanged += SearchTypeComboBox_SelectionChanged;
            MapSystemSelector.OnSelectedItemChanged += MapSystemSelector_OnSelectedItemChanged;
            RegionSelector.OnSelectedItemChanged += RegionSelector_OnSelectedItemChanged;
            Init();
        }
        private async void Init()
        {
            _window?.ShowWaiting("Loading System Data");
            await InitData();
            MapCanvas.SetData(_systemDatas);

            _window?.ShowWaiting("Loading SOV Data");
            var sovDatas = await InitSOV();
            MapDataTypeControl?.SetSOVData(sovDatas);
            SystemFilterControl?.SetData(_mapSolarSystems, _mapRegions, sovDatas);

            _window?.ShowWaiting("Loading PlanetResources data");
            await InitPlanetResourcesData();
            RegionPlanetResourcList.ItemsSource = _regionResourcesDic.Values;
            UpdataSystemPlanetResourcList(0);
            UpgradeList.ItemsSource = _upgrades;

            _window?.ShowWaiting("Loading Statistics data");
            await InitStatistics();
            MapNavigation.SetData(MapCanvas,_systemKills, _systemJumps, _sovDatas);
            OneJumpCover.SetData(MapCanvas, _systemKills, _systemJumps, _sovDatas);
            _window?.HideWaiting();
        }
        private async Task InitData()
        {
            var posDic = SolarSystemPosHelper.PositionDic;
            _mapSolarSystems = (await Core.Services.DB.MapSolarSystemService.QueryAllAsync()).Where(p => !p.IsSpecial()).ToDictionary(p=>p.SolarSystemID);
            _systemDatas = new Dictionary<int, MapData>();
            foreach (var mapSolarSystem in _mapSolarSystems.Values)
            {
                if(posDic.TryGetValue(mapSolarSystem.SolarSystemID, out var pos))
                {
                    MapSystemData mapSystemData = new MapSystemData(pos, mapSolarSystem);
                    _systemDatas.Add(mapSolarSystem.SolarSystemID, mapSystemData);
                }
            }
            _mapRegions = (await Core.Services.DB.MapRegionService.QueryAllAsync()).Where(p => !p.IsSpecial()).ToDictionary(p => p.RegionID);
            ResetXYToFix(_systemDatas);
        }
        private async Task<List<SovData>> InitSOV()
        {
            List<SovData> sovDatas = new List<SovData>();
            try
            {
                var resp = await Core.Services.ESIService.GetDefaultEsi().Sovereignty.Systems();
                if (resp.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var dic = resp.Data.GroupBy(p => p.AllianceId).ToList();
                    foreach (var item in dic)
                    {
                        if (item.Key > 0)
                        {
                            HashSet<int> ints = new HashSet<int>();
                            foreach (var p in item)
                            {
                                ints.Add(p.SystemId);
                            }
                            sovDatas.Add(new SovData()
                            {
                                AllianceId = item.Key,
                                SystemIds = ints
                            });
                        }
                    }
                    if (sovDatas.Count != 0)
                    {
                        var names = await Core.Services.IDNameService.GetByIdsAsync(sovDatas.Select(p => p.AllianceId).ToList());
                        if (names != null && names.Any())
                        {
                            var nameDic = names.ToDictionary(p => p.Id);
                            foreach (var sovData in sovDatas)
                            {
                                if (nameDic.TryGetValue(sovData.AllianceId, out var name))
                                {
                                    sovData.AllianceName = name.Name;
                                }
                                else
                                {
                                    sovData.AllianceName = sovData.AllianceId.ToString();
                                }
                            }
                        }
                        else
                        {
                            foreach (var sovData in sovDatas)
                            {
                                sovData.AllianceName = sovData.AllianceId.ToString();
                            }
                        }
                        sovDatas = sovDatas.OrderByDescending(p => p.Count).ToList();
                        //int groupId = 1;
                        //foreach(var sovData in sovDatas)
                        //{
                        //    sovData.GroupId = groupId++;
                        //}
                    }
                }
                else
                {
                    _window?.ShowError(resp.StatusCode.ToString());
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                _window?.ShowError(ex.Message);
            }
            _sovDatas = new Dictionary<int, SovData>();
            foreach (var data in sovDatas)
            {
                foreach (var sys in data.SystemIds)
                {
                    _sovDatas.Add(sys, data);
                }
            }
            return sovDatas;
        }
        private void ResetXYToFix(Dictionary<int, MapData> datas)
        {
            //缩放xy坐标到屏幕显示范围
            //以最大的x/y为参考最大显示范围
            var maxX = datas.Values.Max(p => p.OriginalX);
            var maxY = datas.Values.Max(p => p.OriginalY); 
            //将x缩放到UI显示区域
            float xScale = (float)(MapCanvas.ActualWidth / maxX);
            float yScale = xScale;//xy同一个缩放比例
            double xOffset = 0;
            double yOffset = 0;
            if (maxY * yScale > MapCanvas.ActualHeight)//按x最大比例缩放后若y超出范围，再按y最大比例缩放，即可确保xy都在范围内
            {
                yScale = (float)(MapCanvas.ActualHeight / maxY);
                xScale = yScale;

                yOffset = 0;
                var afterScaleMaxX = maxX * xScale;
                var emptyX = MapCanvas.ActualWidth - afterScaleMaxX;
                xOffset = emptyX / 2;
            }
            else
            {
                xOffset = 0;
                var afterScaleMaxY = maxY * yScale;
                var emptyY = MapCanvas.ActualHeight - afterScaleMaxY;
                yOffset = emptyY / 2;
            }
            foreach (var data in datas.Values)
            {
                data.OriginalX = (data.OriginalX * xScale) + (float)xOffset;
                data.OriginalY = (data.OriginalY * yScale) + (float)yOffset;
                data.X = data.OriginalX;
                data.Y = data.OriginalY;
            }
        }
        private async Task InitStatistics()
        {
            try
            {
                ESI.NET.EsiClient esiClient = Core.Services.ESIService.GetDefaultEsi();
                var kills = await esiClient.Universe.Kills();
                if (kills?.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    _systemKills = kills.Data.ToDictionary(p => p.SystemId);
                }
                else
                {
                    Core.Log.Error($"Init Kills Statistics Error: {kills?.StatusCode}");
                    _systemKills = new Dictionary<int, ESI.NET.Models.Universe.Kills>();
                }
                var jumps = await esiClient.Universe.Jumps();
                _systemJumps = new Dictionary<int, int>();
                if (jumps?.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    foreach (var p in jumps.Data)
                    {
                        _systemJumps.Add(p.SystemId, p.ShipJumps);
                    }
                }
                else
                {
                    Core.Log.Error($"Init Jumps Statistics Error: {jumps?.StatusCode}");
                }
            }
            catch(Exception ex)
            {
                Core.Log.Error(ex);
                _window?.ShowError(ex.Message);
                _systemKills = new Dictionary<int, ESI.NET.Models.Universe.Kills>();
                _systemJumps = new Dictionary<int, int>();
            }
        }
        #endregion

        #region 搜索
        private void MapSystemSelector_OnSelectedItemChanged(Core.DBModels.MapSolarSystem selectedItem)
        {
            if (selectedItem != null)
            {
                MapCanvas.ToSystem(selectedItem.SolarSystemID);
                if(_systemDatas.TryGetValue(selectedItem.SolarSystemID, out var data))
                {
                    ShowSystemInfo(data as MapSystemData);
                }
            }
        }

        private void RegionSelector_OnSelectedItemChanged(Core.DBModels.MapRegion selectedItem)
        {
            if(selectedItem != null)
                MapCanvas.ToSystem(_systemDatas.Values.Where(p=>(p as MapSystemData).MapSolarSystem.RegionID == selectedItem.RegionID).Select(p=>p.Id).ToList());
        }

        private void SearchTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((sender as ComboBox).SelectedIndex == 1)
            {
                MapSystemSelector.Visibility = Visibility.Collapsed;
                RegionSelector.Visibility = Visibility.Visible;
            }
            else
            {
                MapSystemSelector.Visibility = Visibility.Visible;
                RegionSelector.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region 显示设置
        private void MapDataTypeControl_OnDataTypChanged(int type)
        {
            //PlanetResourcePanel.Visibility = Visibility.Collapsed;
            switch (type)
            {
                case 0:SetDataToSecurity();break;
                case 1:;break;
                //case 2: PlanetResourcePanel.Visibility = Visibility.Visible; break;
            }
        }
        private void SetDataToSecurity()
        {
            foreach(var data in _systemDatas.Values)
            {
                data.InnerText = (data as MapSystemData).MapSolarSystem.Security.ToString("N2");
                data.BgColor = Converters.SystemSecurityForegroundConverter.Convert((data as MapSystemData).MapSolarSystem.Security).Color;
            }
            MapCanvas.Draw();
        }
        delegate long CalResources(Core.Models.PlanetResources.SolarSystemResources resources);
        private long CalResourcesPower(Core.Models.PlanetResources.SolarSystemResources resources)
        {
            return resources == null ? 0 : resources.Power;
        }
        private long CalResourcesWorkforce(Core.Models.PlanetResources.SolarSystemResources resources)
        {
            return resources == null ? 0 : resources.Workforce;
        }
        private long CalResourcesMagmaticGas(Core.Models.PlanetResources.SolarSystemResources resources)
        {
            return resources == null? 0 : resources.MagmaticGas;
        }
        private long CalResourcesSuperionicIce(Core.Models.PlanetResources.SolarSystemResources resources)
        {
            return resources == null  ? 0 : resources.SuperionicIce;
        }
        private async Task InitPlanetResourcesData()
        {
            await Task.Run(() =>
            {
                var planetResourcesDic = SolarSystemResourcesService.GetPlanetResourcesDetailsBySolarSystemID(_mapSolarSystems.Values.Where(p=>p.Security <= 0).Select(p=>p.SolarSystemID).ToList());

                #region region
                _regionResourcesDic = new Dictionary<int, Core.Models.PlanetResources.RegionResources>();
                var groupByRegion = _systemDatas.Values.GroupBy(p => (p as MapSystemData).MapSolarSystem.RegionID);
                foreach (var group in groupByRegion)
                {
                    Core.Models.PlanetResources.RegionResources regionResources = new Core.Models.PlanetResources.RegionResources();
                    regionResources.Region = Core.Services.DB.MapRegionService.Query(group.Key);
                    foreach (var system in group)
                    {
                        if (planetResourcesDic.TryGetValue(system.Id, out var resourcesDetails))
                        {
                            regionResources.Power += resourcesDetails.Sum(p => p.PlanetResources.Power);
                            regionResources.Workforce += resourcesDetails.Sum(p => p.PlanetResources.Workforce);
                            regionResources.SuperionicIce += resourcesDetails.Where(p => p.PlanetResources.ReagentTypeId == SuperionicIceID).Sum(p => p.PlanetResources.ReagentHarvestAmount);
                            regionResources.MagmaticGas += resourcesDetails.Where(p => p.PlanetResources.ReagentTypeId == MagmaticGasID).Sum(p => p.PlanetResources.ReagentHarvestAmount);
                        }
                    }
                    if (regionResources.Power + regionResources.Workforce != 0)
                    {
                        _regionResourcesDic.Add(group.Key, regionResources);
                    }
                }
                #endregion

                #region system
                _systemResourcesDic = new Dictionary<int, Core.Models.PlanetResources.SolarSystemResources>();
                foreach (var resources in planetResourcesDic)
                {
                    if (_systemDatas.TryGetValue(resources.Key, out var mapData))
                    {
                        Core.Models.PlanetResources.SolarSystemResources solarSystemResources = new Core.Models.PlanetResources.SolarSystemResources()
                        {
                            MapSolarSystem = (mapData as MapSystemData).MapSolarSystem,
                            Power = resources.Value.Sum(p => p.PlanetResources.Power),
                            Workforce = resources.Value.Sum(p => p.PlanetResources.Workforce),
                            MagmaticGas = resources.Value.Where(p => p.PlanetResources.ReagentTypeId == MagmaticGasID).Sum(p => p.PlanetResources.ReagentHarvestAmount),
                            SuperionicIce = resources.Value.Where(p => p.PlanetResources.ReagentTypeId == SuperionicIceID).Sum(p => p.PlanetResources.ReagentHarvestAmount),
                        };
                        _systemResourcesDic.Add(resources.Key, solarSystemResources);
                    }
                }
                #endregion

                #region upgrade
                _upgrades = Core.Services.UpgradeService.Current.GetUpgrades();
                #endregion
            });
        }
        private async void SetDataToPlanetResourc(int type)
        {
            _window?.ShowWaiting();
            CalResources calResources = null;
            switch (type)
            { 
                case 0: calResources = CalResourcesPower;break;
                case 1: calResources = CalResourcesWorkforce; break;
                case 2: calResources = CalResourcesMagmaticGas; break;
                case 3: calResources = CalResourcesSuperionicIce; break;
            }
            double max = double.MinValue;//min为0
            await Task.Run(() =>
            {
                foreach (var data in _systemDatas.Values)
                {
                    if(_systemResourcesDic.TryGetValue(data.Id, out var resources))
                    {
                        var resc = calResources(resources);
                        data.InnerText = ISKNormalizeConverter.Normalize(resc);
                        data.Tag = resc;
                        if(resc > max)
                        {
                            max = resc;
                        }
                    }
                    else
                    {
                        data.InnerText = "0";
                        data.Tag = 0L;
                    }
                }
            });
            //Color
            foreach (var data in _systemDatas.Values)
            {
                long resc = (long)data.Tag;
                data.BgColor = PlanetRecourceColorConverter.Convert(Math.Round(resc / max,1));
            }
            _window?.HideWaiting();
            MapCanvas.Draw();
        }
        private void SetDataToSOV(List<SovData> sovDatas)
        {
            var groups = sovDatas.GroupBy(p => p.GroupId);
            Random random = new Random();
            foreach(var data in _systemDatas)
            {
                data.Value.InnerText = string.Empty;
                data.Value.BgColor = Microsoft.UI.Colors.LightGray;
            }
            foreach (var group in groups)
            {
                byte r = (byte)random.Next(0, 255);
                byte g = (byte)random.Next(0, 255);
                byte b = (byte)random.Next(0, 255);
                Windows.UI.Color color = Windows.UI.Color.FromArgb(255,r,g,b);
                foreach(var data in group)
                {
                    foreach(var sys in data.SystemIds)
                    {
                        _systemDatas[sys].BgColor = color;
                        _systemDatas[sys].InnerText = data.GroupId.ToString();
                    }
                }
            }
            MapCanvas.Draw();
        }

        private void MapDataTypeControl_OnSOVDatasChanged(List<SovData> sovDatas)
        {
            SetDataToSOV(sovDatas);
        }
        private void MapDataTypeControl_OnPlanetResourceTypeChanged(int type)
        {
            SetDataToPlanetResourc(type);
        }
        #endregion

        #region 选中星系
        private void MapCanvas_OnPointedSystemChanged(MapData mapData)
        {
            if(mapData == null)
            {
                TopPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                TopPanel.Visibility = Visibility.Visible;
                PointedSystemName.Text = $"{(mapData as MapSystemData).MainText} {(mapData as MapSystemData).InnerText}";
            }
        }
        private void MapCanvas_OnSelectedSystemChanged(MapData mapData)
        {
            ShowSystemInfo(mapData as MapSystemData);
        }
        private void ShowSystemInfo(MapSystemData data)
        {
            if (data == null)
                return;
            SelectedSystemInfoPanel.Visibility = Visibility.Visible;
            SystemResourceDetailButton.Tag = data;
            SelectedSystemNameTextBlock.Text = data.MapSolarSystem.SolarSystemName;
            SelectedSystemIDTextBlock.Text = data.MapSolarSystem.SolarSystemID.ToString();
            SelectedSystemRegionTextBlock.Text = Core.Services.DB.MapRegionService.Query(data.MapSolarSystem.RegionID).RegionName;
            SelectedSystemSecurityTextBlock.Text = data.MapSolarSystem.Security.ToString("N2");

            if (_sovDatas != null && _sovDatas.TryGetValue(data.MapSolarSystem.SolarSystemID, out var sovData))
            {
                SelectedSystemSOVGrid.Visibility = Visibility.Visible;
                SelectedSystemSOVNameTextBlock.Text = sovData.AllianceName;
                SelectedSystemSOVIDTextBlock.Text = sovData.AllianceId.ToString();
            }
            else
            {
                SelectedSystemSOVGrid.Visibility = Visibility.Collapsed;
            }
            if (_systemResourcesDic != null && _systemResourcesDic.TryGetValue(data.MapSolarSystem.SolarSystemID, out var resource))
            {
                SelectedSystemResourceGrid.Visibility = Visibility.Visible;
                SelectedSystemPowerTextBlock.Text = resource.Power.ToString("N0");
                SelectedSystemWorkforceTextBlock.Text = resource.Workforce.ToString("N0");
                SelectedSystemMagmaticGasTextBlock.Text = resource.MagmaticGas.ToString("N0");
                SelectedSystemSuperionicIceTextBlock.Text = resource.SuperionicIce.ToString("N0");
            }
            else
            {
                SelectedSystemResourceGrid.Visibility = Visibility.Collapsed;
            }
        }
        private void CloseSelectedSystemInfoPanelButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedSystemInfoPanel.Visibility = Visibility.Collapsed;
        }
        private async void SystemResourceDetailButton_Click(object sender, RoutedEventArgs e)
        {
            var data = (sender as Button).Tag as MapSystemData;
            _sovDatas.TryGetValue(data.MapSolarSystem.SolarSystemID, out var sovData);
            SolarSystemResources resource = null;
            if(!_systemResourcesDic.TryGetValue(data.MapSolarSystem.SolarSystemID, out resource))
            {
                resource = new SolarSystemResources()
                {
                    MapSolarSystem = data.MapSolarSystem
                };
            }
            List<MapSystemInfo> jumpTos = new List<MapSystemInfo>();
            var jumpToIds = SolarSystemPosHelper.GetJumpTo(data.Id);
            if(jumpToIds != null)
            {
                foreach(var id in jumpToIds)
                {
                    MapSystemInfo mapSystemInfo = new MapSystemInfo()
                    {
                        System = _mapSolarSystems[id],
                    };
                    mapSystemInfo.Region = _mapRegions[mapSystemInfo.System.RegionID];
                    if(_sovDatas.TryGetValue(id,out var sov))
                    {
                        mapSystemInfo.Sov = sov.AllianceName;
                    }
                    jumpTos.Add(mapSystemInfo);
                }
            }
            MapSystemDetailInfo mapSystemDetailInfo = new MapSystemDetailInfo()
            {
                System = data.MapSolarSystem,
                Region = _mapRegions[data.MapSolarSystem.RegionID],
                Sov = sovData,
                Resources = resource,
                JumpTos = jumpTos
            };
            if(_systemKills.TryGetValue(data.MapSolarSystem.SolarSystemID,out var kills))
            {
                mapSystemDetailInfo.ShipKills = kills.ShipKills;
                mapSystemDetailInfo.NpcKills = kills.NpcKills;
                mapSystemDetailInfo.PodKills = kills.PodKills;
            }
            if(_systemJumps.TryGetValue(data.MapSolarSystem.SolarSystemID, out var jumps))
            {
                mapSystemDetailInfo.Jumps = jumps;
            }
            await MapSystemDetailDialog.ShowAsync(mapSystemDetailInfo, this.XamlRoot);
        }
        #endregion

        #region 筛选
        /// <summary>
        /// 筛选设置
        /// </summary>
        /// <param name="ids"></param>
        private void SystemFilterControl_OnFilterSystemChanged(HashSet<int> ids)
        {
            foreach (var data in _systemDatas)
            {
                if (!ids.Contains(data.Key))
                {
                    data.Value.Enable = false;
                }
                else
                {
                    data.Value.Enable = true;
                }
            }
            MapCanvas.Draw();
            UpdataSystemPlanetResourcList(Tool_SystemPlanetResourcList_Mode.SelectedIndex);
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
        #endregion

        #region 工具
        private void Tools_Click(object sender, RoutedEventArgs e)
        {
            MapCanvas.ClearMapGraph();
            MapCanvas.RemoveTemporary();
            MenuFlyoutItem menuFlyoutItem = sender as MenuFlyoutItem;
            foreach(var u in ToolGrid.Children)
            {
                u.Visibility = Visibility.Collapsed;
            }
            UIElement targetTool = null;
            switch(menuFlyoutItem.Tag.ToString())
            {
                case "None": targetTool = null; break;
                case "PlanetResource_Region": targetTool = RegionPlanetResourcList; break;
                case "PlanetResource_System": targetTool = Tool_SystemPlanetResourcList; break;
                case "PlanetResource_Upgrade": targetTool = UpgradeList; break;
                case "OneJumpCover": targetTool = Tool_OneJumpCover; break;
                case "Navigation": targetTool = Tool_Navigation; break;
            }
            if(targetTool == null)
            {
                ToolPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                ToolPanel.Visibility = Visibility.Visible;
                targetTool.Visibility = Visibility.Visible;
                ToolExpander.Header = menuFlyoutItem.Text;
                ToolExpander.IsExpanded = true;
            }
        }
        #endregion

        public void Close()
        {
            MapCanvas.Dispose();
        }
    }
}
