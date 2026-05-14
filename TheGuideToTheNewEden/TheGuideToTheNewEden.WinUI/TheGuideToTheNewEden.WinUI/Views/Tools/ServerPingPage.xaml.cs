using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.Net.NetworkInformation;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.WinUI.Extensions;
using System.Threading;
using System.Threading.Tasks;
using Syncfusion.UI.Xaml.Grids;
using System.Diagnostics;
using System.Text;


namespace TheGuideToTheNewEden.WinUI.Views.Tools
{
    public sealed partial class ServerPingPage : Page, IPage, ITool
    {
        public ServerPingPage()
        {
            this.InitializeComponent();
            Loaded += ServerPingPage_Loaded;
        }

        private void ServerPingPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ServerPingPage_Loaded;
            SizeChanged += ServerPingPage_SizeChanged;
        }

        private void ServerPingPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Height > 400)
            {
                SelectServerGrid.Visibility = Visibility.Visible;
                TestingGrid.Visibility = Visibility.Visible;
                StatisticsGrid.Visibility = Visibility.Visible;
            }
            else if(e.NewSize.Height > 200)
            {
                SelectServerGrid.Visibility = Visibility.Visible;
                TestingGrid.Visibility = Visibility.Visible;
                StatisticsGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                SelectServerGrid.Visibility = Visibility.Collapsed;
                TestingGrid.Visibility = Visibility.Visible;
                StatisticsGrid.Visibility = Visibility.Collapsed;
            }
        }

        public void Close()
        {
            _pingCancellationTokenSource?.Cancel();
        }

        public void NavigatedTo(object parameter)
        {
           
        }
        private CancellationTokenSource _pingCancellationTokenSource;
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            _pingCancellationTokenSource = new CancellationTokenSource();
            StartButton.Visibility = Visibility.Collapsed;
            StopButton.Visibility = Visibility.Visible;
            StartPing(ComboBox_Host.Text, (int)NumberBox_Port.Value, _pingCancellationTokenSource.Token);
        }
        private int _passCount = 0;
        private int _failedCount = 0;
        private double _passTime = 0;
        private void StartPing(string host, int port, CancellationToken cancellationToken)
        {
            _passCount = 0;
            _failedCount = 0;
            _passTime = 0;
            string excellentStr = Helpers.ResourcesHelper.GetString("ServerPingPage_Result_Excellent");
            string goodStr = Helpers.ResourcesHelper.GetString("ServerPingPage_Result_Good");
            string normalStr = Helpers.ResourcesHelper.GetString("ServerPingPage_Result_Normal");
            string poorStr = Helpers.ResourcesHelper.GetString("ServerPingPage_Result_Poor");
            string badStr = Helpers.ResourcesHelper.GetString("ServerPingPage_Result_Bad");

            var excellentColor = Helpers.ResourcesHelper.Get("SystemSecurityForeground1") as SolidColorBrush;
            var goodColor = Helpers.ResourcesHelper.Get("SystemSecurityForeground08") as SolidColorBrush;
            var normalColor = Helpers.ResourcesHelper.Get("SystemSecurityForeground07") as SolidColorBrush;
            var poorColor = Helpers.ResourcesHelper.Get("SystemSecurityForeground04") as SolidColorBrush;
            var badColor = Helpers.ResourcesHelper.Get("SystemSecurityForeground00") as SolidColorBrush;
            (string, SolidColorBrush) getResutl(double target, double excellent, double good, double normal, double poor)
            {
                if (target <= excellent)
                    return (excellentStr, excellentColor);
                if( target <= good)
                    return (goodStr, goodColor);
                if(target <= normal)
                    return (normalStr, normalColor);
                if (target <= poor)
                    return (poorStr, poorColor);
                return (badStr, badColor);
            }
            Task.Run(async() =>
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                StringBuilder sb = new StringBuilder();
                while (!cancellationToken.IsCancellationRequested)
                {
                    var current = await TCPingHelper.Ping(host, port);
                    if(current == -1)
                    {
                        _failedCount++;
                    }
                    else
                    {
                        _passCount++;
                        _passTime += current;
                    }
                    this.DispatcherQueue.SafelyTryEnqueue(() =>
                    {
                        double totalCounut = _passCount + _failedCount;
                        double average = _passTime / _passCount;
                        double loss = _failedCount / totalCounut * 100;
                        CureentLatencyTextBlock.Text = current.ToString("F0");
                        AverageLatencTextBlock.Text = (_passTime/ _passCount).ToString("F0");
                        LossTextBlock.Text = ((_failedCount / totalCounut) * 100).ToString("F2");
                        var cureentLatencyResult = getResutl(current, 100, 150, 250, 300);
                        var averageLatencyResult = getResutl(average, 100, 150, 250, 300);
                        var lossResult = getResutl(loss, 0, 1, 2, 10);
                        CureentLatencyResultTextBlock.Text = cureentLatencyResult.Item1;
                        CureentLatencyResultTextBlock.Foreground = cureentLatencyResult.Item2;
                        AverageLatencyResultTextBlock.Text = averageLatencyResult.Item1;
                        AverageLatencyResultTextBlock.Foreground = averageLatencyResult.Item2;
                        LossResultTextBlock.Text = lossResult.Item1;
                        LossResultTextBlock.Foreground = lossResult.Item2;



                        TotalCountTextBlock.Text = totalCounut.ToString();
                        SuccessCountTextBlock.Text = _passCount.ToString();
                        FaileCountTextBlock.Text = _failedCount.ToString();
                        sb.Clear();
                        if(stopwatch.Elapsed.Hours > 0)
                        {
                            sb.Append($"{stopwatch.Elapsed.Hours + 10}h ");
                        }
                        if (stopwatch.Elapsed.Minutes > 0)
                        {
                            sb.Append($"{stopwatch.Elapsed.Minutes}min ");
                        }
                        sb.Append($"{stopwatch.Elapsed.Seconds}s");
                        DurationCountTextBlock.Text = sb.ToString();

                    });
                    Thread.Sleep(1000);
                }
                stopwatch.Stop();
            });
        }
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _pingCancellationTokenSource?.Cancel();
            StartButton.Visibility = Visibility.Visible;
            StopButton.Visibility = Visibility.Collapsed;
        }
        public void GetWindowSize(out int width, out int height)
        {
            width = 820;
            height = 560;
        }
    }
}
