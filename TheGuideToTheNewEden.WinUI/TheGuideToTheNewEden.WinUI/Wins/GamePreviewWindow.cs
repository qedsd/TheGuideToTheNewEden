using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.WinUI.Common;
using TheGuideToTheNewEden.WinUI.Helpers;
using WinUIEx;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class GamePreviewWindow: Window
    {
        private PreviewItem _setting;
        private AppWindow _appWindow;
        private IntPtr _windowHandle = IntPtr.Zero;
        public GamePreviewWindow(PreviewItem setting)
        {
            _setting = setting;
            
            var presenter = Helpers.WindowHelper.GetOverlappedPresenter(this);
            if (!_setting.ShowTitleBar)
            {
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsResizable = false;
                presenter.SetBorderAndTitleBar(false, false);
            }
            //else
            //{
            //    presenter.IsMaximizable = false;
            //    presenter.IsMinimizable = false;
            //    presenter.SetBorderAndTitleBar(false, false);
            //}
            
            presenter.IsAlwaysOnTop = true;
            TransparentWindowHelper.TransparentWindow(this, _setting.OverlapOpacity);
            _windowHandle = Helpers.WindowHelper.GetWindowHandle(this);
            _appWindow = Helpers.WindowHelper.GetAppWindow(this);
            _appWindow.IsShownInSwitchers = false;
            _appWindow.Resize(new Windows.Graphics.SizeInt32(_setting.WinW, _setting.WinH));
            if (_setting.WinX != -1 && _setting.WinY != -1)
            {
                Helpers.WindowHelper.MoveToScreen(this, _setting.WinX, _setting.WinY);
            }
            var grid = new Microsoft.UI.Xaml.Controls.Grid()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
               Background = new SolidColorBrush(Colors.AliceBlue)
            };
            var content2 = new Microsoft.UI.Xaml.Controls.Grid()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            var content3 = new Microsoft.UI.Xaml.Controls.Grid()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = new SolidColorBrush(Colors.AliceBlue),
            };
            grid.Children.Add(content2);
            grid.Children.Add(content3);
            Content = grid;
            content3.PointerWheelChanged += Content_PointerWheelChanged;
            ExtendsContentIntoTitleBar = true;
            //SetTitleBar(content2);
            //this.CoreWindow.PointerEntered += CoreWindow_PointerEntered;
            //WindowManager.Get(this).IsMinimizable = false;
            //WindowManager.Get(this).IsMaximizable = false;
            content3.PreviewKeyDown += Content3_PreviewKeyDown;
            content3.PointerPressed += Content_PointerPressed;
            content3.PointerReleased += Content_PointerReleased;
            content3.PointerMoved += Content_PointerMoved;
            _appWindow.Changed += _appWindow_Changed;
            _appWindow.Closing += _appWindow_Closing;
        }

        private void CoreWindow_PointerEntered(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.PointerEventArgs args)
        {
            
        }

        private void Content3_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            
        }

        private void Content_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            movedMouse = false;
        }

        private void _appWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            args.Cancel = true;
        }

        private void _appWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if(args.DidVisibilityChange)
            {
                
            }
        }

        private void Content_PointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var properties = e.GetCurrentPoint(this.Content).Properties;
            if (properties != null)
            {
                if(properties.MouseWheelDelta > 0)
                {
                    _appWindow.Resize(new Windows.Graphics.SizeInt32((int)(_appWindow.Size.Width * 1.1), (int)(_appWindow.Size.Height * 1.1)));
                }
                else
                {
                    _appWindow.Resize(new Windows.Graphics.SizeInt32((int)(_appWindow.Size.Width * 0.9), (int)(_appWindow.Size.Height * 0.9)));
                }
            }
        }

        private void Content_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            _lastPointerPoint.X = int.MinValue;
            _lastPointerPoint.Y = int.MinValue;
            Console.WriteLine(movedMouse);
            if (movedMouse)
            {
               
            }
            else
            {
                Win32.ShowWindow(_sourceHWnd,1);
            }
        }

        private System.Drawing.Point _lastPointerPoint = new System.Drawing.Point(int.MinValue, int.MinValue);
        private bool movedMouse = false;
        private void Content_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            movedMouse = true;
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
            {
                var properties = e.GetCurrentPoint(this.Content).Properties;
                if (properties.IsRightButtonPressed)
                {
                    var pos = e.GetCurrentPoint(this.Content);
                    if (_lastPointerPoint.X != int.MinValue)
                    {
                        var moveXP = (pos.Position.X - _lastPointerPoint.X) / Content.ActualSize.X;
                        var moveYP = (pos.Position.Y - _lastPointerPoint.Y) / Content.ActualSize.X;
                        int moveX = (int)(moveXP * _appWindow.ClientSize.Width);
                        int moveY = (int)(moveYP * _appWindow.ClientSize.Height);
                        if (moveX != 0 || moveY != 0)
                        {
                            Helpers.WindowHelper.MoveToScreen(this, (int)(_appWindow.Position.X + moveX), (int)(_appWindow.Position.Y + moveY));
                            _lastPointerPoint.X = (int)pos.Position.X;
                            _lastPointerPoint.Y = (int)pos.Position.Y;
                        }
                    }
                    else
                    {
                        _lastPointerPoint.X = (int)pos.Position.X;
                        _lastPointerPoint.Y = (int)pos.Position.Y;
                    }
                }
            }
        }

        private IntPtr _sourceHWnd = IntPtr.Zero;
        private IntPtr _thumbHWnd = IntPtr.Zero;
        public void Start(IntPtr sourceHWnd)
        {
            _sourceHWnd = sourceHWnd;
            _thumbHWnd = WindowCaptureHelper.Show(_windowHandle, sourceHWnd);
            SizeChanged += GamePreviewWindow_SizeChanged;
            Closed += GamePreviewWindow_Closed;
            UpdateThumbDestination();
            this.Activate();
        }


        private void UpdateThumbDestination()
        {
            if (_thumbHWnd != IntPtr.Zero)
            {
                int left = 0;
                int top = 20;
                int right = _appWindow.ClientSize.Width;
                int bottom = _appWindow.ClientSize.Height;
                WindowCaptureHelper.UpdateThumbDestination(_thumbHWnd, new WindowCaptureHelper.Rect(left, top, right, bottom));
            }
        }
        private void GamePreviewWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            if(_thumbHWnd != IntPtr.Zero)
            {
                UpdateThumbDestination();
            }
        }
        public void Stop()
        {
            if (_thumbHWnd != IntPtr.Zero)
            {
                WindowCaptureHelper.HideThumb(_thumbHWnd);
                _thumbHWnd = IntPtr.Zero;
            }
            SizeChanged -= GamePreviewWindow_SizeChanged;
            _appWindow.Closing -= _appWindow_Closing;
            Close();
        }

        private void GamePreviewWindow_Closed(object sender, WindowEventArgs args)
        {
            if (_thumbHWnd != IntPtr.Zero)
            {
                WindowCaptureHelper.HideThumb(_thumbHWnd);
            }
        }
    }
}
