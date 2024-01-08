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
using TheGuideToTheNewEden.WinUI.Extensions;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class ZKBHomePage : Page
    {
        public ZKBHomePage()
        {
            this.InitializeComponent();
            Loaded += ZKBHomePage_Loaded;
        }

        private async void ZKBHomePage_Loaded(object sender, RoutedEventArgs e)
        {
            this.GetBaseWindow()?.ShowWaiting(Helpers.ResourcesHelper.GetString("ZKBHomePage_ConnectingToWSS"));
            await VM.InitAsync();
            this.GetBaseWindow()?.HideWaiting();
        }
    }
}
