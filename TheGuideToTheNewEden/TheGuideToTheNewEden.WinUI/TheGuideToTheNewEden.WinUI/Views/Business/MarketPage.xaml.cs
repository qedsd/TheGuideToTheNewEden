using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Syncfusion.UI.Xaml.DataGrid;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.WinUI.Controls;
using TheGuideToTheNewEden.WinUI.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUIEx;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class MarketPage : Page, IPage
    {
        public MarketPage()
        {
            this.InitializeComponent();
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Flyout_SelecteMarket.Height = this.ActualSize.Y * 0.8;
        }

        public void ViewType(int typeID)
        {
            VM.SelectType(typeID);
            VM.Window.SetForegroundWindow();
        }

        public void Close()
        {
        }

        public void NavigatedTo(object parameter)
        {
            if (parameter is int id)
            {
                ViewType(id);
            }
        }
    }
}
