using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.Map;
using TheGuideToTheNewEden.WinUI.Interfaces;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using TheGuideToTheNewEden.Core.Extensions;
using System.Threading.Channels;

namespace TheGuideToTheNewEden.WinUI.Views.IntelOverlapPages
{
    /// <summary>
    /// 默认预警窗口样式
    /// </summary>
    public sealed partial class DefaultIntelOverlapPage : Page, IIntelOverlapPage
    {
        private Core.Models.Map.IntelSolarSystemMap _intelMap;
        private Core.Models.EarlyWarningSetting _setting;
        private readonly SolidColorBrush _defaultBrush = new SolidColorBrush(Colors.DarkGray);
        private readonly SolidColorBrush _homeBrush = new SolidColorBrush(Colors.MediumSeaGreen);
        private readonly SolidColorBrush _intelBrush = new SolidColorBrush(Colors.OrangeRed);
        private readonly SolidColorBrush _downgradeBrush = new SolidColorBrush(Colors.Yellow);
        private readonly SolidColorBrush _defaultLineBrush = new SolidColorBrush(Colors.DarkGray);
        private readonly SolidColorBrush _tempLineBrush = new SolidColorBrush(Color.FromArgb(255, 135, 227, 205));
        private const int _defaultWidth = 8;
        private const int _homeWidth = 12;
        private readonly System.Numerics.Vector3 _intelScale = new System.Numerics.Vector3(1.5f, 1.5f, 1);
        private readonly System.Numerics.Vector3 _defaultScale = new System.Numerics.Vector3(1, 1, 1);
        private Ellipse _lastPointerToEllipse;
        private Dictionary<int, Ellipse> _ellipseDic;
        private BaseWindow _window;
        private List<Core.Models.Map.IntelSolarSystemMap> _allSolarSystem;
        public DefaultIntelOverlapPage()
        {
            this.InitializeComponent();
        }
        void IIntelOverlapPage.Init(BaseWindow window, EarlyWarningSetting setting, IntelSolarSystemMap intelMap)
        {
            _window = window;
            _intelMap = intelMap;
            _setting = setting;
            UpdateUI();
        }
        #region 星系提示
        private void Ellipse_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            TempCanvas.Children.Clear();
            CancelSelected();
        }
        private void Ellipse_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            var ellipse = sender as Ellipse;
            if (_lastPointerToEllipse == ellipse)
            {
                return;
            }
            TempCanvas.Children.Clear();
            CancelSelected();
            var map = ellipse.Tag as Core.Models.Map.IntelSolarSystemMap;
            if (map != null)
            {
                #region 临近星系连线
                if (map.JumpTo.NotNullOrEmpty())
                {
                    foreach (var jump in map.JumpTo)
                    {
                        if (_ellipseDic.TryGetValue(jump, out Ellipse jumpToEllipse))
                        {
                            double startX = ellipse.ActualOffset.X + ellipse.ActualSize.X / 2;
                            double startY = ellipse.ActualOffset.Y + ellipse.ActualSize.Y / 2;
                            double endX = jumpToEllipse.ActualOffset.X + jumpToEllipse.ActualSize.X / 2;
                            double endY = jumpToEllipse.ActualOffset.Y + jumpToEllipse.ActualSize.Y / 2;
                            Line line = new Line()
                            {
                                X1 = startX,
                                Y1 = startY,
                                X2 = endX,
                                Y2 = endY,
                                StrokeThickness = 2,
                                Stroke = _tempLineBrush
                            };
                            TempCanvas.Children.Add(line);
                        }
                    }
                }
                #endregion

                #region 选择星系突出显示
                _lastPointerToEllipse = ellipse;
                ellipse.Scale = new System.Numerics.Vector3(ellipse.Scale.X * 1.5f, ellipse.Scale.Y * 1.5f, 1);
                Canvas.SetLeft(ellipse, ellipse.ActualOffset.X - ellipse.Width * (ellipse.Scale.X - 1) / 2);
                Canvas.SetTop(ellipse, ellipse.ActualOffset.Y - ellipse.Height * (ellipse.Scale.Y - 1) / 2);
                TipTextBlock.Text = $"{map.SolarSystemName} {_intelMap.JumpsOf(map.SolarSystemID)} 跳";
                TipTextBlock.Visibility = Visibility.Visible;
                if (ellipse.ActualOffset.X > this.ActualWidth / 2)
                {
                    TipTextBlock.Translation = new System.Numerics.Vector3(ellipse.ActualOffset.X - (float)TipTextBlock.ActualWidth - 8, ellipse.ActualOffset.Y - 20, 1);

                }
                else
                {
                    TipTextBlock.Translation = new System.Numerics.Vector3(ellipse.ActualOffset.X + 8, ellipse.ActualOffset.Y - 20, 1);
                }
                #endregion
            }
        }
        private void CancelSelected()
        {
            if (_lastPointerToEllipse != null)
            {
                Canvas.SetLeft(_lastPointerToEllipse, _lastPointerToEllipse.ActualOffset.X + _lastPointerToEllipse.Width * (_lastPointerToEllipse.Scale.X - 1) / 2);
                Canvas.SetTop(_lastPointerToEllipse, _lastPointerToEllipse.ActualOffset.Y + _lastPointerToEllipse.Height * (_lastPointerToEllipse.Scale.Y - 1) / 2);
                _lastPointerToEllipse.Scale = new System.Numerics.Vector3(_lastPointerToEllipse.Scale.X / 1.5f, _lastPointerToEllipse.Scale.Y / 1.5f, 1);
            }
            _lastPointerToEllipse = null;
            TipTextBlock.Visibility = Visibility.Collapsed;
        }
        #endregion

        /// <summary>
        /// 正处于预警中的星系
        /// </summary>
        private HashSet<int> _intelings = new HashSet<int>();
        /// <summary>
        /// 已降过级的星系
        /// </summary>
        private HashSet<int> _downgradeds = new HashSet<int>();
        void IIntelOverlapPage.Intel(EarlyWarningContent content)
        {
            InfoTextBlock.Text = $"{content.SolarSystemName}({content.Jumps} Jumps):{content.Content}";
            if (_ellipseDic.TryGetValue(content.SolarSystemId, out var value))
            {
                if (content.IntelType == Core.Enums.IntelChatType.Intel)
                {
                    if (value.Fill != _intelBrush)
                    {
                        value.Fill = _intelBrush;
                        value.Scale = _intelScale;
                        Canvas.SetLeft(value, value.ActualOffset.X - value.Width * (_intelScale.X - 1) / 2);
                        Canvas.SetTop(value, value.ActualOffset.Y - value.Height * (_intelScale.Y - 1) / 2);
                    }
                }
                else if (content.IntelType == Core.Enums.IntelChatType.Clear)
                {
                    if (value.Scale.X != 1)
                    {
                        value.Scale = new System.Numerics.Vector3(1, 1, 1);
                        Canvas.SetLeft(value, value.ActualOffset.X + value.Width * (_intelScale.X - 1) / 2);
                        Canvas.SetTop(value, value.ActualOffset.Y + value.Height * (_intelScale.Y - 1) / 2);
                    }
                    if (content.SolarSystemId == _setting.LocationID)
                    {
                        value.Fill = _homeBrush;
                    }
                    else
                    {
                        value.Fill = _defaultBrush;
                    }
                }
                _intelings.Add(content.SolarSystemId);
                _downgradeds.Remove(content.SolarSystemId);
            }
        }
        void IIntelOverlapPage.Clear(List<int> systemIds)
        {
            foreach (var item in systemIds)
            {
                if (_ellipseDic.TryGetValue(item, out var value))
                {
                    value.Scale = new System.Numerics.Vector3(1, 1, 1);
                    Canvas.SetLeft(value, value.ActualOffset.X + value.Width / 4);
                    Canvas.SetTop(value, value.ActualOffset.Y + value.Height / 4);
                    if (item == _setting.LocationID)
                    {
                        value.Fill = _homeBrush;
                    }
                    else
                    {
                        value.Fill = _defaultBrush;
                    }
                    _intelings.Remove(item);
                    _downgradeds.Remove(item);
                }
            }
        }

        void IIntelOverlapPage.Dispose()
        {
            _ellipseDic.Clear();
            _allSolarSystem.Clear();
        }

        void IIntelOverlapPage.Downgrade(List<int> systemIds)
        {
            foreach (var item in systemIds)
            {
                if (_ellipseDic.TryGetValue(item, out var value))
                {
                    value.Fill = _downgradeBrush;
                    _intelings.Remove(item);
                    _downgradeds.Add(item);
                }
            }
        }

        public void UpdateUI()
        {
            if (_window == null)
            {
                return;
            }
            MapCanvas.Children.Clear();
            LineCanvas.Children.Clear();
            double width = _window.Bounds.Width;
            double height = _window.Bounds.Height - 42;
            _ellipseDic = new Dictionary<int, Ellipse>();
            _allSolarSystem = _intelMap.GetAllSolarSystem();
            foreach (var item in _allSolarSystem)
            {
                Ellipse ellipse = new Ellipse()
                {
                    StrokeThickness = 0,
                    Tag = item
                };
                if (item.SolarSystemID == _intelMap.SolarSystemID)
                {
                    ellipse.Fill = _homeBrush;
                    ellipse.Width = _homeWidth;
                    ellipse.Height = _homeWidth;
                }
                else
                {
                    ellipse.Fill = _defaultBrush;
                    ellipse.Width = _defaultWidth;
                    ellipse.Height = _defaultWidth;
                }
                _ellipseDic.Add(item.SolarSystemID, ellipse);
                MapCanvas.Children.Add(ellipse);
                ellipse.PointerEntered += Ellipse_PointerEntered;
                ellipse.PointerExited += Ellipse_PointerExited;
                Canvas.SetLeft(ellipse, width * item.X);
                Canvas.SetTop(ellipse, height * item.Y);
            }
            //line
            HashSet<string> drawn = new HashSet<string>();
            foreach (var item in _allSolarSystem)
            {
                if (item.Jumps.NotNullOrEmpty())
                {
                    if (_ellipseDic.TryGetValue(item.SolarSystemID, out Ellipse itemEllipse))
                    {
                        foreach (var jumpTo in item.Jumps)
                        {
                            int min = Math.Min(item.SolarSystemID, jumpTo.SolarSystemID) - 30000000;
                            int max = Math.Max(item.SolarSystemID, jumpTo.SolarSystemID) - 30000000;
                            string mark = $"{min}{max}";
                            if (!drawn.Contains(mark))
                            {
                                if (_ellipseDic.TryGetValue(jumpTo.SolarSystemID, out Ellipse jumpToEllipse))
                                {
                                    drawn.Add(mark);
                                    double startX = itemEllipse.ActualOffset.X + itemEllipse.ActualSize.X / 2;
                                    double startY = itemEllipse.ActualOffset.Y + itemEllipse.ActualSize.Y / 2;
                                    double endX = jumpToEllipse.ActualOffset.X + jumpToEllipse.ActualSize.X / 2;
                                    double endY = jumpToEllipse.ActualOffset.Y + jumpToEllipse.ActualSize.Y / 2;
                                    Line line = new Line()
                                    {
                                        X1 = startX,
                                        Y1 = startY,
                                        X2 = endX,
                                        Y2 = endY,
                                        StrokeThickness = 1,
                                        Stroke = _defaultLineBrush
                                    };
                                    LineCanvas.Children.Add(line);
                                }
                            }
                        }
                    }

                }
            }

            //重绘预警中的
            foreach (var item in _downgradeds)
            {
                if (_ellipseDic.TryGetValue(item, out var ellipse))
                {
                    ellipse.Fill = _downgradeBrush;
                    ellipse.Scale = _intelScale;
                    Canvas.SetLeft(ellipse, ellipse.ActualOffset.X - ellipse.Width * (_intelScale.X - 1) / 2);
                    Canvas.SetTop(ellipse, ellipse.ActualOffset.Y - ellipse.Height * (_intelScale.Y - 1) / 2);
                }
            }
            foreach (var item in _intelings)
            {
                if (_ellipseDic.TryGetValue(item, out var ellipse))
                {
                    ellipse.Fill = _intelBrush;
                    ellipse.Scale = _intelScale;
                    Canvas.SetLeft(ellipse, ellipse.ActualOffset.X - ellipse.Width * (_intelScale.X - 1) / 2);
                    Canvas.SetTop(ellipse, ellipse.ActualOffset.Y - ellipse.Height * (_intelScale.Y - 1) / 2);
                }
            }
        }
    }
}
