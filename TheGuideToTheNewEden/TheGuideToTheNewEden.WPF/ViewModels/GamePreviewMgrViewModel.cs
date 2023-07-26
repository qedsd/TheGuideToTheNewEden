using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Extensions;
using System.Drawing;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using System.Diagnostics;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using System.Collections.ObjectModel;
using Newtonsoft.Json.Linq;
using System.Timers;
using System.Xml.Linq;
using TheGuideToTheNewEden.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;
using TheGuideToTheNewEden.WPF.Wins;
using System.Security.AccessControl;
using Octokit;
using TheGuideToTheNewEden.WinCore;

namespace TheGuideToTheNewEden.WPF.ViewModels
{
    internal class GamePreviewMgrViewModel: ObservableObject
    {
        private ObservableCollection<ProcessInfo> processes;
        public ObservableCollection<ProcessInfo> Processes
        {
            get => processes;
            set => SetProperty(ref processes, value);
        }
        private Visibility settingVisible = Visibility.Collapsed;
        public Visibility SettingVisible
        {
            get => settingVisible;
            set => SetProperty(ref settingVisible,value);
        }
        private bool running;
        public bool Running
        {
            get => running;
            set => SetProperty(ref running, value);
        }

        private string hotKey = "F4";
        public string HotKey
        {
            get => hotKey;
            set => SetProperty(ref hotKey, value);
        }
        private string processName = "exefile";
        public string ProcessName
        {
            get => processName;
            set => SetProperty(ref processName, value);
        }

        private static readonly string Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "GamePreviewSetting.json");
        /// <summary>
        /// key为ProcessInfo.Guid,进程唯一标识符，与角色名称、设置名称无关
        /// </summary>
        private Dictionary<string, GamePreviewWindow> _runningDic = new Dictionary<string, GamePreviewWindow>();
        public GamePreviewMgrViewModel()
        {
            ForegroundWindowService.Current.Start();
            ForegroundWindowService.Current.OnForegroundWindowChanged += Current_OnForegroundWindowChanged;
            Init();
            HotkeyService.OnKeyboardClicked += HotkeyService_OnKeyboardClicked;
        }
        #region 切换快捷键
        private string _lastProcessGUID;
        private void HotkeyService_OnKeyboardClicked(List<WinCore.KeyboardHook.KeyboardInfo> keys)
        {
            if (keys.NotNullOrEmpty())
            {
                if (_runningDic.Count != 0 && !string.IsNullOrEmpty(HotKey))
                {
                    var keynames = HotKey.Split('+');
                    if (keynames.NotNullOrEmpty())
                    {
                        var targetkeys = keynames.ToList();
                        foreach (var key in targetkeys)
                        {
                            if (!keys.Where(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase)).Any())
                            {
                                return;
                            }
                        }
                        if (_lastProcessGUID == null)
                        {
                            var firstRunning = Processes.FirstOrDefault(p => p.Running);
                            if(firstRunning != null)
                            {
                                if(_runningDic.TryGetValue(firstRunning.GUID, out var value))
                                {
                                    value.ActiveSourceWindow();
                                    _lastProcessGUID = firstRunning.GUID;
                                }
                            }
                        }
                        else
                        {
                            var runnings = Processes.Where(p => p.Running).ToList();
                            for (int i = 0; i < runnings.Count; i++)
                            {
                                var item = runnings[i];
                                if (item.GUID == _lastProcessGUID)
                                {
                                    string targetGUID = null;
                                    if (i != runnings.Count - 1)
                                    {
                                        targetGUID = runnings[i + 1].GUID;
                                    }
                                    else
                                    {
                                        targetGUID = runnings.First().GUID;
                                    }
                                    if (targetGUID != null)
                                    {
                                        if (_runningDic.TryGetValue(targetGUID, out var value))
                                        {
                                            value.ActiveSourceWindow();
                                            _lastProcessGUID = targetGUID;
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }
        #endregion
        private async void Init()
        {
            StopGameMonitor();
            var allProcesses = Process.GetProcesses();
            if(allProcesses.NotNullOrEmpty())
            {
                ObservableCollection<ProcessInfo> targetProcesses = new ObservableCollection<ProcessInfo>();
                var keywords = ProcessName.Split(',');
                await Task.Run(() =>
                {
                    foreach (var process in allProcesses)
                    {
                        foreach (var keyword in keywords)
                        {
                            if (process.ProcessName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                            {
                                if(process.MainWindowHandle != IntPtr.Zero)
                                {
                                    string title = WinCore.FindWindowHelper.GetWindowTitle(process.MainWindowHandle);
                                    ProcessInfo processInfo = new ProcessInfo()
                                    {
                                        MainWindowHandle = process.MainWindowHandle,
                                        ProcessName = process.ProcessName,
                                        WindowTitle = title,
                                        Process = process
                                    };
                                    targetProcesses.Add(processInfo);
                                }
                                break;
                            }
                        }
                    }
                });
                if(Processes.NotNullOrEmpty() && targetProcesses.NotNullOrEmpty())
                {
                    //保留当前运行中的进程
                    var runnings = Processes.Where(p => p.Running).ToList();
                    Processes.Clear();
                    if (runnings.NotNullOrEmpty())//存在运行中，
                    {
                        //将运行中从新增里排除
                        foreach (var running in runnings)
                        {
                            var item = targetProcesses.FirstOrDefault(p=>p.MainWindowHandle == running.MainWindowHandle);
                            if(item != null)
                            {
                                targetProcesses.Remove(item);
                            }
                            Processes.Add(running);
                        }
                    }
                    if(targetProcesses.Any())
                    {
                        foreach(var item in targetProcesses)
                        {
                            Processes.Add(item);
                        }    
                    }
                }
                else
                {
                    if(targetProcesses.NotNullOrEmpty())
                    {
                        Processes = targetProcesses;
                    }
                    else
                    {
                        TryClearProcesses();
                    }
                }
            }
            else
            {
                TryClearProcesses();
            }
            StartGameMonitor();
        }
        private void TryClearProcesses()
        {
            if (Processes.NotNullOrEmpty())
            {
                var runnings = Processes.Where(p => p.Running).ToList();
                if (runnings.NotNullOrEmpty())
                {
                    Processes.Clear();
                    foreach (var item in runnings)
                    {
                        Processes.Add(item);
                    }
                }
                else
                {
                    Processes.Clear();
                }
            }
            else
            {
                Processes = null;
            }
        }

        public ICommand RefreshProcessListCommand => new RelayCommand(() =>
        {
            Init();
        });

        public ICommand StartCommand => new RelayCommand(() =>
        {
            foreach(var p in Processes)
            {
                GamePreviewWindow window = new GamePreviewWindow(p.GUID);
                window.OnStop += Window_OnStop;
                _runningDic.Add(p.GUID, window);
                window.Start(p.MainWindowHandle);
            }
            Running = _runningDic.Count != 0;
        });

        private void Window_OnStop(string id)
        {
            _runningDic.Remove(id);
        }

        public ICommand StopCommand => new RelayCommand(() =>
        {
            StopAll();
        });
        public void StopAll()
        {
            foreach (var p in Processes)
            {
                if (_runningDic.TryGetValue(p.GUID, out var window))
                {
                    _runningDic.Remove(p.GUID);
                    window.Stop();
                    Running = _runningDic.Count != 0;
                }
            }
        }
        #region 监控游戏关闭
        private Timer gameMonitor;
        private void StartGameMonitor()
        {
            if(Processes?.Count != 0)
            {
                if(gameMonitor == null)
                {
                    gameMonitor = new Timer()
                    {
                        Interval = 500,
                        AutoReset = false,
                    };
                    gameMonitor.Elapsed += GameMonitor_Elapsed;
                }
                gameMonitor.Start();
            }
        }
        private void StopGameMonitor()
        {
            gameMonitor?.Stop();
        }
        private void GameMonitor_Elapsed(object sender, ElapsedEventArgs e)
        {
            if(processes.NotNullOrEmpty())
            {
                List<ProcessInfo> exited = new List<ProcessInfo>();
                foreach (var pro in processes)
                {
                    if (pro.Process.HasExited)
                    {
                        exited.Add(pro);
                    }
                }
                if(exited.Any())
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var pro in exited)
                        {
                            Processes.Remove(pro);
                            if (_runningDic.TryGetValue(pro.GUID, out var value))
                            {
                                _runningDic.Remove(pro.GUID);
                                pro.Setting.ProcessInfo = null;
                                pro.Setting = null;
                                value.Stop();
                            }
                            if (_runningDic.Count == 0)
                            {
                                Running = false;
                            }
                        }
                    });
                }
            }
            gameMonitor.Start();
        }
        #endregion

        /// <summary>
        /// 当前活动窗口变化
        /// </summary>
        /// <param name="hWnd"></param>
        private void Current_OnForegroundWindowChanged(IntPtr hWnd)
        {
            
        }

        public void Dispose()
        {
            StopAll();
            HotkeyService.OnKeyboardClicked -= HotkeyService_OnKeyboardClicked;
        }
    }
}
