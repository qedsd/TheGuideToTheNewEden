using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.WinUI.Common;
using Microsoft.UI.Windowing;
using System.Diagnostics;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    public class SelecteCaptureAreaWindow : BaseWindow
    {
        private IntPtr _sourceHWnd = IntPtr.Zero;
        private IntPtr _thumbHWnd = IntPtr.Zero;
        private readonly IntPtr _windowHandle = IntPtr.Zero;
        private readonly AppWindow _appWindow;
        private Image _image = new Image();
        private ImageCropper _imageCropper = new ImageCropper();
        private DispatcherTimer dispatcherTimer;
        public SelecteCaptureAreaWindow(IntPtr sourceHWnd)
        {
            this.InitializeComponent();
            SetHeadText("选择本地声望区域");
            _sourceHWnd = sourceHWnd;
            _windowHandle = Helpers.WindowHelper.GetWindowHandle(this);
            MainContent = _imageCropper;
            _appWindow = Helpers.WindowHelper.GetAppWindow(this);
            Activated += SelecteCaptureAreaWindow_Activated;
            SizeChanged += SelecteCaptureAreaWindow_SizeChanged;
            dispatcherTimer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromMilliseconds(200),
            };
            dispatcherTimer.Tick += DispatcherTimer_Tick;
            dispatcherTimer.Start();
            Closed += SelecteCaptureAreaWindow_Closed;
        }

        private async void SelecteCaptureAreaWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            await Task.Delay(100);
            UpdateThumbDestination();
        }

        private void SelecteCaptureAreaWindow_Closed(object sender, WindowEventArgs args)
        {
            dispatcherTimer.Tick -= DispatcherTimer_Tick;
            SizeChanged -= SelecteCaptureAreaWindow_SizeChanged;
            dispatcherTimer.Stop();
        }

        private void DispatcherTimer_Tick(object sender, object e)
        {
            if(_imageCropper.Source != null)
            {
                double xScale = (double)_sourceClientRect.Width / _imageCropper.Source.PixelWidth;
                double yScale = (double)_sourceClientRect.Height / _imageCropper.Source.PixelHeight;
                Rect actullyRect = new Rect();
                actullyRect.X = xScale * _imageCropper.CroppedRegion.X;
                actullyRect.Y = yScale * _imageCropper.CroppedRegion.Y;
                actullyRect.Width = xScale * _imageCropper.CroppedRegion.Width;
                actullyRect.Height = xScale * _imageCropper.CroppedRegion.Height;
                CroppedRegionChanged?.Invoke(_imageCropper.CroppedRegion);
            }
        }

        private void SelecteCaptureAreaWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            if (_thumbHWnd != IntPtr.Zero)
            {
                //UpdateThumbDestination();
            }
        }
        private System.Drawing.Rectangle _sourceClientRect = new System.Drawing.Rectangle();
        private void UpdateThumbDestination()
        {
            _thumbHWnd = WindowCaptureHelper.Show(_windowHandle, _sourceHWnd);
            if (_thumbHWnd != IntPtr.Zero)
            {
                try
                {
                    int left = 0;
                    int top = 0;
                    int right = _appWindow.ClientSize.Width;
                    int bottom = _appWindow.ClientSize.Height;
                    Win32.GetClientRect(_sourceHWnd, ref _sourceClientRect);//源窗口显示区域分辨率大小
                    //目标窗口显示区域
                    WindowCaptureHelper.Rect rcD = new WindowCaptureHelper.Rect(left, top, right, bottom);
                    WindowCaptureHelper.UpdateThumbDestination2(_thumbHWnd, rcD);
                    System.Drawing.Point point = new System.Drawing.Point();
                    Win32.ClientToScreen(_windowHandle, ref point);
                    //不用_appWindow.Position，有偏差
                    var img = Helpers.WindowCaptureHelper.GetScreenshot(point.X, point.Y, _appWindow.ClientSize.Width, _appWindow.ClientSize.Height);
                    WindowCaptureHelper.HideThumb(_thumbHWnd);
                    _imageCropper.Source = Helpers.ImageHelper.ImageConvertToWriteableBitmap(img);
                    img.Dispose();
                    //捕获到的图像是根据此窗口调整后的大小，不是源窗口实际大小
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }
        }

        public delegate void CroppedRegionChangedDelegate(Rect rect);
        public event CroppedRegionChangedDelegate CroppedRegionChanged;
    }
}
