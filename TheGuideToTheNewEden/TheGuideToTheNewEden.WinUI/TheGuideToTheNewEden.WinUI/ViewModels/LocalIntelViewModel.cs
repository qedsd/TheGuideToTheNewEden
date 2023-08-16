using CommunityToolkit.Mvvm.Input;
using ESI.NET.Models.Fleets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Wins;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    public class LocalIntelViewModel:BaseViewModel
    {
        private ObservableCollection<ProcessInfo> processes = new ObservableCollection<ProcessInfo>();
        public ObservableCollection<ProcessInfo> Processes
        {
            get => processes;
            set => SetProperty(ref processes, value);
        }

        private ProcessInfo selectedProcess;
        public ProcessInfo SelectedProcess
        {
            get => selectedProcess;
            set
            {
                if (SetProperty(ref selectedProcess, value))
                {
                    SetProcess();
                }
            }
        }

        public LocalIntelSetting Setting { get; set; }

        private LocalIntelProcSetting procSetting;
        public LocalIntelProcSetting ProcSetting
        {
            get => procSetting;
            set => SetProperty(ref procSetting, value);
        }

        private bool running;
        public bool Running
        {
            get => running;
            set => SetProperty(ref running, value);
        }
        private readonly Dictionary<string, LocalIntelProcSetting> _runningDic = new Dictionary<string, LocalIntelProcSetting>();
        private static readonly string SettingFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "LocalIntelSetting.json");
        public LocalIntelViewModel()
        {
            Init();
        }
        private void Init()
        {
            if (System.IO.File.Exists(SettingFilePath))
            {
                string json = System.IO.File.ReadAllText(SettingFilePath);
                if (string.IsNullOrEmpty(json))
                {
                    Setting = new LocalIntelSetting();
                }
                else
                {
                    Setting = JsonConvert.DeserializeObject<LocalIntelSetting>(json);
                }
            }
            else
            {
                Setting = new LocalIntelSetting();
            }
            GetProcesses();
        }
        private async void GetProcesses()
        {
            var allProcesses = Process.GetProcesses();
            if (allProcesses.NotNullOrEmpty())
            {
                List<ProcessInfo> targetProcesses = new List<ProcessInfo>();
                //获取所有目标进程
                await Task.Run(() =>
                {
                    foreach (var process in allProcesses.Where(p => p.ProcessName == "exefile").ToList())
                    {
                        if (process.MainWindowHandle != IntPtr.Zero)
                        {
                            ProcessInfo processInfo = new ProcessInfo()
                            {
                                MainWindowHandle = process.MainWindowHandle,
                                ProcessName = process.ProcessName,
                                WindowTitle = process.MainWindowTitle,
                                Process = process
                            };
                            targetProcesses.Add(processInfo);
                        }
                    }
                });
                if (targetProcesses.NotNullOrEmpty())
                {
                    List<ProcessInfo> targetProcessesForShow;//最终要显示的目标进程
                    if (Processes.NotNullOrEmpty())//当前列表不为空，需要保留运行中的进程
                    {
                        targetProcessesForShow = new List<ProcessInfo>();
                        var runnings = Processes.Where(p => p.Running).ToList();
                        if (runnings.NotNullOrEmpty())//存在运行中，
                        {
                            //将运行中从新增里排除
                            foreach (var running in runnings)
                            {
                                var item = targetProcesses.FirstOrDefault(p => p.MainWindowHandle == running.MainWindowHandle);
                                if (item != null)
                                {
                                    targetProcesses.Remove(item);
                                }
                                targetProcessesForShow.Add(running);
                            }
                        }
                        if (targetProcesses.Any())//添加未运行中的
                        {
                            foreach (var item in targetProcesses)
                            {
                                targetProcessesForShow.Add(item);
                            }
                        }
                    }
                    else//当前列表为空，保存显示全部
                    {
                        targetProcessesForShow = targetProcesses;
                    }
                    Processes.Clear();
                    foreach (var item in targetProcessesForShow)
                    {
                        Processes.Add(item);
                    }
                }
            }
            else
            {
                Processes.Clear();
            }
        }
        private void SetProcess()
        {
            var target = Setting.ProcSettings.FirstOrDefault(p => p.Name == SelectedProcess.WindowTitle);
            ProcSetting = target != null ? target : new LocalIntelProcSetting();
        }

        private SelecteCaptureAreaWindow _currentSelecteCaptureAreaWindow;
        public ICommand StartCommand => new RelayCommand(() =>
        {
            _currentSelecteCaptureAreaWindow = new SelecteCaptureAreaWindow(SelectedProcess.MainWindowHandle);
            _currentSelecteCaptureAreaWindow.Activate();
            _currentSelecteCaptureAreaWindow.CroppedRegionChanged += SelecteCaptureAreaWindow_CroppedRegionChanged;
        });

        public ICommand StopCommand => new RelayCommand(() =>
        {
            _currentSelecteCaptureAreaWindow.Close();
        });

        public ICommand StartAllCommand => new RelayCommand(() =>
        {

        });

        public ICommand StopAllCommand => new RelayCommand(() =>
        {
            
        });
        public ICommand RefreshProcessListCommand => new RelayCommand(() =>
        {
            GetProcesses();
        });


        private void SelecteCaptureAreaWindow_CroppedRegionChanged(Windows.Foundation.Rect rect)
        {
            Debug.WriteLine($"裁剪区域 {rect.X} {rect.Y} {rect.Width} {rect.Height}");
        }
    }
}
