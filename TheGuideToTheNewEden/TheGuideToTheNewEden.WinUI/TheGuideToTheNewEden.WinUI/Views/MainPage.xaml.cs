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
using Windows.Foundation;
using Windows.Foundation.Collections;
using NavigationViewItem = TheGuideToTheNewEden.WinUI.Models.NavigationViewItem;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class MainPage : UserControl
    {
        private List<NavigationViewItem> _navigationViewItems;
        public MainPage()
        {
            InitMenu();
            Loaded += MainPage_Loaded;
            this.InitializeComponent();
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainPage_Loaded;
            ClientServiceHelper.GetRequiredService<Services.NavigationService2>().Init(ContentFrame, Loading, InfoBar);
            MenuList.ItemsSource = _navigationViewItems;
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            VersionTextBlock.Text = version.ToString();
        }

        private void InitMenu()
        {
            _navigationViewItems = new List<NavigationViewItem>();

            _navigationViewItems.Add(new Models.NavigationViewItem(typeof(HomePage2)));
            _navigationViewItems.Add(new NavigationViewItem(null, "Navigation.CharacterPage"));
            _navigationViewItems.Add(new NavigationViewItem(null, "Navigation.MarketPage"));
            _navigationViewItems.Add(new NavigationViewItem(null, "Navigation.BusinessPage"));
            _navigationViewItems.Add(new NavigationViewItem(null, "Navigation.GamePreviewMgrPage"));
            _navigationViewItems.Add(new NavigationViewItem(null, "Navigation.ChannelIntelPage"));
            _navigationViewItems.Add(new NavigationViewItem(null, "Navigation.ChannelMonitorPage"));
            _navigationViewItems.Add(new NavigationViewItem(null, "Navigation.ChannelScanPage"));
            _navigationViewItems.Add(new NavigationViewItem(null, "Navigation.ChannelMarketPage"));
            _navigationViewItems.Add(new NavigationViewItem(null, "Navigation.GameLogMonitorPage"));
            _navigationViewItems.Add(new NavigationViewItem(null, "Navigation.TranslationPage"));
            _navigationViewItems.Add(new NavigationViewItem(null, "Navigation.DEDPage"));
            _navigationViewItems.Add(new NavigationViewItem(null, "Navigation.MissionPage"));
            _navigationViewItems.Add(new NavigationViewItem(null, "Navigation.WormholePage"));
            _navigationViewItems.Add(new NavigationViewItem(null, "Navigation.LinksPage"));
            _navigationViewItems.Add(new NavigationViewItem(null, "Navigation.MapPage"));
            _navigationViewItems.Add(new NavigationViewItem(null, "Navigation.ZKBPage"));
            _navigationViewItems.Add(new NavigationViewItem(null, "Navigation.DatabasePage"));
            _navigationViewItems.Add(new NavigationViewItem(typeof(SettingPage), "Navigation.SettingPage"));
        }

        public void Dispose()
        {
            ClientServiceHelper.GetRequiredService<Services.NavigationService2>()?.Dispose();
        }

        private void MenuList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = MenuList.SelectedItem as NavigationViewItem;
            if (item != null)
            {
                ClientServiceHelper.GetRequiredService<Services.NavigationService2>().NavigateTo(item.Type);
            }
        }
    }
}
