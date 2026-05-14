using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.Core.Extensions;
using System.Reflection.Metadata;
using TheGuideToTheNewEden.WinUI.Services;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class PriceCardControl : UserControl
    {
        private const int TargetDays = 30;
        public PriceCardControl()
        {
            this.InitializeComponent();
        }
        public static readonly DependencyProperty TypeIdProperty
           = DependencyProperty.Register(
               nameof(TypeId),
               typeof(int),
               typeof(PriceCardControl),
               new PropertyMetadata(true, new PropertyChangedCallback(TypeIdPropertyChanged)));

        public int TypeId
        {
            get => (int)GetValue(TypeIdProperty);
            set => SetValue(TypeIdProperty, value);
        }

        private static void TypeIdPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as PriceCardControl;
            control.LoadData();
        }

        private async void LoadData()
        {
            LoadingUI.Visibility = Visibility.Visible;
            LoadDataButton.Visibility = Visibility.Collapsed;
            LoadingUI.IsActive = true;
            try
            {
                var type = Core.Services.DB.InvTypeService.QueryType(TypeId);
                TypeImage.Source = new BitmapImage(new Uri(Converters.GameImageConverter.GetImageUri(TypeId, Converters.GameImageConverter.ImgType.Type, 32)));
                TypeName.Text = type.TypeName;
                var orders = await Services.MarketOrderService.Current.GetOrderAsync(TypeId);
                var sellPrice = orders.Where(p => !p.IsBuyOrder).Min(p => p.Price);
                double showPrice = sellPrice;
                if (TypeId == Services.MarketOrderService.PlexTypeId)
                {
                    PriceTip.Text = "×500";
                    showPrice = sellPrice * 500;
                }
                
                PriceTextBlock.Text = showPrice % 1 != 0 ? showPrice.ToString("N2") : showPrice.ToString("N0");
                var allHistories = await Services.MarketOrderService.Current.GetHistoryAsync(TypeId);
                if (allHistories.NotNullOrEmpty())
                {
                    var targetHistories = allHistories.Where(p => (DateTime.Now - p.Date).TotalDays <= TargetDays).ToList();
                    var avgPrice = targetHistories.Average(p => p.Highest);
                    var priceDiff = (sellPrice - avgPrice)/ avgPrice * 100;//当前相对于过去一段时间平均价的涨幅 
                    string diffWay = priceDiff > 0 ? "+" : string.Empty;
                    PriceChangeTextBlock.Text = $"{diffWay}{priceDiff.ToString("N2")}%";
                    PriceChangeTextBlock.Foreground = priceDiff > 0 ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 229, 101, 91)) : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 97, 197, 171));
                    
                    var avgVolume = targetHistories.Average(p => p.Volume);
                    var n = targetHistories.Count;
                    var xValues = Enumerable.Range(1, n).Select(x => (double)x).ToArray();
                    var yValues = targetHistories.Select(p=>(double)p.Volume).ToArray();

                    // 计算斜率 (m) 和截距 (b) for y = mx + b
                    var (slope, intercept, rSquared) = Core.Helpers.MathHelper.CalculateLinearRegression(xValues, yValues);

                    // 计算趋势涨幅
                    var startTrendValue = slope * 1 + intercept; // x=1时的趋势值
                    var endTrendValue = slope * n + intercept;   // x=n时的趋势值
                    var trendGrowth = (endTrendValue - startTrendValue) / startTrendValue * 100;
                    VolumeTextBlock.Text = avgVolume.ToString("N0");
                    diffWay = trendGrowth > 0 ? "+" : string.Empty;
                    VolumeChangeTextBlock.Text = $"{diffWay}{trendGrowth.ToString("N2")}%";
                    VolumeChangeTextBlock.Foreground = trendGrowth > 0 ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 229, 101, 91)) : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 97, 197, 171));
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                LoadDataButton.Visibility = Visibility.Visible;
            }
            finally
            {
                LoadingUI.Visibility = Visibility.Collapsed;
                LoadingUI.IsActive = true;
            }
            
        }

        private void LoadDataButton_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void DetailButton_Click(object sender, RoutedEventArgs e)
        {
            ClientServiceHelper.GetRequiredService<PageNavigationService>().NavigateToMarket(TypeId);
        }
    }
}
