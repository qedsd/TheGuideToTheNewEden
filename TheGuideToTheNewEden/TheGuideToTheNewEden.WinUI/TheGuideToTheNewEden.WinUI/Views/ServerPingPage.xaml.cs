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
using System.Collections.ObjectModel;
using TheGuideToTheNewEden.Core.Helpers;
using System.Net.NetworkInformation;
using Syncfusion.UI.Xaml.Data;
using ESI.NET.Models.Universe;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class ServerPingPage : Page
    {
        private BaseWindow _window;
        private ObservableCollection<PingStatus> _pings = new ObservableCollection<PingStatus>();
        private PingConfig _pingConfig;
        public ServerPingPage()
        {
            this.InitializeComponent();
            _pingConfig = PingConfig.Load();
            ComboBox_Host.Text = _pingConfig.Host;
            NumberBox_Port.Value = _pingConfig.Port;
            NumberBox_Times.Value = _pingConfig.Times;
            NumberBox_Span.Value = _pingConfig.Span;
            LineSeries1.ItemsSource = _pings;
            Loaded += ServerPingPage_Loaded;
            Unloaded += ServerPingPage_Unloaded;
        }

        private void ServerPingPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Stop();
        }

        private void ServerPingPage_Loaded(object sender, RoutedEventArgs e)
        {
            _window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
        }

        private void Button_Start_Click(object sender, RoutedEventArgs e)
        {
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
            if (string.IsNullOrEmpty(ComboBox_Host.Text))
            {
                _window?.ShowError("请输入主机");
                return;
            }
            _pingConfig.Host = ComboBox_Host.Text;
            _pingConfig.Port = (int)NumberBox_Port.Value;
            _pingConfig.Times = (int)NumberBox_Times.Value;
            _pingConfig.Span = (int)NumberBox_Span.Value;
            _pingConfig.Save();
            Button_Start.Content = "停止";
            _pings.Clear();
            Ping(ComboBox_Host.Text, (int)NumberBox_Port.Value, (int)NumberBox_Times.Value, NumberBox_Span.Value);
        }
        private void Stop()
        {
            Button_Start.Content = "开始";
            _isStop = true;
        }
        private bool _isStop = true;
        private async void Ping(string host, int port, int times, double span)
        {
            _isStop = false;
            for (int i = 0; i < times; i++)
            {
                var result = await TCPingHelper.Ping(host, port);
                if (_isStop)
                {
                    return;
                }
                else
                {
                    _pings.Add(new PingStatus(_pings.Count + 1, result));
                    var validPings = _pings.Where(p => p.Ms > 0).ToList();
                    if (validPings.Any())
                    {
                        var average = validPings.Average(p => p.Ms);
                        TextBlock_RTTAverage.Text = average.ToString("F2");
                        double sumP = 0;
                        validPings.ForEach(p => sumP += Math.Abs(p.Ms - average) / average);
                        TextBlock_RTTFluctuation.Text = (sumP / validPings.Count * 100).ToString("F2");
                        TextBlock_PacketLossRate.Text = ((_pings.Count - validPings.Count) / _pings.Count * 100).ToString("F2");
                    }
                    else
                    {
                        TextBlock_RTTAverage.Text = "--";
                        TextBlock_RTTFluctuation.Text = "--";
                        TextBlock_PacketLossRate.Text = "100";
                    }
                    await Task.Delay((int)span);
                }
                if (_isStop)
                {
                    return;
                }
            }
            Stop();
            _window?.ShowSuccess("已完成");
        }

        internal class PingStatus
        {
            internal PingStatus(int id, double ms)
            {
                Id = id;
                Ms = ms;
            }
            public int Id { get; set; }
            public double Ms { get; set; }
        }

        internal class PingConfig
        {
            public string Host { get; set; } = "tranquility.servers.eveonline.com";
            public int Port { get; set; } = 26000;
            public int Times { get; set; } = 8;
            public int Span { get; set; } = 1500;
            private static readonly string FilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "PingConfig.json");
            public static PingConfig Load()
            {
                if(System.IO.File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    return JsonConvert.DeserializeObject<PingConfig>(json);
                }
                else
                {
                    return new PingConfig();
                }
            }
            public void Save()
            {
                if(!Directory.Exists(Path.GetDirectoryName(FilePath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                }
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(this));
            }
        }
    }
}
