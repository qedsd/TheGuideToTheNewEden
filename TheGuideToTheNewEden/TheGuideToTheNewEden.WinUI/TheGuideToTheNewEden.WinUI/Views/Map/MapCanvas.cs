using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.EVEHelpers;
using TheGuideToTheNewEden.Core.Models.Map;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WinUI.Views.Map
{
    internal class MapCanvas : UserControl
    {
        private CanvasControl _canvasControl;
        /// <summary>
        /// 当前缩放
        /// </summary>
        private float _currentZoom = 1;
        private const float _stepZoom = 0.2f;
        private const float _maxZoom = 10;
        private const float _minZoom = 1;
        private Dictionary<int, MapData> _systemDatas;
        private Dictionary<int, MapData> _regionDatas;
        public MapCanvas()
        {
            _canvasControl = new CanvasControl();
            _canvasControl.CreateResources += CanvasControl_CreateResources;
            _canvasControl.Draw += CanvasControl_Draw;
            _canvasControl.PointerPressed += CanvasControl_PointerPressed;
            _canvasControl.PointerReleased += CanvasControl_PointerReleased;
            _canvasControl.PointerMoved += CanvasControl_PointerMoved;
            _canvasControl.PointerWheelChanged += CanvasControl_PointerWheelChanged;
            Content = _canvasControl;
        }

        private void CanvasControl_CreateResources(CanvasControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
        {
            InitData();
            SetMode(MapMode.System);
            Draw();
        }
        private double _scaleCenterX = 0;
        private double _scaleCenterY = 0;
        private void CanvasControl_PointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(sender as UIElement);
            _scaleCenterX = point.Position.X;
            _scaleCenterY = point.Position.Y;
            
            var delta = point.Properties.MouseWheelDelta > 0 ? -1f : 1f;//-1缩小 1 放大
            var newZoom = _stepZoom * delta;
            var scaleCenterXOffset = (float)(_scaleCenterX *( 1 + newZoom) - _scaleCenterX);
            var scaleCenterYOffset = (float)(_scaleCenterY * (1 + newZoom) - _scaleCenterY);
            Draw(newZoom, -scaleCenterXOffset, -scaleCenterYOffset);
        }
        private double _lastPressedX = 0;
        private double _lastPressedY = 0;
        private void CanvasControl_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var pointerPoint = e.GetCurrentPoint(sender as UIElement);
            _lastPressedX = pointerPoint.Position.X;
            _lastPressedY = pointerPoint.Position.Y;
            Debug.WriteLine($"PointerPressed:{_lastPressedX} {_lastPressedY}");
        }
        private void CanvasControl_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var pointerPoint = e.GetCurrentPoint(sender as UIElement);
            if (pointerPoint.Properties.IsLeftButtonPressed)
            {
                var xOffset = (float)(pointerPoint.Position.X - _lastPressedX);
                var yOffset = (float)(pointerPoint.Position.Y - _lastPressedY);
                _lastPressedX = pointerPoint.Position.X;
                _lastPressedY = pointerPoint.Position.Y;
                Draw(0, xOffset, yOffset);
            }
        }
        private void CanvasControl_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            _lastPressedX = 0;
            _lastPressedY = 0;
        }
        private Dictionary<int, MapData> _usingMapDatas;
        private void CanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if(_usingMapDatas != null)
            {
                int invisibleCount = 0;
                foreach (var data in _usingMapDatas.Values)
                {
                    double drawX = data.X;
                    double drawY = data.Y;
                    if (drawX <= sender.ActualWidth && drawY <= sender.ActualHeight && drawX >= 0 && drawY >= 0)
                    {
                        args.DrawingSession.FillRectangle((float)drawX, (float)drawY, data.W, data.H, data.BgColor);
                    }
                    else
                    {
                        invisibleCount++;
                    }
                }
            }
        }
        public void Draw()
        {
            Draw(0, 0, 0);
        }
        private void Draw(float zoom, double xOffset, double yOffset)
        {
            _currentZoom += zoom;
            Debug.WriteLine($"{_currentZoom} {zoom} {xOffset} {yOffset}");
            UpdateData(zoom, xOffset, yOffset);
            _canvasControl.Invalidate();
        }
        private void UpdateData(float zoom, double xOffset, double yOffset)
        {
            var xyZoom = 1 + zoom;
            var whZoom = 1 + zoom / 2;
            foreach (var data in _usingMapDatas.Values)
            {
                data.X = data.X * xyZoom + xOffset;
                data.Y = data.Y * xyZoom + yOffset;
                data.W = data.OriginalW * _currentZoom;
                data.H = data.OriginalH * _currentZoom;
            }
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
            _regionDatas = new Dictionary<int, MapData>();
            foreach (var mapRegion in mapRegions)
            {
                var regionPos = regionPosDic[mapRegion.RegionID];
                MapRegionData mapRegionData = new MapRegionData(regionPos, mapRegion);
                _regionDatas.Add(mapRegion.RegionID, mapRegionData);
            }
            ResetXYToFix(_systemDatas);
            ResetXYToFix(_regionDatas);
        }
        private void ResetXYToFix(Dictionary<int, MapData> datas)
        {
            //缩放xy坐标到屏幕显示范围
            //以最大的x/y为参考最大显示范围
            var maxX = datas.Values.Max(p => p.OriginalX);
            var maxY = datas.Values.Max(p => p.OriginalY);
            //将x缩放到UI显示区域
            var xScale = this.ActualWidth / maxX;
            double yScale = xScale;//xy同一个缩放比例
            double xOffset = 0;
            double yOffset = 0;
            if (maxY * yScale > this.ActualHeight)//按x最大比例缩放后若y超出范围，再按y最大比例缩放，即可确保xy都在范围内
            {
                yScale = this.ActualHeight / maxY;
                xScale = yScale;

                yOffset = 0;
                var afterScaleMaxX = maxX * xScale;
                var emptyX = this.ActualWidth - afterScaleMaxX;
                xOffset = emptyX / 2;
            }
            else
            {
                xOffset = 0;
                var afterScaleMaxY = maxY * yScale;
                var emptyY = this.ActualHeight - afterScaleMaxY;
                yOffset = emptyY / 2;
            }
            foreach (var data in datas.Values)
            {
                data.OriginalX = (data.OriginalX * xScale) + xOffset;
                data.OriginalY = (data.OriginalY * yScale) + yOffset;
                data.X = data.OriginalX;
                data.Y = data.OriginalY;
            }
        }

        #region public
        public void SetMode(MapMode mapMode)
        {
            switch(mapMode)
            {
                case MapMode.Region:
                    {
                        _usingMapDatas = _regionDatas;
                    }
                    break;
                case MapMode.System:
                    {
                        _usingMapDatas = _systemDatas;
                    }
                    break;
            }
        }
        #endregion

        #region class
        public class MapData
        {
            public MapData(MapPosition pos)
            {
                OriginalX = pos.X;
                OriginalY = pos.Y;
            }
            public double OriginalX { get; set; }
            public double OriginalY { get; set; }
            public double X { get; set; }
            public double Y { get; set; }

            public float OriginalW { get; set; } = 4;
            public float OriginalH { get; set; } = 4;
            public float W { get; set; } = 4;
            public float H { get; set; } = 4;
            public string InnerText { get; set; }
            public string MainText { get; set; }
            public Windows.UI.Color BgColor { get; set; }
        }
        public class MapSystemData : MapData
        {
            public MapSystemData(SolarSystemPosition solarSystemPosition, MapSolarSystem mapSolarSystem):base(solarSystemPosition)
            {
                SolarSystemPosition = solarSystemPosition;
                MapSolarSystem = mapSolarSystem;
                X = SolarSystemPosition.X;
                Y = SolarSystemPosition.Y;
                MainText = MapSolarSystem.SolarSystemName;
                InnerText = MapSolarSystem.Security.ToString("N1");
                BgColor = Converters.SystemSecurityForegroundConverter.Convert(MapSolarSystem.Security).Color;
            }
            public SolarSystemPosition SolarSystemPosition { get; set; }
            public Core.DBModels.MapSolarSystem MapSolarSystem { get; set; }
        }
        public class MapRegionData: MapData
        {
            public RegionPosition RegionPosition { get; set; }
            public Core.DBModels.MapRegion MapRegion { get; set; }
            public MapRegionData(RegionPosition regionPosition ,Core.DBModels.MapRegion mapRegion) : base(regionPosition)
            {
                RegionPosition = regionPosition;
                MapRegion = mapRegion;
                X = RegionPosition.X;
                Y = RegionPosition.Y;
                MainText = MapRegion.RegionName;
                BgColor = Colors.Green;
            }
        }
        public enum MapMode
        {
            Region,System
        }
        #endregion
    }
}
