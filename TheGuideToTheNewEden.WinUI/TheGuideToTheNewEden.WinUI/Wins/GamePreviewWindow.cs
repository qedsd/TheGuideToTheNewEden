using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        //TODO:监控源窗口大小变化自带修改目标窗口显示，目前只有监控目标窗口变化
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
                Height = 32
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
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(content2);
            content3.PointerReleased += Content_PointerReleased;
            content3.PointerWheelChanged += Content3_PointerWheelChanged;
            _appWindow.Closing += AppWindow_Closing;
            this.VisibilityChanged += GamePreviewWindow_VisibilityChanged;
            _appWindow.Changed += AppWindow_Changed;
        }

        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if(args.DidPositionChange || args.DidSizeChange)
            {
                _setting.WinW = _appWindow.ClientSize.Width;
                _setting.WinH = _appWindow.ClientSize.Height;
                _setting.WinX = _appWindow.Position.X;
                _setting.WinY = _appWindow.Position.Y;
                OnSettingChanged?.Invoke(_setting);
            }
        }

        private void GamePreviewWindow_VisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
        {
            if(!args.Visible)
            {
                this.Activate();
            }
        }

        private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            args.Cancel = true;
        }

        private void Content_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ActiveSourceWindow();
        }
        /// <summary>
        /// 显示目标窗口
        /// </summary>
        private void ActiveSourceWindow()
        {
            if (Win32.IsIconic(_sourceHWnd))
            {
                Win32.ShowWindow(_sourceHWnd, 4);
            }
            else
            {
                Win32.ShowWindow(_sourceHWnd, 5);
            }
            Win32.SetForegroundWindow(_sourceHWnd);
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
                try
                {
                    int left = 0;
                    int top = 0;
                    int right = _appWindow.ClientSize.Width;
                    int bottom = _appWindow.ClientSize.Height;
                    var windowRect = new System.Drawing.Rectangle();
                    Win32.GetWindowRect(_sourceHWnd, ref windowRect);
                    var clientRect = new System.Drawing.Rectangle();
                    Win32.GetClientRect(_sourceHWnd, ref clientRect);
                    System.Drawing.Point point = new System.Drawing.Point();
                    Win32.ClientToScreen(_sourceHWnd, ref point);
                    var titleBarHeight = point.Y - windowRect.Top;//去掉标题栏高度
                    WindowCaptureHelper.Rect rcD = new WindowCaptureHelper.Rect(left, top, right, bottom);
                    int widthMargin = point.X - windowRect.Left;//去掉左边白边及右边显示完整
                    WindowCaptureHelper.Rect scS = new WindowCaptureHelper.Rect(widthMargin, titleBarHeight, clientRect.Right + widthMargin, clientRect.Bottom);
                    WindowCaptureHelper.UpdateThumbDestination(_thumbHWnd, rcD, scS);
                }
                catch(Exception ex)
                {

                }
            }
        }
        private void GamePreviewWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            if (_thumbHWnd != IntPtr.Zero)
            {
                UpdateThumbDestination();
            }
        }


        private void Content3_PointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var properties = e.GetCurrentPoint(sender as Grid).Properties;
            if (properties != null)
            {
                if (properties.MouseWheelDelta > 0)
                {
                    _appWindow.Resize(new Windows.Graphics.SizeInt32((int)(_appWindow.Size.Width * 1.05), (int)(_appWindow.Size.Height * 1.05)));
                }
                else
                {
                    _appWindow.Resize(new Windows.Graphics.SizeInt32((int)(_appWindow.Size.Width * 0.95), (int)(_appWindow.Size.Height * 0.95)));
                }
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
            _appWindow.Closing -= AppWindow_Closing;
            this.VisibilityChanged -= GamePreviewWindow_VisibilityChanged;
            Close();
        }

        private void GamePreviewWindow_Closed(object sender, WindowEventArgs args)
        {
            if (_thumbHWnd != IntPtr.Zero)
            {
                WindowCaptureHelper.HideThumb(_thumbHWnd);
            }
        }

        public delegate void SettingChangedDelegate(PreviewItem previewItem);
        public event SettingChangedDelegate OnSettingChanged;
    }
}
