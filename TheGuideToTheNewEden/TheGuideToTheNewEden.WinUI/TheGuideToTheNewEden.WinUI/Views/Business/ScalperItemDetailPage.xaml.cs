using ESI.NET.Models.Market;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.Core.Models.Market;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views.Business
{
    public sealed partial class ScalperItemDetailPage : Page
    {
        public ScalperItemDetailPage()
        {
            this.InitializeComponent();
        }
        private ScalperItem _scalperItem;
        public void SetItem(ScalperItem item)
        {
            _scalperItem = item;
            Image_Type.Source = new BitmapImage(new Uri(Converters.GameImageConverter.GetImageUri(item.InvType.TypeID, Converters.GameImageConverter.ImgType.Type, 64)));
            TextBlock_TypeName.Text = item.InvType.TypeName;
            TextBlock_Suggestion.Text = item.Suggestion.ToString("N2");
            TextBlock_BuyPrice.Text = item.BuyPrice.ToString("N2");
            TextBlock_SellPrice.Text = item.SellPrice.ToString("N2");
            TextBlock_DestinationSales.Text = item.DestinationSales.ToString("N0");
            TextBlock_TargetSales.Text = item.TargetSales.ToString("N0");
            TextBlock_ROI.Text = item.ROI.ToString("N2");
            TextBlock_NetProfit.Text = item.NetProfit.ToString("N2");
            TextBlock_TargetSalesNetProfit.Text = item.TargetNetProfit.ToString("N2");
            TextBlock_HeatValue.Text = item.HeatValue.ToString("N2");
            TextBlock_Principal.Text = item.Principal.ToString("N2");
            TextBlock_HistoryPriceFluctuation.Text = item.HistoryPriceFluctuation.ToString("N2");
            TextBlock_NowPriceFluctuation.Text = item.NowPriceFluctuation.ToString("N2");
            TextBlock_SellOrderSaturation.Text = item.Saturation.ToString("N2");
            TextBlock_NetProfit.Text = item.NetProfit.ToString("N2");
            TextBlock_NetProfit.Text = item.NetProfit.ToString("N2");
            TextBlock_Volume.Text = item.InvType.PackagedVolume.ToString("N2");

            DataGrid_SourceSell.ItemsSource = item.SourceSellOrders;
            DatGrid_SourceBuy.ItemsSource = item.SourceBuyOrders;
            DataGrid_DestinationSell.ItemsSource = item.DestinationSellOrders;
            DatGrid_DestinationBuy.ItemsSource = item.DestinationBuyOrders;
            UpdateSourceHistory();
            UpdateDestinationHistory();
        }

        private void ComboBox_Source_HistoryRange_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSourceHistory();
        }

        private void ComboBox_Destination_HistoryRange_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDestinationHistory();
        }
        private void UpdateSourceHistory()
        {
            if(_scalperItem == null)
            {
                return;
            }
            List<Core.Models.Market.Statistic> statistics = null;
            switch (ComboBox_Source_HistoryRange.SelectedIndex)
            {
                case 0:
                    {
                        statistics = _scalperItem.SourceStatistics.Where(p => p.Date > DateTime.Now.AddMonths(-1)).ToList();
                    }
                    break;
                case 1:
                    {
                        statistics = _scalperItem.SourceStatistics.Where(p => p.Date > DateTime.Now.AddMonths(-3)).ToList();
                    }
                    break;
                case 2:
                    {
                        statistics = _scalperItem.SourceStatistics.Where(p => p.Date > DateTime.Now.AddMonths(-6)).ToList();
                    }
                    break;
                case 3:
                    {
                        statistics = _scalperItem.SourceStatistics.Where(p => p.Date > DateTime.Now.AddMonths(-12)).ToList();
                    }
                    break;
                case 4:
                    {
                        statistics = _scalperItem.SourceStatistics;
                    }
                    break;
            }
            LineSeries_Source_StatisticHighest.ItemsSource = statistics;
            LineSeries_Source_StatisticAverage.ItemsSource = statistics;
            LineSeries_Source_StatisticLowest.ItemsSource = statistics;
            LineSeries_Source_StatisticVolume.ItemsSource = statistics;
        }
        private void UpdateDestinationHistory()
        {
            if (_scalperItem == null)
            {
                return;
            }
            List<Core.Models.Market.Statistic> statistics = null;
            switch (ComboBox_Destination_HistoryRange.SelectedIndex)
            {
                case 0:
                    {
                        statistics = _scalperItem.DestinationStatistics.Where(p => p.Date > DateTime.Now.AddMonths(-1)).ToList();
                    }
                    break;
                case 1:
                    {
                        statistics = _scalperItem.DestinationStatistics.Where(p => p.Date > DateTime.Now.AddMonths(-3)).ToList();
                    }
                    break;
                case 2:
                    {
                        statistics = _scalperItem.DestinationStatistics.Where(p => p.Date > DateTime.Now.AddMonths(-6)).ToList();
                    }
                    break;
                case 3:
                    {
                        statistics = _scalperItem.DestinationStatistics.Where(p => p.Date > DateTime.Now.AddMonths(-12)).ToList();
                    }
                    break;
                case 4:
                    {
                        statistics = _scalperItem.DestinationStatistics;
                    }
                    break;
            }
            LineSeries_Destination_StatisticHighest.ItemsSource = statistics;
            LineSeries_Destination_StatisticAverage.ItemsSource = statistics;
            LineSeries_Destination_StatisticLowest.ItemsSource = statistics;
            LineSeries_Destination_StatisticVolume.ItemsSource = statistics;
        }
    }
}
