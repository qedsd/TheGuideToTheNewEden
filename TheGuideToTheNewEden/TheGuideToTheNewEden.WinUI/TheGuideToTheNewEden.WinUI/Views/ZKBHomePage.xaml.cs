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
using TheGuideToTheNewEden.WinUI.Views.KB;
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
            Loaded -= ZKBHomePage_Loaded;
            this.GetBaseWindow()?.ShowWaiting(Helpers.ResourcesHelper.GetString("ZKBHomePage_ConnectingToWSS"));
            await VM.InitAsync();
            this.GetBaseWindow()?.HideWaiting();
        }

        private void KBListControl_OnItemClicked(Core.Models.KB.KBItemInfo itemInfo)
        {
            KBDetailPage detailPage = new KBDetailPage(itemInfo);
            TabViewItem item = new TabViewItem()
            {
                Header = itemInfo.Victim.Name,
                IsSelected = true,
                Content = detailPage
            };
            ContentTabView.TabItems.Add(item);
        }

        private void ContentTabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            sender.TabItems.Remove(args.Item);
        }
    }
}
