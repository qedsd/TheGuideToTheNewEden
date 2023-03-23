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

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class MutiWindowPage : Page
    {
        public MutiWindowPage()
        {
            this.InitializeComponent();
            Loaded += MutiWindowPage_Loaded;
        }

        private void MutiWindowPage_Loaded(object sender, RoutedEventArgs e)
        {
            var window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var img = Helpers.WindowCaptureHelper.GetShotCutImage(hWnd);
            if (img != null)
            {
                Img.Source = Helpers.BitmapConveters.ConvertToBitMapSource(img);
            }
        }
    }
}
