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
using TheGuideToTheNewEden.Core.Models;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class ChannelMonitorPage : Page
    {
        public ChannelMonitorPage()
        {
            this.InitializeComponent();
        }

        private void MenuFlyoutItem_OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var info = (sender as MenuFlyoutItem)?.DataContext as Models.ChatChanelInfo;
            if (info != null)
            {
                System.Diagnostics.Process.Start("explorer.exe", info.FilePath);
            }
        }

        private void Button_DeleteKey_Click(object sender, RoutedEventArgs e)
        {
            VM.SelectedCharacter.Setting.Keys.Remove((sender as Button).DataContext as ChannelMonitorKey);
        }
    }
}
