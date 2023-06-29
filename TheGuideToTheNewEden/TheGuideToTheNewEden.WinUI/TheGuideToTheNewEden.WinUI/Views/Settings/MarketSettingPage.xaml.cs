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
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views.Settings
{
    public sealed partial class MarketSettingPage : Page
    {
        public MarketSettingPage()
        {
            this.InitializeComponent();
            Loaded += MarketSettingPage_Loaded;
        }

        private void MarketSettingPage_Loaded(object sender, RoutedEventArgs e)
        {
            VM.Window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
        }
    }
}
