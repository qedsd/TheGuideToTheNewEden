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
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class EarlyWarningPage : Page, IPage
    {
        private BaseWindow _window;
        public EarlyWarningPage()
        {
            this.InitializeComponent();
            Loaded += EarlyWarningPage_Loaded2;
            Loaded += EarlyWarningPage_Loaded;
            if (ViewModels.EarlyWarningItemViewModel.RunningCharacters == null)
            {
                ViewModels.EarlyWarningItemViewModel.RunningCharacters = new HashSet<string>();
            }
            else
            {
                ViewModels.EarlyWarningItemViewModel.RunningCharacters.Clear();
            }
        }

        public void Close()
        {
            MapSolarSystemNameService.ClearCache();
            WarningService.Dispose();
        }
        private void EarlyWarningPage_Loaded2(object sender, RoutedEventArgs e)
        {
            _window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
        }
        private void EarlyWarningPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= EarlyWarningPage_Loaded;
            TabView_AddTabButtonClick(TabView, null);
        }

        private void TabView_AddTabButtonClick(TabView sender, object args)
        {
            TabViewItem item = new TabViewItem()
            {
                Header = "新建预警",
                IsSelected = true,
            };
            EarlyWarningItemPage content = new EarlyWarningItemPage(item);
            (content.DataContext as ViewModels.EarlyWarningItemViewModel).SetWindow(_window);
            item.Content = content;
            sender.TabItems.Add(item);
        }

        private void TabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            ((args.Item as TabViewItem).Content as EarlyWarningItemPage).Stop();
            sender.TabItems.Remove(args.Item);
        }
    }
}
