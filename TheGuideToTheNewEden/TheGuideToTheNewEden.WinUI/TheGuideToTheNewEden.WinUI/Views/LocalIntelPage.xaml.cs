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
            //Test();
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

        private void Test()
        {
            var sourceMat = IntelImageHelper.ImageToMat(System.Drawing.Image.FromFile("D:\\System\\图片\\EVE\\3.png"));
            var grayMat = IntelImageHelper.GetGray(sourceMat);
            var edgeMat = IntelImageHelper.GetEdge(grayMat);
            
            var writeableBitmap = ImageHelper.MemoryStreamConvertToWriteableBitmap(edgeMat.Width, edgeMat.Height, edgeMat.ToMemoryStream());
            Image1.Source = writeableBitmap;
            var rects = IntelImageHelper.CalStandingRects(edgeMat,8);
            var afterDrawRectMat = IntelImageHelper.DrawRects(sourceMat, rects);
            
            var writeableBitmap2 = ImageHelper.MemoryStreamConvertToWriteableBitmap(afterDrawRectMat.Width, afterDrawRectMat.Height, afterDrawRectMat.ToMemoryStream());
            Image2.Source = writeableBitmap2;

            sourceMat.Dispose();
            edgeMat.Dispose();
            grayMat.Dispose();
            afterDrawRectMat.Dispose();
        }
    }
}
