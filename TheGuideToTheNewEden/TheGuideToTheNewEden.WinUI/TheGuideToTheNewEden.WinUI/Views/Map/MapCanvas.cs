﻿using ESI.NET.Models.PlanetaryInteraction;
using ESI.NET.Models.Universe;
using log4net.Core;
using Microsoft.Graphics.Canvas;
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
using TheGuideToTheNewEden.WinUI.Models.Map;
using TheGuideToTheNewEden.WinUI.Views.Map.Drawers;

namespace TheGuideToTheNewEden.WinUI.Views.Map
{
    public class MapCanvas : UserControl
    {
        private CanvasControl _canvasControl;
        private CanvasControl _selectedCanvasControl;
        private CanvasControl _otherCanvasControl;
        /// <summary>
        /// 当前缩放
        /// </summary>
        private float _currentZoom = 1;
        /// <summary>
        /// 当前图形缩放
        /// </summary>
        private float _currentWHZoom = 1;
        private const float _stepZoom = 0.2f;
        private const float _lineZoom = 2;
        private const float _detailZoom = 8;
        private const float _toZoom = 20;
        private const float _maxScaleWHZoom = 9;
        private Windows.UI.Color _mainTextColor;
        private Windows.UI.Color _linkColor;
        private Windows.UI.Color _selectedColor;
        private bool _isDark = false;
        private DispatcherTimer _findDataTimer;
        private Dictionary<int, MapData> _usingMapDatas;
        /// <summary>
        /// 按x从小到大排序的可见数据
        /// </summary>
        private List<MapData> _visbleMapDatas;
        private MapData _selectedData;
        private double _scaleCenterX = 0;
        private double _scaleCenterY = 0;
        private double _lastPressedX = 0;
        private double _lastPressedY = 0;
        private double _lastMovedX = 0;
        private double _lastMovedY = 0;
        private List<MapGraphBase> _otherMapGraphs = new List<MapGraphBase>();

        /// <summary>
        /// 按绘制顺序存放实例
        /// </summary>
        private List<Drawers.IMapDrawer> _mapDrawers = new List<Drawers.IMapDrawer>();
        public MapCanvas()
        {
            _canvasControl = new CanvasControl();
            _canvasControl.CreateResources += CanvasControl_CreateResources;
            _canvasControl.Draw += CanvasControl_Draw;
            _canvasControl.PointerPressed += CanvasControl_PointerPressed;
            _canvasControl.PointerReleased += CanvasControl_PointerReleased;
            _canvasControl.PointerMoved += CanvasControl_PointerMoved;
            _canvasControl.PointerWheelChanged += CanvasControl_PointerWheelChanged;

            _selectedCanvasControl = new CanvasControl();
            _selectedCanvasControl.Draw += SelectedCanvasControl_Draw;

            _otherCanvasControl = new CanvasControl();
            _otherCanvasControl.Draw += OtherCanvasControl_Draw;

            var contentGrid = new Grid();
            contentGrid.Children.Add(_otherCanvasControl);
            contentGrid.Children.Add(_selectedCanvasControl);
            contentGrid.Children.Add(_canvasControl);
            Content = contentGrid;

            SetColor(Services.Settings.ThemeSelectorService.IsDark);
            Services.Settings.ThemeSelectorService.OnChangedTheme += ThemeSelectorService_OnChangedTheme;

            _findDataTimer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _findDataTimer.Tick += FindDataTimer_Tick;
            _findDataTimer.Start();

            InitDrawers();
        }
        private void InitDrawers()
        {
            _mapDrawers.Add(new JumpBridgeDrawer());

            _mapDrawers.ForEach(p =>
            {
                p.DrawRequsted += ((s, e) =>
                {
                    Draw();
                });
            });
        }

        private void OtherCanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if(_otherMapGraphs.Any())
            {
                foreach(var graph in _otherMapGraphs)
                {
                    graph.Draw(args, _usingMapDatas);
                }
            }
        }

        private void SelectedCanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if(_selectedData != null)
            {
                //选中高亮框
                var w = _selectedData.W * 0.2;
                var rect = new Windows.Foundation.Rect(_selectedData.X - w, _selectedData.Y - w, _selectedData.W + w * 2, _selectedData.H + w * 2);
                var color = GetEnableColor(_selectedData.BgColor, _selectedData.Enable);
                args.DrawingSession.FillRectangle(rect, Windows.UI.Color.FromArgb(100, color.R, color.G, color.B));
            }
        }

        
        private void FindDataTimer_Tick(object sender, object e)
        {
            var lastSelectedData = _selectedData;
            _selectedData = null;
            if (_visbleMapDatas != null && _visbleMapDatas.Any() && _lastMovedX != 0 && _lastMovedY != 0)
            {
                //若还处在上一个数据范围内就不用重新找
                if(lastSelectedData != null && _lastMovedX <= (lastSelectedData.X + lastSelectedData.W) && _lastMovedX >= lastSelectedData.X
                    && _lastMovedY <= (lastSelectedData.Y + lastSelectedData.H) && _lastMovedY >= lastSelectedData.Y)
                {
                    _selectedData = lastSelectedData;
                    return;
                }
                double spanX = ActualWidth * _visbleMapDatas[0].W * 2;
                double spanY = ActualHeight * _visbleMapDatas[0].W * 2;
                var posX = _lastMovedX;
                var posY = _lastMovedY;
                float maxX = (float)(posX + spanX);
                float minX = (float)(posX - spanX);
                int low = 0;
                int high = _visbleMapDatas.Count - 1;
                MapData resultData = null;
                bool isTargetData(MapData tryData)
                {
                    if (posX >= tryData.X && posX <= tryData.X + tryData.W
                                && posY >= tryData.Y && posY <= tryData.Y + tryData.H)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                while (low <= high)
                {
                    int mid = (low + high) / 2;
                    if (_visbleMapDatas[mid].X <= maxX && _visbleMapDatas[mid].X >= minX)
                    {
                        //找到当前点位于区间内
                        //向左右两边分别扩展找到所有合适的点
                        //向左
                        for (int i = mid ; i >= 0; i--)
                        {
                            if(isTargetData(_visbleMapDatas[i]))
                            {
                                resultData = _visbleMapDatas[i];
                                break;
                            }
                        }
                        if(resultData == null)
                        {
                            //向右
                            for (int i = mid + 1; i < _visbleMapDatas.Count; i++)
                            {
                                if (isTargetData(_visbleMapDatas[i]))
                                {
                                    resultData = _visbleMapDatas[i];
                                    break;
                                }
                            }
                        }
                        break;
                    }
                    else if (_visbleMapDatas[mid].X > maxX)
                    {
                        high = mid - 1;
                    }
                    else if (_visbleMapDatas[mid].X < minX)
                    {
                        low = mid + 1;
                    }
                }
                _selectedData = resultData;
                PointedSystemChanged?.Invoke(resultData);
            }
            _selectedCanvasControl.Invalidate();
        }

        private void SetColor(bool isDark)
        {
            Windows.UI.Color color = isDark ? Colors.White : Colors.Black;
            _mainTextColor = Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
            _linkColor = isDark ? Windows.UI.Color.FromArgb(50, Colors.Gray.R, Colors.Gray.G, Colors.Gray.B) : Windows.UI.Color.FromArgb(200, Colors.LightGray.R, Colors.LightGray.G, Colors.LightGray.B);
            _selectedColor = isDark ? Windows.UI.Color.FromArgb(255, Colors.GreenYellow.R, Colors.GreenYellow.G, Colors.GreenYellow.B) : Windows.UI.Color.FromArgb(255, Colors.GreenYellow.R, Colors.GreenYellow.G, Colors.GreenYellow.B);
        }
        private void ThemeSelectorService_OnChangedTheme(ElementTheme theme)
        {
            _isDark = Services.Settings.ThemeSelectorService.IsDark;
            SetColor(_isDark);
            Draw();
        }

        private void CanvasControl_CreateResources(CanvasControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
        {
            Draw();
        }
        
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
            if (pointerPoint.Properties.IsLeftButtonPressed)//拖拽移动布局
            {
                _lastMovedX = 0;
                _lastMovedY = 0;
                if (_lastPressedX != 0 && _lastPressedY != 0)
                {
                    Debug.WriteLine($"PointerMoved:({_lastPressedX},{_lastPressedY}) -> ({pointerPoint.Position.X},{pointerPoint.Position.Y})");
                    var xOffset = (float)(pointerPoint.Position.X - _lastPressedX);
                    var yOffset = (float)(pointerPoint.Position.Y - _lastPressedY);
                    _lastPressedX = pointerPoint.Position.X;
                    _lastPressedY = pointerPoint.Position.Y;
                    Draw(0, xOffset, yOffset);
                }
            }
            else//选择星系
            {
                _lastMovedX = pointerPoint.Position.X;
                _lastMovedY = pointerPoint.Position.Y;
            }
        }
        private void CanvasControl_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            _lastPressedX = 0;
            _lastPressedY = 0;
            SelectedSystemChanged?.Invoke(_selectedData);
        }
        private Windows.UI.Color GetEnableColor(Windows.UI.Color targetColor, bool enable)
        {
            return enable ? targetColor : Windows.UI.Color.FromArgb(targetColor.A, Colors.LightGray.R, Colors.LightGray.G, Colors.LightGray.B);
        }
        private const double _visibleScale = 0.5;
        private void CanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if(_usingMapDatas != null)
            {
                int invisibleCount = 0;
                foreach (var data in _usingMapDatas.Values)
                {
                    double wExt = sender.ActualWidth * _visibleScale;
                    double hExt = sender.ActualHeight * _visibleScale;
                    double drawX = data.X;
                    double drawY = data.Y;
                    if (drawX <= sender.ActualWidth + wExt && drawY <= sender.ActualHeight + hExt && drawX >= -wExt && drawY >= -hExt)
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
                _visbleMapDatas = visibleDatas.OrderBy(p=>p.X).ToList();
                foreach(var drawer in _mapDrawers)
                {
                    drawer.Draw(args, _usingMapDatas, visibleDatas);
                }
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
                //if (_currentZoom >= 19)//画星系详细信息：安全等级、名称等
                {
                    foreach (var data in visibleDatas)
                    {
                        double drawX = data.X;
                        double drawY = data.Y;
                        var fColor = data.BgColor;
                        var tColor = Windows.UI.Color.FromArgb(50, data.BgColor.R, data.BgColor.G, data.BgColor.B);

                        //内图形
                        args.DrawingSession.FillRectangle(data.X, data.Y, data.W, data.H, GetEnableColor(fColor,data.Enable));
                        //内文字
                        if (_currentZoom >= 24)
                        {
                            CanvasTextFormat innerTextFormat = new CanvasTextFormat()
                            {
                                FontSize = 12,
                                HorizontalAlignment = CanvasHorizontalAlignment.Center,
                                VerticalAlignment = CanvasVerticalAlignment.Center,
                                //Options = CanvasDrawTextOptions.Clip
                            };
                            args.DrawingSession.DrawText(data.InnerText, new Windows.Foundation.Rect((float)drawX, (float)drawY, (float)data.W, (float)data.H), GetEnableColor(Colors.White, data.Enable), innerTextFormat);
                        }
                        if (_currentZoom >= 24)
                        {
                            //外图形
                            var borderWidth = data.W * 0.2f;
                            args.DrawingSession.FillRectangle(new Windows.Foundation.Rect((float)drawX - borderWidth, (float)drawY - borderWidth, data.W + borderWidth * 2, data.H + borderWidth * 2), GetEnableColor(tColor, data.Enable));
                        }
                        if (_currentZoom >= 13)
                        {
                            //外文字
                            CanvasTextFormat mainTextFormat = new CanvasTextFormat()
                            {
                                FontSize = 12,
                                HorizontalAlignment = CanvasHorizontalAlignment.Center,
                                VerticalAlignment = CanvasVerticalAlignment.Top,
                            };
                            args.DrawingSession.DrawText(data.MainText, new System.Numerics.Vector2((float)(drawX + data.W / 2), (float)(drawY + data.H + 4)), GetEnableColor(_mainTextColor, data.Enable), mainTextFormat);
                        }
                    }
                }
                //else//只画大概的内图形
                //{
                //    foreach (var data in visibleDatas)
                //    {
                //        //只有内图形
                //        args.DrawingSession.FillRectangle(data.X, data.Y, data.W, data.H, GetEnableColor(data.BgColor, data.Enable));
                //    }
                //}
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
            if(_usingMapDatas != null && _usingMapDatas.Any())
            {
                UpdateData(zoom, xOffset, yOffset);
                _canvasControl.Invalidate();
                _otherCanvasControl.Invalidate();
            }
        }
        private void UpdateData(float zoom, float xOffset, float yOffset)
        {
            foreach (var data in _usingMapDatas.Values)
            {
                data.X = data.X * zoom + xOffset;
                data.Y = data.Y * zoom + yOffset;
            }
            if(zoom != 1)//仅平移则无需缩放图形大小
            {
                var z = _currentZoom > 26 ? 26 : _currentZoom;
                var whZoom = 1+ (z - 1) / 4;
                foreach (var data in _usingMapDatas.Values)
                {
                    data.W = data.OriginalW * whZoom;
                    data.H = data.OriginalH * whZoom;
                }
            }
        }

        #region public
        public void Draw()
        {
            Draw(1, 0, 0);
        }
        public void SetData(Dictionary<int, MapData> datas)
        {
            _usingMapDatas = datas;
            _currentZoom = 1;
            _scaleCenterX = 0;
            _scaleCenterY = 0;
            _lastPressedX = 0;
            _lastPressedY = 0;
            _lastMovedX = 0;
            _lastMovedY = 0;
        }
        public void ToSystem(int id)
        {
            if(_usingMapDatas.TryGetValue(id, out var data))
            {
                float centerX = (float)ActualWidth / 2;
                float centerY = (float)ActualHeight / 2;
                float offsetX, offsetY, zoom;
                if(_currentZoom < _toZoom)
                {
                    zoom = _toZoom / _currentZoom;
                    offsetX = centerX - data.X * zoom;
                    offsetY = centerY - data.Y * zoom;
                }
                else
                {
                    zoom = 1;
                    offsetX = centerX - data.X;
                    offsetY = centerY - data.Y;
                }
                Draw(zoom, offsetX, offsetY);
            }
        }
        public void ToSystem(List<int> ids)
        {
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            foreach(var id in ids)
            {
                if (_usingMapDatas.TryGetValue(id, out var data))
                {
                    if(data.X < minX)
                    {
                        minX = data.X;
                    }
                    if(data.X > maxX)
                    {
                        maxX = data.X;
                    }
                    if (data.Y < minY)
                    {
                        minY = data.Y;
                    }
                    if (data.Y > maxY)
                    {
                        maxY = data.Y;
                    }
                }
            }
            if(minX < float.MaxValue)
            {
                //适当将XY扩大以便显示完全
                minX -= (float)(ActualWidth * 0.05);
                maxX += (float)(ActualWidth * 0.05);
                minY -= (float)(ActualHeight * 0.05);
                maxY += (float)(ActualHeight * 0.05);
                //按最大最小x/y差值缩小到显示范围宽度并以此缩放当作缩放
                float xScale = (float)this.ActualWidth / (maxX - minX);
                float yScale = (float)this.ActualHeight / (maxY - minY);
                float scale = Math.Abs(xScale - 1) > Math.Abs(yScale - 1) ? xScale : yScale;
                float xOffset = -minX * scale;
                float yOffset = -minY * scale;
                Draw(scale, xOffset, yOffset);
            }
        }
        public void AddMapGraph(List<MapGraphBase> mapGraphs)
        {
            _otherMapGraphs.AddRange(mapGraphs);
            _otherCanvasControl.Invalidate();
        }
        public void ClearMapGraph()
        {
            _otherMapGraphs.Clear();
            _otherCanvasControl.Invalidate();
        }
        private Dictionary<int, bool> _temporaryDisableData = new Dictionary<int, bool>();
        public void TemporaryEnableData(List<int> datas)
        {
            _temporaryDisableData.Clear();
            foreach(var data in _usingMapDatas)
            {
                _temporaryDisableData.Add(data.Key, data.Value.Enable);
                data.Value.Enable = false;
            }

            foreach (var data in datas)
            {
                _usingMapDatas[data].Enable = true;
            }
            Draw();
        }
        public void RemoveTemporary()
        {
            if(_temporaryDisableData.Any())
            {
                foreach (var data in _temporaryDisableData)
                {
                    _usingMapDatas[data.Key].Enable = data.Value;
                }
            }
            Draw();
        }
        public void Dispose()
        {
            _canvasControl.RemoveFromVisualTree();
            _canvasControl = null;
            _selectedCanvasControl.RemoveFromVisualTree();
            _selectedCanvasControl = null;
            _otherCanvasControl.RemoveFromVisualTree();
            _otherCanvasControl = null;
            _findDataTimer.Stop();
            _findDataTimer = null;
            Services.Settings.ThemeSelectorService.OnChangedTheme -= ThemeSelectorService_OnChangedTheme;
        }
        public delegate void SelectedSystemChangedEventHandel(MapData mapData);
        private SelectedSystemChangedEventHandel PointedSystemChanged;
        public event SelectedSystemChangedEventHandel OnPointedSystemChanged
        {
            add
            {
                PointedSystemChanged += value;
            }
            remove
            {
                PointedSystemChanged -= value;
            }
        }
        private SelectedSystemChangedEventHandel SelectedSystemChanged;
        public event SelectedSystemChangedEventHandel OnSelectedSystemChanged
        {
            add
            {
                SelectedSystemChanged += value;
            }
            remove
            {
                SelectedSystemChanged -= value;
            }
        }

        #endregion

        public enum MapGraphType
        {
            Circle, Line
        }
        public abstract class MapGraphBase
        {
            public MapGraphType GraphType { get; set; }
            public abstract void Draw(CanvasDrawEventArgs args, Dictionary<int, MapData> datas);
        }
        public class CircleMapGraph: MapGraphBase
        {
            public CircleMapGraph()
            {
                GraphType = MapGraphType.Circle;
            }
            public int CenterDataId { get; set; }
            public List<int> CoverDataIds { get; set; }
            public Windows.UI.Color Color { get; set; } = Windows.UI.Color.FromArgb(100, Colors.White.R, Colors.White.G, Colors.White.B);
            public float Margin { get; set; } = 1;

            public override void Draw(CanvasDrawEventArgs args, Dictionary<int, MapData> datas)
            {
                var centerData = datas[CenterDataId];
                float r = centerData.W + Margin;
                MapData farthestData = centerData;
                if (CoverDataIds != null && CoverDataIds.Any())
                {
                    foreach (var coverDataId in CoverDataIds)
                    {
                        var data = datas[coverDataId];
                        var curR = Math.Sqrt(Math.Pow(centerData.X - data.X, 2) + Math.Pow(centerData.Y - data.Y, 2));
                        if(curR > r)
                        {
                            r = (float)curR;
                            farthestData = data;
                        }
                    }
                }
                args.DrawingSession.FillCircle(centerData.CenterX, centerData.CenterY, r, Windows.UI.Color.FromArgb(100, centerData.BgColor.R, centerData.BgColor.G, centerData.BgColor.B));
            }
        }
        public class LineMapGraph : MapGraphBase
        {
            public LineMapGraph()
            {
                GraphType = MapGraphType.Line;
            }
            public int Data1Id { get; set; }
            public int Data2Id { get; set; }
            public Windows.UI.Color Color { get; set; } = Windows.UI.Color.FromArgb(255, Colors.LightSeaGreen.R, Colors.LightSeaGreen.G, Colors.LightSeaGreen.B);
            public float StrokeWidth { get; set; } = 3;
            public override void Draw(CanvasDrawEventArgs args, Dictionary<int, MapData> datas)
            {
                var data1 = datas[Data1Id];
                var data2 = datas[Data2Id];
                args.DrawingSession.DrawLine(data1.CenterX, data1.CenterY, data2.CenterX, data2.CenterY, Color, StrokeWidth);
            }
        }
    }
}
