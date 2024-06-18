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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Timers;
using TheGuideToTheNewEden.WinUI.Controls;
using TheGuideToTheNewEden.WinUI.Converters;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static TheGuideToTheNewEden.WinUI.Extensions.PageExtension;

namespace TheGuideToTheNewEden.WinUI.Views.Map
{
    public sealed partial class MapPage : Page
    {
        private BaseWindow _window;
        private DispatcherTimer _timer;
        public MapPage()
        {
            this.InitializeComponent();
            Loaded += MapPage_Loaded;
            SizeChanged += MapPage_SizeChanged;
            _timer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromMilliseconds(100),
            };
            //_timer.Tick += Timer_Tick;
        }

        private async void MapPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //await UpdateMap();
        }

        private async void MapPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MapPage_Loaded;
            _window = this.GetBaseWindow();
        }

        private void MapCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            
        }

        private void MapSystemSelector_OnSelectedItemChanged(Core.DBModels.MapSolarSystem selectedItem)
        {
            MapCanvas.ToSystem(selectedItem.SolarSystemID);
        }

        //private async Task UpdateMap()
        //{
        //    //从星系实际最大最小xy坐标比例算得长宽比为1：2.265
        //    //x : y = 1 : 1.1
        //    MapMainCanvas.Children.Clear();
        //    //double width = (MapMainCanvas.ActualWidth == 0 ? _window.Bounds.Width : MapMainCanvas.ActualWidth) - 16;
        //    //double height = MapMainCanvas.ActualHeight == 0 ? _window.Bounds.Height - 42 - 32 : MapMainCanvas.ActualHeight;
        //    double width = MapMainCanvas.ActualWidth;
        //    double height = MapMainCanvas.ActualHeight;
        //    var systems = await Core.EVEHelpers.SolarSystemPosHelper.GetAllAsync();
        //    foreach (var system in systems)
        //    {
        //        //MapSystemControl mapSystemControl = new MapSystemControl();
        //        //Ellipse mapSystemControl = new Ellipse()
        //        //{
        //        //    Width = 6,
        //        //    Height = 6,
        //        //    Fill = new SolidColorBrush(Microsoft.UI.Colors.DarkGray)
        //        //};
        //        Rectangle mapSystemControl = new Rectangle()
        //        {
        //            Width = 6,
        //            Height = 6,
        //            Fill = new SolidColorBrush(Microsoft.UI.Colors.DarkGray)
        //        };
        //        MapMainCanvas.Children.Add(mapSystemControl);
        //        Canvas.SetLeft(mapSystemControl, width * system.X);
        //        Canvas.SetTop(mapSystemControl, height * system.Y);
        //    }
        //}

        //private void MapPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        //{
        //    var point = e.GetCurrentPoint(sender as UIElement);
        //    var delta = point.Properties.MouseWheelDelta > 0 ? -1f : 1f;
        //    MapScrollView.ZoomTo(MapScrollView.ZoomFactor + delta, new System.Numerics.Vector2((float)(point.Position.X), (float)(point.Position.Y)),new ScrollingZoomOptions(ScrollingAnimationMode.Enabled));
        //}

        //private double _x;
        //private double _y;
        //private PointerPoint _pointerPoint;
        //private void MapPointerMoved(object sender, PointerRoutedEventArgs e)
        //{
        //    _pointerPoint = e.GetCurrentPoint(MapScrollView.Content);
        //    //var pointer = e.GetCurrentPoint(MapScrollView.Content);
        //    //if(pointer.Properties.IsLeftButtonPressed)
        //    //{
        //    //    var xSpan = pointer.Position.X - _x;
        //    //    var ySpan = pointer.Position.Y - _y;
        //    //    Debug.WriteLine($"{_x} {_y} {xSpan} {ySpan} {MapScrollView.HorizontalOffset} {MapScrollView.VerticalOffset} {(MapScrollView.Content as FrameworkElement).ActualWidth}");
        //    //    MapScrollView.ScrollTo(MapScrollView.HorizontalOffset + xSpan, MapScrollView.VerticalOffset + ySpan);
        //    //    _x = pointer.Position.X;
        //    //    _y = pointer.Position.Y;
        //    //}
        //}

        //private void MapPointerPressed(object sender, PointerRoutedEventArgs e)
        //{
        //    var pointer = e.GetCurrentPoint(MapScrollView.Content);
        //    if (pointer.Properties.IsLeftButtonPressed)
        //    {
        //        _x = pointer.Position.X;
        //        _y = pointer.Position.Y;
        //    }
        //    _timer.Start();
        //}

        //private void Timer_Tick(object sender, object e)
        //{
        //    Debug.WriteLine($"{_pointerPoint.Position.X} {_pointerPoint.Position.Y} {_x - _pointerPoint.Position.X}");
        //    //var xP = (_x - _pointerPoint.Position.X) / MapScrollView.ActualWidth;
        //    //var yP = (_y - _pointerPoint.Position.Y) / MapScrollView.ActualHeight;
        //    MapScrollView.ScrollTo(MapScrollView.HorizontalOffset + (_x - _pointerPoint.Position.X) * MapScrollView.ZoomFactor, MapScrollView.VerticalOffset + (_y - _pointerPoint.Position.Y) * MapScrollView.ZoomFactor);
        //    _x = _pointerPoint.Position.X;
        //    _y = _pointerPoint.Position.Y;
        //}
        //private void MapPointerReleased(object sender, PointerRoutedEventArgs e)
        //{
        //    _timer.Stop();
        //}
    }
}
