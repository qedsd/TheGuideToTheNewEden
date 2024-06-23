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
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WinUI.Controls;
using TheGuideToTheNewEden.WinUI.Converters;
using TheGuideToTheNewEden.WinUI.Models.Map;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static TheGuideToTheNewEden.WinUI.Controls.MapDataTypeControl;
using static TheGuideToTheNewEden.WinUI.Extensions.PageExtension;

namespace TheGuideToTheNewEden.WinUI.Views.Map
{
    public sealed partial class MapPage : Page
    {
        private BaseWindow _window;
        private Dictionary<int, MapData> _systemDatas;
        private bool _isSystemData = true;
        public MapPage()
        {
            this.InitializeComponent();
            Loaded += MapPage_Loaded;
        }
        private void MapPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MapPage_Loaded;
            _window = this.GetBaseWindow();
            InitData();
            SearchTypeComboBox.SelectionChanged += SearchTypeComboBox_SelectionChanged;
            MapSystemSelector.OnSelectedItemChanged += MapSystemSelector_OnSelectedItemChanged;
            RegionSelector.OnSelectedItemChanged += RegionSelector_OnSelectedItemChanged;
            InitSOV();
        }

        private void MapCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            MapCanvas.Loaded -= MapCanvas_Loaded;
        }

        private void InitData()
        {
            var posDic = SolarSystemPosHelper.PositionDic;
            var mapSolarSystems = MapSolarSystemService.Query(posDic.Values.Where(p => string.IsNullOrEmpty(p.SolarSystemName)).Select(p => p.SolarSystemID).ToList());
            _systemDatas = new Dictionary<int, MapData>();
            foreach (var mapSolarSystem in mapSolarSystems)
            {
                MapSystemData mapSystemData = new MapSystemData(posDic[mapSolarSystem.SolarSystemID], mapSolarSystem);
                _systemDatas.Add(mapSolarSystem.SolarSystemID, mapSystemData);
            }
            var regionPosDic = RegionMapHelper.PositionDic;
            var mapRegions = MapRegionService.Query(regionPosDic.Keys.ToList());
            ResetXYToFix(_systemDatas);
            MapCanvas.SetData(_systemDatas);
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

        private void MapSystemSelector_OnSelectedItemChanged(Core.DBModels.MapSolarSystem selectedItem)
        {
            if(selectedItem != null)
                MapCanvas.ToSystem(selectedItem.SolarSystemID);
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

        private void SystemFilterControl_OnFilterSystemChanged(HashSet<int> ids)
        {
            if (_isSystemData)
            {
                foreach (var data in _systemDatas)
                {
                    if(!ids.Contains(data.Key))
                    {
                        data.Value.Enable = false;
                    }
                    else
                    {
                        data.Value.Enable = true;
                    }
                }
            }
            MapCanvas.Draw();
        }

        private List<SovData> _sovDatas;
        private async void InitSOV()
        {
            _sovDatas = new List<SovData>();
            _window?.ShowWaiting();
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
                        _sovDatas.Add(new SovData()
                        {
                            AllianceId = item.Key,
                            SystemIds = ints
                        });
                    }
                }
                if (_sovDatas.Count != 0)
                {
                    var names = await Core.Services.IDNameService.GetByIdsAsync(_sovDatas.Select(p => p.AllianceId).ToList());
                    if (names != null && names.Any())
                    {
                        var nameDic = names.ToDictionary(p => p.Id);
                        foreach (var sovData in _sovDatas)
                        {
                            if (nameDic.TryGetValue(sovData.GroupId, out var name))
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
                        foreach (var sovData in _sovDatas)
                        {
                            sovData.AllianceName = sovData.AllianceId.ToString();
                        }
                    }
                    _sovDatas = _sovDatas.OrderByDescending(p => p.Count).ToList();
                    int groupId = 1;
                    foreach(var sovData in _sovDatas)
                    {
                        sovData.GroupId = groupId++;
                    }
                }
            }
            else
            {
                _window?.ShowError(resp.StatusCode.ToString());
            }
            _window?.HideWaiting();
            SystemFilterControl?.SetSOVData(_sovDatas);
            MapDataTypeControl?.SetSOVData(_sovDatas);
        }

        #region 显示设置
        private void MapDataTypeControl_OnDataTypChanged(int type)
        {
            switch(type)
            {
                case 0:SetDataToSecurity();break;
            }
        }
        private void SetDataToSecurity()
        {
            foreach(var data in _systemDatas.Values)
            {
                data.InnerText = (data as MapSystemData).MapSolarSystem.Security.ToString("N1");
                data.BgColor = Converters.SystemSecurityForegroundConverter.Convert((data as MapSystemData).MapSolarSystem.Security).Color;
            }
            MapCanvas.Draw();
        }
        delegate long CalResources(Core.DBModels.PlanetResources resources);
        private long CalResourcesPower(Core.DBModels.PlanetResources resources)
        {
            return resources == null ? 0 : resources.Power;
        }
        private long CalResourcesWorkforce(Core.DBModels.PlanetResources resources)
        {
            return resources == null ? 0 : resources.Workforce;
        }
        private long CalResourcesPowerAndWorkforce(Core.DBModels.PlanetResources resources)
        {
            return resources == null ? 0 : resources.Power + resources.Workforce;
        }
        private async void SetDataToPlanetResourc(int type)
        {
            _window?.ShowWaiting();
            CalResources calResources = null;
            switch (type)
            { 
                case 0: calResources = CalResourcesPower;break;
                case 1: calResources = CalResourcesWorkforce; break;
                case 2: calResources = CalResourcesPowerAndWorkforce; break;
            }
            double max = double.MinValue;//min为0
            await Task.Run(() =>
            {
                var resourcesDic = SolarSystemResourcesService.GetPlanetResourcesDetailsBySolarSystemID(_systemDatas.Keys.ToList());
                foreach (var data in _systemDatas.Values)
                {
                    if(resourcesDic.TryGetValue(data.Id, out var resources))
                    {
                        var resc = resources.Sum(p => calResources(p.PlanetResources));
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
        private void SetDataToSOV()
        {
            MapCanvas.Draw();
        }

        private void MapDataTypeControl_OnSOVDatasChanged(List<SovData> sovDatas)
        {
            SetDataToSOV();
        }
        private void MapDataTypeControl_OnPlanetResourceTypeChanged(int type)
        {
            SetDataToPlanetResourc(type);
        }
        #endregion
    }
}
