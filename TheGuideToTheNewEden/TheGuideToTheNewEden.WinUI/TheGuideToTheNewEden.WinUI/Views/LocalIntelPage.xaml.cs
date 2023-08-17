using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WinUI.Common;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.ViewModels;
using TheGuideToTheNewEden.WinUI.Wins;
using Windows.Foundation.Collections;
using Windows.UI;
using WinUIEx;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class LocalIntelPage : Page
    {
        private BaseWindow _window;
        private Microsoft.UI.Windowing.AppWindow _appWindow;
        private IntPtr _windowHandle;
        public LocalIntelPage()
        {
            this.InitializeComponent();
            Loaded += LocalIntelPage_Loaded;
            Unloaded += LocalIntelPage_Unloaded;
            Loaded += LocalIntelPage_Loaded1;
            PreviewGrid.SizeChanged += PreviewGrid_SizeChanged;
        }

        private void LocalIntelPage_Loaded1(object sender, RoutedEventArgs e)
        {
            if (_thumb != IntPtr.Zero && VM.SelectedProcess != null)
            {
                _thumb = WindowCaptureHelper.Show(_windowHandle, VM.SelectedProcess.MainWindowHandle);
                UpdateThumbDestination();
            }
        }

        private void LocalIntelPage_Unloaded(object sender, RoutedEventArgs e)
        {
            WindowCaptureHelper.HideThumb(_thumb);
        }

        private void PreviewGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateThumbDestination();
        }

        private void LocalIntelPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= LocalIntelPage_Loaded;
            _window = WindowHelper.GetWindowForElement(this) as BaseWindow;
            _appWindow = Helpers.WindowHelper.GetAppWindow(_window);
            _windowHandle = WindowHelper.GetWindowHandle(_window);
        }

        private void Button_RemoveStanding_Click(object sender, RoutedEventArgs e)
        {
            VM.RemoveStanding((sender as Button).DataContext as LocalIntelStandingSetting);
        }

        private void GetPreviewGridPos(out int x, out int y, out int w, out int h)
        {
            var showWP = (PreviewParentGrid.ActualWidth + PreviewParentGrid.Margin.Left + PreviewParentGrid.Margin.Right)
                          / 
                         (MainGrid.ActualWidth + MainGrid.Margin.Left + MainGrid.Margin.Right);
            w = (int)(showWP * _appWindow.ClientSize.Width);
            y = (int)(_window.TitleBarHeight * Helpers.WindowHelper.GetDpiScale(_window) + MainGrid.Margin.Top);
            x = (int)((1- showWP) * _appWindow.ClientSize.Width);
            var showHP = PreviewGrid.ActualHeight / MainGrid.ActualHeight;
            h = (int)(showHP * _appWindow.ClientSize.Height - y);
        }
        private IntPtr _thumb;
        private void ProcessList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(_thumb != IntPtr.Zero)
            {
                WindowCaptureHelper.HideThumb(_thumb);
            }
            if(e.AddedItems?.Count > 0)
            {
                _thumb = WindowCaptureHelper.Show(_windowHandle, VM.SelectedProcess.MainWindowHandle);
                UpdateThumbDestination();
            }
        }
        private void UpdateThumbDestination()
        {
            if (_thumb != IntPtr.Zero)
            {
                GetPreviewGridPos(out int x, out int y, out int w, out int h);
                GetCenterThumbRect(x, y, (int)(w / 3.0), h, out WindowCaptureHelper.Rect rcD);
                WindowCaptureHelper.UpdateThumbDestination2(_thumb, rcD);
                var shotImg = GetScreenshot(rcD);
                if(shotImg!= null)
                {
                    var edgeMat = IntelImageHelper.GetEdge(System.Drawing.Image.FromFile("D:\\System\\图片\\1 (2).png"));
                    var writeableBitmap = ImageHelper.MemoryStreamConvertToWriteableBitmap(shotImg.Width, shotImg.Height, edgeMat.ToMemoryStream());
                    Image1.Source = writeableBitmap;
                    OpenCvSharp.Point[][] points;
                    var mat2 = IntelImageHelper.CalStandingRects(edgeMat, out points);
                    var writeableBitmap2 = ImageHelper.MemoryStreamConvertToWriteableBitmap(mat2.Width, mat2.Height, mat2.ToMemoryStream());
                    Image2.Source = writeableBitmap2;
                    mat2.Dispose();
                    shotImg.Dispose();
                    edgeMat.Dispose();
                }
            }
        }
        private void GetCenterThumbRect(int x, int y, int w, int h, out WindowCaptureHelper.Rect rect)
        {
            int sourceCenterY = y + h / 2;
            int sourceCenterX = x + w / 2;
            rect = new WindowCaptureHelper.Rect();
            var thumbSize = WindowCaptureHelper.GetThumbSourceSize(_thumb);
            if(thumbSize.x<w && thumbSize.y<h)
            {
                //保持原样，只居中
                rect.Left = sourceCenterX - thumbSize.x / 2;
                rect.Right = rect.Left + thumbSize.x;
                rect.Top = sourceCenterY - thumbSize.y / 2;
                rect.Bottom = rect.Top + thumbSize.y;
            }
            else if(thumbSize.x / (double)w > thumbSize.y / (double)h)
            {
                //w最大化，h跟随缩放
                int newW = w;
                int newH = thumbSize.y * newW / thumbSize.x;
                rect.Left = x;
                rect.Right = x + newW;
                rect.Top = sourceCenterY - newH / 2;
                rect.Bottom = rect.Top + newH;
            }
            else
            {
                //h最大化，w跟随缩放
                int newH = h;
                int newW = thumbSize.x * newH / thumbSize.y;
                rect.Left = sourceCenterX - newW / 2;
                rect.Right = rect.Left + newW;
                rect.Top = y;
                rect.Bottom = y + h;
            }
        }
        private void GetCenterThumbRect2(int x, int y, int w, int h, out WindowCaptureHelper.Rect rect)
        {
            rect = new WindowCaptureHelper.Rect();
            var thumbSize = WindowCaptureHelper.GetThumbSourceSize(_thumb);
            if (thumbSize.x > thumbSize.y)//横向
            {
                //x最大化，y调整到居中
                rect.Right = x + w;
                rect.Left = x;
                AutoAdjustY(rect, thumbSize.x, thumbSize.y, y, h);
            }
            else//竖向
            {
                //y最大化，x调整到居中
                rect.Top = y;
                rect.Bottom = y + h;
                var p = (double)h / thumbSize.y;
                var newW = p * thumbSize.x;
                if(newW <= w)//缩放后的宽度放得下
                {
                    var centerX = x + w / 2;
                    rect.Left = (int)(centerX - newW / 2);
                    rect.Right = rect.Left + (int)newW;
                }
                else//缩放后的宽度还放不下，需要调整y
                {
                    rect.Left = x;
                    rect.Right = x + w;
                    AutoAdjustY(rect, thumbSize.x, thumbSize.y, y, h);
                }
            }
        }
        private void AutoAdjustY(WindowCaptureHelper.Rect rect, int thumbSizeX, int thumbSizeY, int miniY, int maxH)
        {
            ///等比例缩放h
            var p = (double)(rect.Right - rect.Left) / thumbSizeX;
            var newH = p * thumbSizeY;
            var centerY = miniY + maxH / 2;
            rect.Top = (int)(centerY - newH / 2);
            rect.Bottom = rect.Top + (int)newH;
        }
        private System.Drawing.Image GetScreenshot(WindowCaptureHelper.Rect rcD)
        {
            if(rcD.Left != rcD.Right && rcD.Top != rcD.Bottom)
            {
                System.Drawing.Point point = new System.Drawing.Point();
                Win32.ClientToScreen(_windowHandle, ref point);
                var img = Helpers.WindowCaptureHelper.GetScreenshot(point.X + rcD.Left, point.Y + rcD.Top, rcD.Right - rcD.Left, rcD.Bottom - rcD.Top);
                return img;
            }
            else
            {
                return null;
            }
        }
    }
}
