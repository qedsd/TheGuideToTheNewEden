using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.Core.Models;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class LocalIntelNotifyWindowPage : Page
    {
        private readonly ObservableCollection<LocalIntelNotify> _localIntelNotifies = new ObservableCollection<LocalIntelNotify>();
        public LocalIntelNotifyWindowPage()
        {
            this.InitializeComponent();
            ListView.ItemsSource = _localIntelNotifies;
        }
        public void Add(LocalIntelNotify localIntelNotify)
        {
            _localIntelNotifies.Add(localIntelNotify);
            ListView.SelectedItem = localIntelNotify;
            ListView.ScrollIntoView(localIntelNotify);
        }

        private void Grid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            var data = (sender as FrameworkElement).DataContext as LocalIntelNotify;
            if(data != null)
            {
                Helpers.WindowHelper.SetForegroundWindow_Click(data.HWnd);
            }
        }
    }
}
