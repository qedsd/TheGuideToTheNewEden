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
using TheGuideToTheNewEden.Core.DBModels;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class ChannelScanPage : Page
    {
        public ChannelScanPage()
        {
            this.InitializeComponent();
        }

        private void Button_IdName_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_IgnoreList_Delete(object sender, RoutedEventArgs e)
        {
            var item = (sender as FrameworkElement).DataContext as IdName;
            if (item != null)
            {
                VM.DeleteIgnoreCommand.Execute(item);
            }
        }
    }
}
