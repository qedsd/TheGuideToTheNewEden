using log4net.Core;
using Microsoft.Graphics.Canvas.Text;
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
using static Vanara.PInvoke.User32.RAWINPUT;

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
        private const float _lineZoom = 2;
        private const float _detailZoom = 6;
        private const float _maxZoom = 10;
        private const float _minZoom = 1;
        private Dictionary<int, MapData> _systemDatas;
        private Dictionary<int, MapData> _regionDatas;
        private Windows.UI.Color _mainTextColor;
        private Windows.UI.Color _linkColor;
        private bool _isDark = false;
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

            SetMainTextColor(Services.Settings.ThemeSelectorService.IsDark);
            Services.Settings.ThemeSelectorService.OnChangedTheme += ThemeSelectorService_OnChangedTheme;
        }
        private void SetMainTextColor(bool isDark)
        {
            Windows.UI.Color color = isDark ? Colors.White : Colors.Black;
            _mainTextColor = Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
            _linkColor = isDark ? Windows.UI.Color.FromArgb(50, Colors.Gray.R, Colors.Gray.G, Colors.Gray.B) : Windows.UI.Color.FromArgb(200, Colors.LightGray.R, Colors.LightGray.G, Colors.LightGray.B); ;
        }
        private void ThemeSelectorService_OnChangedTheme(ElementTheme theme)
        {
            _isDark = Services.Settings.ThemeSelectorService.IsDark;
            SetMainTextColor(_isDark);
            Draw();
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
            var newZoom = 1+ _stepZoom * delta;
            var scaleCenterXOffset = (float)(_scaleCenterX * newZoom - _scaleCenterX);
            var scaleCenterYOffset = (float)(_scaleCenterY *  newZoom - _scaleCenterY);
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
                        data.Visible = true;
                    }
                    else
                    {
                        data.Visible = false;
                        invisibleCount++;
                    }
                }
                var visibleDatas = _usingMapDatas.Values.Where(p => p.Visible);
                //先画的会被后画的遮盖
                //连线
                if (_currentZoom >= _lineZoom)
                {
                    foreach (var data in visibleDatas)
                    {
                        if (data.LinkTo != null)
                        {
                            foreach (var jumpTo in data.LinkTo)
                            {
                                var linkToData = _usingMapDatas[jumpTo];
                                var ponit0 = new System.Numerics.Vector2((float)data.CenterX, (float)data.CenterY);
                                var ponit1 = new System.Numerics.Vector2((float)linkToData.CenterX, (float)linkToData.CenterY);
                                args.DrawingSession.DrawLine(ponit0, ponit1, _linkColor, 1);
                            }
                        }
                    }
                }
                //星系图形表示
                TurnRGB turnRGB = _isDark ? TurnRGBToDark : TurnRGBToLight;
                foreach (var data in visibleDatas)
                {
                    Windows.UI.Color fColor;
                    if (_currentZoom >= _detailZoom)
                    {
                        fColor = Windows.UI.Color.FromArgb(data.BgColor.A, turnRGB(data.BgColor.R), turnRGB(data.BgColor.G), turnRGB(data.BgColor.B));
                    }
                    else
                    {
                        fColor = data.BgColor;
                    }
                    args.DrawingSession.FillRectangle(data.X, data.Y, data.W, data.H, fColor);
                }
                //画星系详细信息：安全等级、名称等
                if (_currentZoom >= _detailZoom)
                {
                    foreach (var data in visibleDatas)
                    {
                        double drawX = data.X;
                        double drawY = data.Y;
                        var fColor = data.BgColor;
                        var tColor = Windows.UI.Color.FromArgb(50, data.BgColor.R, data.BgColor.G, data.BgColor.B);
                        var borderW = 1f;

                        CanvasTextFormat innerTextFormat = new CanvasTextFormat()
                        {
                            FontSize = 14,
                            HorizontalAlignment = CanvasHorizontalAlignment.Center,
                            VerticalAlignment = CanvasVerticalAlignment.Center
                        };
                        args.DrawingSession.DrawText(data.InnerText, new Windows.Foundation.Rect((float)drawX, (float)drawY, (float)data.W, (float)data.H), fColor, innerTextFormat);
                        args.DrawingSession.DrawRoundedRectangle(new Windows.Foundation.Rect((float)drawX - borderW, (float)drawY - borderW, data.W + borderW * 2, data.H + borderW * 2), borderW * 2, borderW * 2, fColor, borderW * 2);
                        CanvasTextFormat mainTextFormat = new CanvasTextFormat()
                        {
                            FontSize = 12,
                            HorizontalAlignment = CanvasHorizontalAlignment.Center,
                            VerticalAlignment = CanvasVerticalAlignment.Top,
                        };
                        args.DrawingSession.DrawText(data.MainText, new System.Numerics.Vector2((float)(drawX + data.W / 2), (float)(drawY + data.H + 2)), _mainTextColor, mainTextFormat);
                    }
                }
            }
        }
        delegate byte TurnRGB(float s);
        private byte TurnRGBToLight(float s)
        {
            float level = 0.7f;
            return (byte)Math.Round((255 - s) * level + s);
        }
        private byte TurnRGBToDark(float s)
        {
            float level = 0.7f;
            return (byte)Math.Round(s - (s * level));
        }
        public void Draw()
        {
            Draw(1, 0, 0);
        }
        private void Draw(float zoom, float xOffset, float yOffset)
        {
            if(zoom != 0)
            {
                _currentZoom *= zoom;
            }
            else
            {
                zoom = 1;
            }
            
            Debug.WriteLine($"{_currentZoom} {zoom} {xOffset} {yOffset}");
            UpdateData(zoom, xOffset, yOffset);
            _canvasControl.Invalidate();
        }
        private void UpdateData(float zoom, float xOffset, float yOffset)
        {
            foreach (var data in _usingMapDatas.Values)
            {
                data.X = data.X * zoom + xOffset;
                data.Y = data.Y * zoom + yOffset;
            }
            if(zoom != 1 && _currentZoom < 20)//仅平移则无需缩放图形大小
            {
                var whZoom = (float)(_currentZoom / zoom * (zoom * Math.Pow(0.95, _currentZoom)));
                foreach (var data in _usingMapDatas.Values)
                {
                    data.W = data.OriginalW * whZoom;
                    data.H = data.OriginalH * whZoom;
                }
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
            float xScale = (float)(this.ActualWidth / maxX);
            float yScale = xScale;//xy同一个缩放比例
            double xOffset = 0;
            double yOffset = 0;
            if (maxY * yScale > this.ActualHeight)//按x最大比例缩放后若y超出范围，再按y最大比例缩放，即可确保xy都在范围内
            {
                yScale = (float)(this.ActualHeight / maxY);
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
                data.OriginalX = (data.OriginalX * xScale) + (float)xOffset;
                data.OriginalY = (data.OriginalY * yScale) + (float)yOffset;
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
                OriginalX = (float)pos.X;
                OriginalY = (float)pos.Y;
            }
            public int Id { get; set; }
            public float OriginalX { get; set; }
            public float OriginalY { get; set; }
            public float X { get; set; }
            public float Y { get; set; }

            public float OriginalW { get; set; } = 5;
            public float OriginalH { get; set; } = 5;
            public float W { get; set; } = 4;
            public float H { get; set; } = 4;
            public string InnerText { get; set; }
            public string MainText { get; set; }
            public Windows.UI.Color BgColor { get; set; }

            public bool Visible { get; set; } = true;

            public List<int> LinkTo;

            public float CenterX { get => X + W / 2; }
            public float CenterY { get => Y + H / 2; }
        }
        public class MapSystemData : MapData
        {
            public MapSystemData(SolarSystemPosition solarSystemPosition, MapSolarSystem mapSolarSystem):base(solarSystemPosition)
            {
                Id = solarSystemPosition.SolarSystemID;
                SolarSystemPosition = solarSystemPosition;
                MapSolarSystem = mapSolarSystem;
                X = (float)SolarSystemPosition.X;
                Y = (float)SolarSystemPosition.Y;
                MainText = MapSolarSystem.SolarSystemName;
                InnerText = MapSolarSystem.Security.ToString("N1");
                BgColor = Converters.SystemSecurityForegroundConverter.Convert(MapSolarSystem.Security).Color;
                LinkTo = SolarSystemPosition.JumpTo;
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
                Id = regionPosition.RegionId;
                RegionPosition = regionPosition;
                MapRegion = mapRegion;
                X = (float)RegionPosition.X;
                Y = (float)RegionPosition.Y;
                MainText = MapRegion.RegionName;
                BgColor = Colors.Green;
                LinkTo = RegionPosition.JumpTo;
            }
        }
        public enum MapMode
        {
            Region,System
        }
        #endregion
    }
}
