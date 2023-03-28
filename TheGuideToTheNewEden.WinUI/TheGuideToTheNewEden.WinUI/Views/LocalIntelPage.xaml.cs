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
using TheGuideToTheNewEden.WinUI.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class LocalIntelPage : Page
    {
        private BaseWindow _window;
        public LocalIntelPage()
        {
            this.InitializeComponent();
            this.Unloaded += LocalIntelPage_Unloaded;
            Loaded += LocalIntelPage_Loaded;
        }

        private void LocalIntelPage_Loaded(object sender, RoutedEventArgs e)
        {
            _window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
        }

        private void LocalIntelPage_Unloaded(object sender, RoutedEventArgs e)
        {

        }
        private int inteIndex = 1;
        private void TabView_AddTabButtonClick(TabView sender, object args)
        {
            TabViewItem item = new TabViewItem()
            {
                Header = $"本地预警{inteIndex++}",
                IsSelected = true,
            };
            LocalIntelItemPage content = new LocalIntelItemPage();
            (content.DataContext as BaseViewModel).SetWindow(_window);
            item.Content = content;
            sender.TabItems.Add(item);
        }

        private void TabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            ((args.Item as TabViewItem).Content as LocalIntelItemPage).Stop();
            sender.TabItems.Remove(args.Item);
        }
    }
}
