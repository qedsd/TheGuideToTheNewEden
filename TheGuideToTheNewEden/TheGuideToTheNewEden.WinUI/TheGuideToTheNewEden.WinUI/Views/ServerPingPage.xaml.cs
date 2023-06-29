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
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class ServerPingPage : Page
    {
        private Stopwatch _stopwatch;
        private BaseWindow _window;
        public ServerPingPage()
        {
            this.InitializeComponent();
            Loaded += ServerPingPage_Loaded;
        }

        private void ServerPingPage_Loaded(object sender, RoutedEventArgs e)
        {
            _window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
        }

        private void Button_Start_Click(object sender, RoutedEventArgs e)
        {
            if(_stopwatch == null)
            {
                _stopwatch = new Stopwatch();
            }
            if (_isStop)//准备开始
            {
                
                Start();
            }
            else
            {
               
                Stop();
            }
        }
        private void Start()
        {
            if(string.IsNullOrEmpty(ComboBox_Host.Text))
            {
                _window?.ShowError("请输入主机");
                return;
            }
            Button_Start.Content = "停止";
            Ping(ComboBox_Host.Text, (int)NumberBox_Port.Value, (int)NumberBox_Times.Value, NumberBox_Span.Value);
        }
        private void Stop()
        {
            Button_Start.Content = "开始";
            _isStop = true;
            _stopwatch.Stop();
        }
        private bool _isStop = true;
        private async void Ping(string host, int port, int times, double span)
        {
            TextBlock_Result.Text = string.Empty;
            _isStop = false;
            for (int i = 0; i< times;i++)
            {
                _stopwatch.Restart();
                var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock.Blocking = true;
                await sock.ConnectAsync(host, port);
                _stopwatch.Stop();
                sock.Disconnect(false);
                TextBlock_Result.Text += "连接至 " + host + ":" + port + " 延迟=" + Math.Round(_stopwatch.Elapsed.TotalMilliseconds, 0) + "ms\n";
                if(_isStop)
                {
                    return;
                }
                else
                {
                    await Task.Delay((int)span);
                }
                if (_isStop)
                {
                    return;
                }
            }
            Stop();
        }
    }
}
