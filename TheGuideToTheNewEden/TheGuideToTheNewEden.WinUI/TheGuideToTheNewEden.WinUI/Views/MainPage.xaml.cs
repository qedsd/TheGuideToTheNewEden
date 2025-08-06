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
using TheGuideToTheNewEden.WinUI.Views.Character;
using TheGuideToTheNewEden.WinUI.Views.Map;
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
            ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().Init(NavPanel,ContentFrame, Loading, InfoBar);
            MenuList.ItemsSource = _navigationViewItems;
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            VersionTextBlock.Text = version.ToString();
        }

        private void InitMenu()
        {
            _navigationViewItems = new List<NavigationViewItem>();

            _navigationViewItems.Add(new Models.NavigationViewItem(typeof(HomePage2)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(CharactersShellPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(MarketPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(BusinessPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(GamePreviewMgrPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(ChannelIntelPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(ChannelMonitorPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(ChannelScanPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(ChannelMarketPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(GameLogMonitorPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(TranslationPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(DEDPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(MissionPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(WormholePage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(LinksPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(MapPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(ZKBHomePage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(DatabasePage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(SettingPage)));
        }

        public void Dispose()
        {
            ClientServiceHelper.GetRequiredService<Services.PageNavigationService>()?.Dispose();
        }

        private void MenuList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = MenuList.SelectedItem as NavigationViewItem;
            if (item != null)
            {
                ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().NavigateTo(item.Type);
            }
        }
    }
}
