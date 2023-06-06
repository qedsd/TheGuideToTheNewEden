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
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class MarketPage : Page
    {
        public MarketPage()
        {
            this.InitializeComponent();
            Loaded += MarketPage_Loaded;
        }

        private void MarketPage_Loaded(object sender, RoutedEventArgs e)
        {
            VM.Window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Flyout_SelecteMarket.Height = this.ActualSize.Y * 0.8;
        }
    }
    public class SystemSecurityCellStyleSelector : StyleSelector
    {
        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            var data = item as Core.Models.Market.Order;
            var mappingName = (container as GridCell).ColumnBase.GridColumn.MappingName;

            if (mappingName == "Security")
            {
                switch(data.Security)
                {
                    case 1: return Helpers.ResourcesHelper.Get("SystemSecurityForegroundCellStyle1") as Style;
                    case 0.9: return Helpers.ResourcesHelper.Get("SystemSecurityForegroundCellStyle09") as Style;
                    case 0.8: return Helpers.ResourcesHelper.Get("SystemSecurityForegroundCellStyle08") as Style;
                    case 0.7: return Helpers.ResourcesHelper.Get("SystemSecurityForegroundCellStyle07") as Style;
                    case 0.6: return Helpers.ResourcesHelper.Get("SystemSecurityForegroundCellStyle06") as Style;
                    case 0.5: return Helpers.ResourcesHelper.Get("SystemSecurityForegroundCellStyle05") as Style;
                    case 0.4: return Helpers.ResourcesHelper.Get("SystemSecurityForegroundCellStyle04") as Style;
                    case 0.3: return Helpers.ResourcesHelper.Get("SystemSecurityForegroundCellStyle03") as Style;
                    case 0.2: return Helpers.ResourcesHelper.Get("SystemSecurityForegroundCellStyle02") as Style;
                    case 0.1: return Helpers.ResourcesHelper.Get("SystemSecurityForegroundCellStyle01") as Style;
                    default: return Helpers.ResourcesHelper.Get("SystemSecurityForegroundCellStyle00") as Style;
                }
            }
            return base.SelectStyleCore(item, container);
        }
    }
}
