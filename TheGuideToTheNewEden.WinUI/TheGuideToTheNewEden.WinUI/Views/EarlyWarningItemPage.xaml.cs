using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.WinUI.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class EarlyWarningItemPage : Page
    {
        public EarlyWarningItemPage()
        {
            this.InitializeComponent();
            //Loaded += EarlyWarningItemPage_Loaded;
        }

        private void EarlyWarningItemPage_Loaded(object sender, RoutedEventArgs e)
        {
            //(this.DataContext as EarlyWarningItemViewModel).ChatContents.CollectionChanged += ChatContents_CollectionChanged;
        }

        private void ChatContents_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            //弃用自动滚动到末尾，会闪烁
            if(e.NewItems != null && e.NewItems.Count != 0)
            {
                LogContentListView.ScrollIntoView(e.NewItems[e.NewItems.Count - 1]);
            }
        }

        public void Stop()
        {
            (DataContext as ViewModels.EarlyWarningItemViewModel).StopCommand.Execute(null);
        }
    }
}
