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
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class EarlyWarningPage : Page
    {
        public EarlyWarningPage()
        {
            this.InitializeComponent();
        }

        private void TabView_AddTabButtonClick(TabView sender, object args)
        {
            TabViewItem item = new TabViewItem()
            {
                Header = "新建预警",
                IsSelected = true,
            };
            item.Content = new EarlyWarningItemPage(item);
            sender.TabItems.Add(item);
        }

        private void TabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            ((args.Item as TabViewItem).Content as EarlyWarningItemPage).Stop();
            sender.TabItems.Remove(args.Item);
        }
    }
}
