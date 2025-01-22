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
using TheGuideToTheNewEden.Core.Models.CharacterScan;
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.Services;
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

        private async void Button_IdName_Click(object sender, RoutedEventArgs e)
        {
            var data = (sender as FrameworkElement).DataContext as IdName;
            if (data != null && data.Id > 0)
            {
                this.GetBaseWindow().ShowWaiting();
                await KBNavigationService.Default.NavigationTo(data);
                this.GetBaseWindow().HideWaiting();
            }
        }

        private void Button_IgnoreList_Delete(object sender, RoutedEventArgs e)
        {
            var item = (sender as FrameworkElement).DataContext as IdName;
            if (item != null)
            {
                VM.DeleteIgnoreCommand.Execute(item);
            }
        }

        private void MenuFlyoutItem_AddIgnore_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as FrameworkElement).DataContext as IdName;
            if (item != null)
            {
                VM.AddIgnore(item);
            }
        }

        private void MenuFlyoutItem_Reload_Click(object sender, RoutedEventArgs e)
        {
            var item = ResultDataGrid.SelectedItem as CharacterScanInfo;
            if(item != null)
            {
                VM.ReloadZKBInfo(item);
            }
        }
    }
}
