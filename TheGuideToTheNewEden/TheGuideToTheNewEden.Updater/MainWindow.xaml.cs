using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                Button_Download_Click(null, null);
            }
            else
            {
                Button_Download.Visibility = Visibility.Collapsed;
            }
        }
        public async void Download(string url, string saveFile)
        {
            _savedFileName = System.IO.Path.GetFileName(saveFile);
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
        private string _savedFileName;
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
        private void ShowInstall()
        {
            HideDownload();
            Button_Download.Visibility = Visibility.Collapsed;
        }
        private void AutoInstall()
        {
            if (MessageBox.Show("文件下载完成，请关闭软件后点击安装", "安装", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Install();
            }
            else
            {
                ShowInstall();
                Button_Install.Visibility = Visibility.Visible;
            }
        }
        private async void Install()
        {
            ShowInstall();
            if (KillProcess())
            {
                LoadingLine_Installing.Visibility = Visibility.Visible;
                await Extract();
                await CopyTo();
                Clear();
                LoadingLine_Installing.Visibility = Visibility.Collapsed;
                MessageBox.Show("安装完成");
                this.Close();
            }
        }
        private bool KillProcess()
        {
            //string basePath = AppDomain.CurrentDomain.BaseDirectory.Substring(0, AppDomain.CurrentDomain.BaseDirectory.Length - 8);
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    if (p.ProcessName == "TheGuideToTheNewEden")
                    {
                        //if(System.IO.Path.GetDirectoryName(p.MainModule.FileName) == basePath )
                        {
                            p.Kill();
                            p.WaitForExit();
                        }
                    }
                }
                catch(Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    return false;
                }
            }
            return true;
        }
        private string _tempFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Temp");
        /// <summary>
        /// 解压
        /// </summary>
        /// <returns></returns>
        private async Task<bool> Extract()
        {
            return await Task.Run(() =>
            {
                try
                {
                    string folder = _tempFolder;
                    if (System.IO.Directory.Exists(folder))
                    {
                        System.IO.Directory.Delete(folder, true);
                    }
                    System.IO.Directory.CreateDirectory(folder);
                    (new FastZip()).ExtractZip(_savedFileName, folder, "");
                    return true;
                }
                catch (Exception ex)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(ex.Message);
                    });
                    return false;
                }
            });
        }
        /// <summary>
        /// 备份原文件
        /// </summary>
        /// <returns></returns>
        private async Task<bool> Backup()
        {
            return await Task.Run(() =>
            {
                try
                {
                    string basePath = AppDomain.CurrentDomain.BaseDirectory.Substring(0, AppDomain.CurrentDomain.BaseDirectory.Length - 8);
                    
                    return true;
                }
                catch( Exception ex )
                {
                    MessageBox.Show(ex.Message);
                    return false;
                }
            });
        }
        private async Task<bool> CopyTo()
        {
            return await Task.Run(() =>
            {
                try
                {
                    DirectoryInfo currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                    string targetFolder = currentDir.Parent.FullName;
                    string tempFolder = _tempFolder;
                    var sourceFolder = Directory.GetDirectories(tempFolder)?.FirstOrDefault();
                    CopyFiles(sourceFolder, targetFolder);
                    return true;
                }
                catch (Exception ex)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(ex.Message);
                    });
                    return false;
                }
            });
        }
        private void CopyFiles(string sourceFolder, string targetFolder)
        {
            if(targetFolder == AppDomain.CurrentDomain.BaseDirectory)//不删除当前程序文件夹
            {
                return;
            }
            if(!string.IsNullOrEmpty(sourceFolder) && !string.IsNullOrEmpty(targetFolder))
            {
                if(Directory.Exists(sourceFolder))
                {
                    if(!Directory.Exists(targetFolder))
                    {
                        Directory.CreateDirectory(targetFolder);
                    }
                    var sourceFilse = Directory.GetFiles(sourceFolder);
                    if (sourceFilse?.Length > 0)
                    {
                        foreach (var sourceFile in sourceFilse)
                        {
                            File.Copy(sourceFile, System.IO.Path.Combine(targetFolder, System.IO.Path.GetFileName(sourceFile)), true);
                        }
                    }
                    var sourceDirs = Directory.GetDirectories(sourceFolder);
                    if (sourceDirs?.Length > 0)
                    {
                        foreach (var sourceDir in sourceDirs)
                        {
                            CopyFiles(sourceDir, System.IO.Path.Combine(targetFolder, sourceDir.Split('\\').Last()));
                        }
                    }
                }
            }
        }
        private async void Clear()
        {
            try
            {
                await Task.Run(() =>
                {
                    if (!string.IsNullOrEmpty(_savedFileName) && File.Exists(_savedFileName))
                    {
                        File.Delete(_savedFileName);
                    }
                    Directory.Delete(_tempFolder, true);
                });
            }
            catch(Exception ex)
            {
                this.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(ex.Message);
                });
            }
        }

        private void Button_Install_Click(object sender, RoutedEventArgs e)
        {
            Install();
        }
    }
}
