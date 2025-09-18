using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TheGuideToTheNewEden.WinUI.Wins;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class ChannelMarketPage : Page, IPage
    {
        public ChannelMarketPage()
        {
            this.InitializeComponent();
        }

        public void Close()
        {
            VM.Dispose();
        }

        private void MenuFlyoutItem_OpenFoder_Click(object sender, RoutedEventArgs e)
        {

            var info = (sender as MenuFlyoutItem)?.DataContext as Models.ChatChanelInfo;
            if (info != null)
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select, \"{info.FilePath}\"");
            }
        }

        private void MenuFlyoutItem_OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var info = (sender as MenuFlyoutItem)?.DataContext as Models.ChatChanelInfo;
            if (info != null)
            {
                System.Diagnostics.Process.Start("explorer.exe", info.FilePath);
            }
        }
    }
}
