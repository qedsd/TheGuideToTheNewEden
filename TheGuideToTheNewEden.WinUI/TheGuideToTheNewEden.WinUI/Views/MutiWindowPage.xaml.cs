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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.WinUI.Models;
using System.Drawing;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class MutiWindowPage : Page
    {
        private BaseWindow BaseWindow;
        public MutiWindowPage()
        {
            this.InitializeComponent();
            Loaded += MutiWindowPage_Loaded;
            SizeChanged += MutiWindowPage_SizeChanged;
        }

        object locker = new object();
        private int ImageH;
        private void MutiWindowPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            lock(locker)
            {
                ImageH = (int)e.NewSize.Width;
            }
        }
        WindowCapture windowCapture;
        private async void MutiWindowPage_Loaded(object sender, RoutedEventArgs e)
        {
            BaseWindow = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
            var list = await FindWindowHelper.EnumWindowsAsync();
            if(list.NotNullOrEmpty())
            {
                var target = list.FirstOrDefault(p => p.Title.Contains("钉钉"));
                if(target.hwnd != IntPtr.Zero)
                {
                    windowCapture = new WindowCapture(target);
                    windowCapture.OnWindowCaptured += WindowCapture_OnWindowCaptured;
                    windowCapture.Start();
                    ShowWindowHelper.SetForegroundWindow(target.hwnd);
                }
            }
        }

        private void WindowCapture_OnWindowCaptured(WindowInfo winfowInfo, System.Drawing.Bitmap bitmap)
        {
            int h = ImageH;
            //lock (locker)
            //{
            //    h = ImageH;
            //}
            Bitmap resizeBitmap = new Bitmap(bitmap, new System.Drawing.Size(h / bitmap.Height * bitmap.Width, h));
            bitmap.Dispose();
            //var cutBitmap = ImageHelper.CutBitmap(resizeBitmap, new Rectangle((int)(resizeBitmap.Width * 0.1), (int)(resizeBitmap.Height * 0.1), (int)(resizeBitmap.Width * 0.5), (int)(resizeBitmap.Height * 0.5)));
            //var resize = Core.Helpers.ImageHelper.ResetSize(Helpers.BitmapConveters.ConvertToMemoryStream(bitmap), 0, h);
            //var resizeBitmap = Helpers.BitmapConveters.ConvertFromMemoryStream(resize);
            BaseWindow.DispatcherQueue.TryEnqueue(() =>
            {
                //Img.Source = Helpers.BitmapConveters.ConvertToBitMapSource(resizeBitmap);
            });
            windowCapture.Start();
        }
    }
}
