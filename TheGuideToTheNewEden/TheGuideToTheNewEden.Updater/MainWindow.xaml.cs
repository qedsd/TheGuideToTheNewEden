using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Handlers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TheGuideToTheNewEden.Updater
{
    public partial class MainWindow : Window
    {
        private string _version;
        private string _des;
        private string _url;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        public MainWindow()
        {
            InitializeComponent();
            if(App.Args != null && App.Args.Length >= 3)
            {
                _version = App.Args[0];
                _des = App.Args[1];
                _url = App.Args[2];
                TextBlock_Version.Text = _version;
                TextBlock_Des.Text = _des;
            }
            else
            {
                Button_Download.Visibility = Visibility.Collapsed;
            }
        }
        public async void Download(string url, string saveFile)
        {
            ProgressBar_Download.Value = 0;
            TextBlock_Received.Text = string.Empty;
            TextBlock_All.Text = string.Empty;
            _cancellationTokenSource = new CancellationTokenSource();
            var progressMessageHandler = new ProgressMessageHandler(new HttpClientHandler());
            progressMessageHandler.HttpReceiveProgress += ProgressMessageHandler_HttpReceiveProgress;
            try
            {
                using (var client = new HttpClient(progressMessageHandler))
                {
                    client.Timeout = TimeSpan.FromHours(24);
                    using (var filestream = new FileStream(saveFile, FileMode.Create))
                    {
                        var response = await client.GetAsync(url, _cancellationTokenSource.Token);
                        var stream = await response.Content.ReadAsStreamAsync();
                        await stream.CopyToAsync(filestream);
                        AutoInstall();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                HideDownload();
            }
        }

        private void ProgressMessageHandler_HttpReceiveProgress(object sender, HttpProgressEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                ProgressBar_Download.Value = e.ProgressPercentage;
                TextBlock_Received.Text = $"{e.BytesTransferred / 1024f / 1024f:N1} MB";
                TextBlock_All.Text = $"{e.TotalBytes / 1024f / 1024f:N1} MB";
            });
        }

        private void WebClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                ProgressBar_Download.Value = e.ProgressPercentage;
                TextBlock_Received.Text = $"{e.BytesReceived / 1024f / 1024f:N1} MB";
                TextBlock_All.Text = $"{e.TotalBytesToReceive / 1024f / 1024f:N1} MB";
            });
        }

        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void Button_Mini_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }


        private void Button_Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Button_Download_Click(object sender, RoutedEventArgs e)
        {
            ShowDownload();
            Download(_url, _url.Split('/').Last());
        }

        private void Button_Browser_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("explorer.exe", "https://github.com/qedsd/TheGuideToTheNewEden/releases/latest");
        }

        private void Button_CancelDownload_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource.Cancel();
            HideDownload();
        }

        private void ShowDownload()
        {
            Button_Download.Visibility = Visibility.Collapsed;
            Button_CancelDownload.Visibility = Visibility.Visible;
            StackPanel_DownloadInfo.Visibility = Visibility.Visible;
            ProgressBar_Download.Visibility = Visibility.Visible;
        }
        private void HideDownload()
        {
            Button_Download.Visibility = Visibility.Visible;
            Button_CancelDownload.Visibility = Visibility.Collapsed;
            StackPanel_DownloadInfo.Visibility = Visibility.Collapsed;
            ProgressBar_Download.Visibility = Visibility.Collapsed;
        }

        private void AutoInstall()
        {
            HideDownload();
            if (MessageBox.Show("文件已下载完成，是否开始安装？", "安装", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {

            }
        }
    }
}
