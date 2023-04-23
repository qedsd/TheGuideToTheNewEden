using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Helpers;
using static TheGuideToTheNewEden.WinUI.Helpers.FindWindowHelper;
using TheGuideToTheNewEden.Core.Extensions;
using Microsoft.UI.Xaml.Media.Imaging;
using SqlSugar.DistributedSystem.Snowflake;
using System.Drawing;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using System.Diagnostics;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.WinUI.Wins;
using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json.Linq;
using System.Timers;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class GamePreviewMgrViewModel : BaseViewModel
    {
        private Core.Models.GamePreviews.PreviewSetting previewSetting;
        public Core.Models.GamePreviews.PreviewSetting PreviewSetting
        {
            get => previewSetting;
            set => SetProperty(ref previewSetting, value);
        }
        private Core.Models.GamePreviews.PreviewItem setting;
        public Core.Models.GamePreviews.PreviewItem Setting
        {
            get => setting;
            set
            {
                SetProperty(ref setting, value);
            }
        }
        private ObservableCollection<PreviewItem> settings;
        public ObservableCollection<PreviewItem> Settings
        {
            get => settings;
            set => SetProperty(ref settings, value);
        }
        private Core.Models.GamePreviews.PreviewItem selectedSetting;
        /// <summary>
        /// 标识保存列表被选中项，当前设置可能不在保存列表中，所以需要和Setting区分开
        /// </summary>
        public Core.Models.GamePreviews.PreviewItem SelectedSetting
        {
            get => selectedSetting;
            set
            {
                SetProperty(ref selectedSetting, value);
                if (value != null)
                {
                    Setting = value;
                }
                else
                {
                    Setting = new PreviewItem()
                    {
                        Name = SelectedProcess.GetCharacterName()
                    };
                }
            }
        }
        private ObservableCollection<ProcessInfo> processes;
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
                SetProperty(ref selectedProcess, value);
                SetProcess();
            }
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
        
        
        private static readonly string Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "GamePreviewSetting.json");
        /// <summary>
        /// key为ProcessInfo.Guid,进程唯一标识符，与角色名称、设置名称无关
        /// </summary>
        private Dictionary<string, GamePreviewWindow> _runningDic = new Dictionary<string, GamePreviewWindow>();
        public GamePreviewMgrViewModel()
        {
            ForegroundWindowService.Current.Start();
            ForegroundWindowService.Current.OnForegroundWindowChanged += Current_OnForegroundWindowChanged;
            if (System.IO.File.Exists(Path))
            {
                string json = System.IO.File.ReadAllText(Path);
                if (string.IsNullOrEmpty(json))
                {
                    PreviewSetting = new Core.Models.GamePreviews.PreviewSetting();
                }
                else
                {
                    PreviewSetting = JsonConvert.DeserializeObject<Core.Models.GamePreviews.PreviewSetting>(json);
                }
            }
            else
            {
                PreviewSetting = new Core.Models.GamePreviews.PreviewSetting();
            }
            if(PreviewSetting.PreviewItems.NotNullOrEmpty())
            {
                Settings = PreviewSetting.PreviewItems.ToObservableCollection();
                //foreach(var item in Settings)
                //{
                //    item.ProcessGUID = item.Name;
                //}
            }
            else
            {
                Settings = new ObservableCollection<PreviewItem>();
            }
            Init();
            HotkeyService.OnKeyboardClicked += HotkeyService_OnKeyboardClicked;
        }
        #region 切换快捷键
        private string _lastProcessGUID;
        private void HotkeyService_OnKeyboardClicked(List<Common.KeyboardHook.KeyboardInfo> keys)
        {
            if(keys.NotNullOrEmpty())
            {
                if (_runningDic.Count != 0 && !string.IsNullOrEmpty(PreviewSetting.SwitchHotkey))
                {
                    var keynames = PreviewSetting.SwitchHotkey.Split('+');
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
                            Debug.WriteLine($"首次激活第一个{_runningDic.First().Key}");
                            _runningDic.First().Value.ActiveSourceWindow();
                            _lastProcessGUID = _runningDic.First().Key;
                        }
                        else
                        {
                            for (int i = 0; i < _runningDic.Count; i++)
                            {
                                var item = _runningDic.ElementAt(i);
                                if (item.Key == _lastProcessGUID)
                                {
                                    KeyValuePair<string, GamePreviewWindow> show;
                                    if (i != _runningDic.Count - 1)
                                    {
                                        show = _runningDic.ElementAt(i + 1);
                                        Debug.WriteLine($"激活下一个{show.Key}");
                                    }
                                    else
                                    {
                                        show = _runningDic.First();
                                        Debug.WriteLine($"激活下一轮第一个{show.Key}");
                                    }
                                    if (show.Key != null)
                                    {
                                        _lastProcessGUID = show.Key;
                                        show.Value.ActiveSourceWindow();
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
            Window?.ShowWaiting();
            StopGameMonitor();
            var allProcesses = Process.GetProcesses();
            if(allProcesses.NotNullOrEmpty())
            {
                ObservableCollection<ProcessInfo> targetProcesses = new ObservableCollection<ProcessInfo>();
                var keywords = PreviewSetting.ProcessKeywords.Split(',');
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
                                    string title = Helpers.FindWindowHelper.GetWindowTitle(process.MainWindowHandle);
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
            Window?.HideWaiting();
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

        /// <summary>
        /// 加载相应设置
        /// </summary>
        private void SetProcess()
        {
            if (_lastHighlightWindow != null)
            {
                _lastHighlightWindow.CancelHighlight();
                _lastHighlightWindow = null;
            }
            if (SelectedProcess != null)
            {
                SettingVisible = Visibility.Visible;
                if(SelectedProcess.Setting != null)//运行中
                {
                    Setting = SelectedProcess.Setting;
                    SelectedSetting = Settings.Contains(Setting) ? Setting : null;
                    _lastHighlightWindow = null;
                    if (_runningDic.TryGetValue(SelectedProcess.GUID,out var window))
                    {
                        window.Highlight();
                        _lastHighlightWindow = window;
                    }
                }
                else //进程未运行中
                {
                    var name = SelectedProcess.GetCharacterName();
                    if (!string.IsNullOrEmpty(name))
                    {
                        //从保存列表里找出第一个同名且不在运行中的设置
                        var targetSetting = Settings.FirstOrDefault(p => p.Name == name && p.ProcessInfo == null);
                        if (targetSetting != null)
                        {
                            Setting = targetSetting;
                            SelectedSetting = targetSetting;
                        }
                        else//没有找到则新建
                        {
                            Setting = new PreviewItem()
                            {
                                Name = SelectedProcess.GetCharacterName()
                            };
                            SelectedSetting = null;
                        }
                    }
                    else//如果不设置名称，不会保存，直接新建
                    {
                        Setting = new PreviewItem()
                        {
                            Name = SelectedProcess.GetCharacterName()
                        };
                        SelectedSetting = null;
                    }
                }
            }
            else
            {
                SettingVisible = Visibility.Collapsed;
            }
        }

        public ICommand RefreshProcessListCommand => new RelayCommand(() =>
        {
            Init();
        });

        public ICommand StartCommand => new RelayCommand(() =>
        {
            if(Setting != null)
            {
                try
                {
                    GamePreviewWindow gamePreviewWindow = new GamePreviewWindow(Setting);
                    if (_runningDic.TryAdd(SelectedProcess.GUID, gamePreviewWindow))
                    {
                        gamePreviewWindow.OnSettingChanged += GamePreviewWindow_OnSettingChanged;
                        gamePreviewWindow.OnStop += GamePreviewWindow_OnStop;
                        gamePreviewWindow.Start(SelectedProcess.MainWindowHandle);
                        SelectedProcess.Setting = Setting;
                        Setting.ProcessInfo = SelectedProcess;
                        SelectedProcess.Running = true;
                        SelectedProcess.SettingName = Setting.Name;
                        Running = true;
                        //保存
                        if (!string.IsNullOrEmpty(Setting.Name))
                        {
                            if(!PreviewSetting.PreviewItems.Contains(Setting))
                            {
                                PreviewSetting.PreviewItems.Add(Setting);
                            }
                            if(!Settings.Contains(Setting))
                            {
                                Settings.Add(Setting);
                            }
                            SelectedSetting = Setting;
                            SaveSetting();
                        }
                        else
                        {
                            Window.ShowMsg("设置名称为空，将不保存设置");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    Window.ShowError(ex.Message, false);
                }
            }
        });

        private void GamePreviewWindow_OnStop(PreviewItem previewItem)
        {
            Stop(previewItem);
        }

        private void GamePreviewWindow_OnSettingChanged(PreviewItem previewItem)
        {
            SaveSetting();
        }
        private void SaveSetting()
        {
            if (!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(Path)))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            }
            string json = JsonConvert.SerializeObject(PreviewSetting);
            System.IO.File.WriteAllText(Path,json);
        }

        public ICommand StopCommand => new RelayCommand(() =>
        {
            Stop(Setting);
        });
        private void Stop(PreviewItem previewItem)
        {
            if(previewItem == null || previewItem.ProcessInfo == null)
            {
                return;
            }
            if (_runningDic.TryGetValue(previewItem.ProcessInfo.GUID, out var window))
            {
                _runningDic.Remove(previewItem.ProcessInfo.GUID);
                previewItem.ProcessInfo.Running = false;
                previewItem.ProcessInfo.Setting = null;
                previewItem.ProcessInfo = null;
                window.Stop();
                if (_runningDic.Count == 0)
                {
                    Running = false;
                }
            }
            else
            {
                Window.ShowError("不存在目标进程窗口");
            }
        }
        public ICommand NewSettingCommand => new RelayCommand(() =>
        {
            SelectedSetting = null;
        });
        public ICommand RemoveSettingCommand => new RelayCommand<PreviewItem>((item) =>
        {
            if(item != null)
            {
                if(Settings.Remove(item))
                {
                    if(PreviewSetting.PreviewItems.Remove(item))
                    {
                        SaveSetting();
                        if(item == SelectedSetting)
                        {
                            SelectedSetting = null;
                        }
                    }
                }
            }
        });
        public void StopAll()
        {
            foreach(var window in _runningDic.Values)
            {
                window.Stop();
            }
        }
        public ICommand SetUniformSizeCommand => new RelayCommand(() =>
        {
            if(!_runningDic.NotNullOrEmpty())
            {
                Window.ShowError("无激活窗口", true);
            }
            else
            {
                foreach (var window in _runningDic.Values)
                {
                    window.SetSize(PreviewSetting.UniformWidth, PreviewSetting.UniformHeight);
                }
                Window.ShowSuccess("已应用窗口尺寸");
            }
        });
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
                    Window.DispatcherQueue.TryEnqueue(() =>
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
                                if(SelectedProcess == pro)
                                {
                                    SelectedProcess.Running = false;
                                    SelectedProcess = null;
                                }
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

        private GamePreviewWindow _lastHighlightWindow;
        /// <summary>
        /// 当前活动窗口变化
        /// </summary>
        /// <param name="hWnd"></param>
        private void Current_OnForegroundWindowChanged(IntPtr hWnd)
        {
            if (_lastHighlightWindow != null)
            {
                _lastHighlightWindow.CancelHighlight();
            }
            var targetProcess = Processes.FirstOrDefault(p=>p.Running && p.MainWindowHandle == hWnd);
            if(targetProcess != null)
            {                
                foreach(var item in _runningDic)
                {
                    if(item.Key == targetProcess.GUID)
                    {
                        if(item.Value.Setting.HideOnForeground)
                        {
                            item.Value.Hide2();
                        }
                        else if(item.Value.Setting.Highlight)
                        {
                            item.Value.Highlight();
                            _lastHighlightWindow = item.Value;
                        }
                    }
                    else
                    {
                        item.Value.Show2();
                    }
                }
            }
            else
            {
                foreach (var item in _runningDic)
                {
                    item.Value.Show2();
                }
            }
        }

        #region 自动窗口布局
        
        public ICommand SetAutoLayoutCommand => new RelayCommand(() =>
        {
            UpdateAutoLayout();
        });
        private void UpdateAutoLayout()
        {
            if(!Running)
            {
                Window.ShowError("请先开始当前进程预览", true);
                return;
            }
            if(PreviewSetting.AutoLayout < 0)
            {
                Window.ShowError("请选择对齐方式", true);
                return;
            }
            if (PreviewSetting.AutoLayoutAnchor < 0)
            {
                Window.ShowError("请选择对齐位置", true);
                return;
            }
            if(_runningDic.TryGetValue(Setting.ProcessInfo.GUID, out var window))
            {
                switch(PreviewSetting.AutoLayout)
                {
                    case 0: SetAutoLayout1(window);break;
                    case 1: SetAutoLayout2(window); break;
                    case 2: SetAutoLayout3(window); break;
                    case 3: SetAutoLayout4(window); break;
                }
                Window.ShowSuccess("已应用对齐布局");
            }
        }
        /// <summary>
        /// 左对齐
        /// </summary>
        private void SetAutoLayout1(GamePreviewWindow targetWindow)
        {
            targetWindow.GetSizeAndPos(out int targetWinX, out int targetWinY, out int targetWinW, out int targetWinH);
            int startX = targetWinX + targetWinW + PreviewSetting.AutoLayoutSpan;
            foreach (var win in _runningDic.Values)
            {
                if(win == targetWindow)
                {
                    continue;
                }
                int startY = 0;
                switch(PreviewSetting.AutoLayoutAnchor)
                {
                    case 0: startY = targetWinY; break;//上对齐
                    case 1: startY = targetWinY + targetWinH / 2 - win.GetHeight() / 2; break;//中对齐
                    case 2: startY = targetWinY + targetWinH - win.GetHeight(); break;//下对齐
                }
                win.SetPos(startX, startY);
                startX += win.GetWidth() + PreviewSetting.AutoLayoutSpan;
            }
        }
        /// <summary>
        /// 右对齐
        /// </summary>
        private void SetAutoLayout2(GamePreviewWindow targetWindow)
        {
            targetWindow.GetSizeAndPos(out int targetWinX, out int targetWinY, out int targetWinW, out int targetWinH);
            int startX = targetWinX - PreviewSetting.AutoLayoutSpan;
            foreach (var win in _runningDic.Values)
            {
                if (win == targetWindow)
                {
                    continue;
                }
                startX = startX - win.GetWidth() - PreviewSetting.AutoLayoutSpan;
                int startY = 0;
                switch (PreviewSetting.AutoLayoutAnchor)
                {
                    case 0: startY = targetWinY; break;//上对齐
                    case 1: startY = targetWinY + targetWinH / 2 - win.GetHeight() / 2; break;//中对齐
                    case 2: startY = targetWinY + targetWinH - win.GetHeight(); break;//下对齐
                }
                win.SetPos(startX, startY);
            }
        }
        /// <summary>
        /// 上对齐
        /// </summary>
        private void SetAutoLayout3(GamePreviewWindow targetWindow)
        {
            targetWindow.GetSizeAndPos(out int targetWinX, out int targetWinY, out int targetWinW, out int targetWinH);
            int startY = targetWinY + targetWinH + PreviewSetting.AutoLayoutSpan;
            foreach (var win in _runningDic.Values)
            {
                if (win == targetWindow)
                {
                    continue;
                }
                int startX = 0;
                switch (PreviewSetting.AutoLayoutAnchor)
                {
                    case 0: startX = targetWinX; break;//左对齐
                    case 1: startX = (targetWinX + targetWinW / 2) - win.GetWidth() / 2; break;//中对齐
                    case 2: startX = (targetWinX + targetWinW) - win.GetWidth(); break;//右对齐
                }
                win.SetPos(startX, startY);
                startY += win.GetHeight() + PreviewSetting.AutoLayoutSpan;
            }
        }
        /// <summary>
        /// 下对齐
        /// </summary>
        private void SetAutoLayout4(GamePreviewWindow targetWindow)
        {
            targetWindow.GetSizeAndPos(out int targetWinX, out int targetWinY, out int targetWinW, out int targetWinH);
            int startY = targetWinY - PreviewSetting.AutoLayoutSpan;
            foreach (var win in _runningDic.Values)
            {
                if (win == targetWindow)
                {
                    continue;
                }
                startY = startY - win.GetHeight() - PreviewSetting.AutoLayoutSpan;
                int startX = 0;
                switch (PreviewSetting.AutoLayoutAnchor)
                {
                    case 0: startX = targetWinX; break;//左对齐
                    case 1: startX = (targetWinX + targetWinW / 2) - win.GetWidth() / 2; break;//中对齐
                    case 2: startX = (targetWinX + targetWinW) - win.GetWidth(); break;//右对齐
                }
                win.SetPos(startX, startY);
            }
        }
        #endregion
    }
}
