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
using System.Xml.Linq;
using TheGuideToTheNewEden.Core;
using WinUIEx;
using TheGuideToTheNewEden.WinUI.Interfaces;

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
                //else
                //{
                //    Setting = new PreviewItem()
                //    {
                //        Name = SelectedProcess.GetCharacterName()
                //    };
                //}
            }
        }
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
                if(SetProperty(ref selectedProcess, value))
                {
                    SetProcess();
                }
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
        private readonly Dictionary<string, IGamePreviewWindow> _runningDic = new Dictionary<string, IGamePreviewWindow>();
        public GamePreviewMgrViewModel()
        {
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
            StartHotkey();
        }
        #region 切换快捷键
        private int _forwardHotkeyRegisterId;
        private int _backwardHotkeyRegisterId;
        private void StartHotkey()
        {
            bool result1 = StartForwardHotkey();
            bool result2 = StartBackwardHotkey();
            if(result1 && result2)
            {
                Window.ShowSuccess($"注册切换热键成功");
            }
            else if(!result1 && !result2)
            {
                Window.ShowSuccess($"注册切换热键失败");
            }
            else if(result1)
            {
                Window.ShowSuccess($"注册切换热键{PreviewSetting.SwitchHotkey_Forward}成功");
            }
            else if(result2)
            {
                Window.ShowSuccess($"注册切换热键{PreviewSetting.SwitchHotkey_Backward}成功");
            }
        }
        private bool StartForwardHotkey()
        {
            Core.Log.Info($"向前全局切换快捷键{PreviewSetting.SwitchHotkey_Forward}");
            if (string.IsNullOrEmpty(PreviewSetting.SwitchHotkey_Forward))
            {
                return false;
            }
            if(HotkeyService.GetHotkeyService(Window.GetWindowHandle()).Register(PreviewSetting.SwitchHotkey_Forward, out _forwardHotkeyRegisterId))
            {
                Core.Log.Info("注册热键成功");
                Window.ShowSuccess($"注册热键{PreviewSetting.SwitchHotkey_Forward}成功");
                KeyboardService.OnKeyboardClicked -= HotkeyService_OnKeyboardClicked;
                KeyboardService.OnKeyboardClicked += HotkeyService_OnKeyboardClicked;
                return true;
            }
            else
            {
                Core.Log.Info("注册热键失败");
                Window.ShowError($"注册热键{PreviewSetting.SwitchHotkey_Forward}失败，请检查是否按键冲突");
                return false;
            }
        }
        private bool StartBackwardHotkey()
        {
            Core.Log.Info($"向后全局切换快捷键{PreviewSetting.SwitchHotkey_Backward}");
            if (string.IsNullOrEmpty(PreviewSetting.SwitchHotkey_Backward))
            {
                return false;
            }
            if (HotkeyService.GetHotkeyService(Window.GetWindowHandle()).Register(PreviewSetting.SwitchHotkey_Backward, out _backwardHotkeyRegisterId))
            {
                Core.Log.Info("注册热键成功");
                Window.ShowSuccess($"注册热键{PreviewSetting.SwitchHotkey_Backward}成功");
                KeyboardService.OnKeyboardClicked -= HotkeyService_OnKeyboardClicked;
                KeyboardService.OnKeyboardClicked += HotkeyService_OnKeyboardClicked;
                return true;
            }
            else
            {
                Core.Log.Info("注册热键失败");
                Window.ShowError($"注册热键{PreviewSetting.SwitchHotkey_Backward}失败，请检查是否按键冲突");
                return false;
            }
        }
        public ICommand SetForwardHotkeyCommand => new RelayCommand(() =>
        {
            if(string.IsNullOrEmpty(PreviewSetting.SwitchHotkey_Forward))
            {
                if (_forwardHotkeyRegisterId > 0)//先注销原本热键
                {
                    HotkeyService.GetHotkeyService(Window.GetWindowHandle()).Unregister(_forwardHotkeyRegisterId);
                    _forwardHotkeyRegisterId = -1;
                    Window.ShowSuccess($"已注销热键{PreviewSetting.SwitchHotkey_Forward}");
                }
                return;
            }
            if(HotkeyService.TryGetHotkeyVK(PreviewSetting.SwitchHotkey_Forward,out _,out _))
            {
                if (_forwardHotkeyRegisterId > 0)//先注销原本热键
                {
                    HotkeyService.GetHotkeyService(Window.GetWindowHandle()).Unregister(_forwardHotkeyRegisterId);
                }
                StartForwardHotkey();
            }
            else
            {
                Window.ShowError($"不规范热键");
            }
        });
        public ICommand SetBackwardHotkeyCommand => new RelayCommand(() =>
        {
            if (string.IsNullOrEmpty(PreviewSetting.SwitchHotkey_Backward))
            {
                if (_backwardHotkeyRegisterId > 0)//先注销原本热键
                {
                    HotkeyService.GetHotkeyService(Window.GetWindowHandle()).Unregister(_backwardHotkeyRegisterId);
                    _backwardHotkeyRegisterId = -1;
                    Window.ShowSuccess($"已注销热键{PreviewSetting.SwitchHotkey_Backward}");
                }
                return;
            }
            if (HotkeyService.TryGetHotkeyVK(PreviewSetting.SwitchHotkey_Backward, out _, out _))
            {
                if (_backwardHotkeyRegisterId > 0)//先注销原本热键
                {
                    HotkeyService.GetHotkeyService(Window.GetWindowHandle()).Unregister(_backwardHotkeyRegisterId);
                }
                StartBackwardHotkey();
            }
            else
            {
                Window.ShowError($"不规范热键");
            }
        });
        private string _lastProcessGUID;
        private void HotkeyService_OnKeyboardClicked(List<Common.KeyboardHook.KeyboardInfo> keys)
        {
            //限定按键顺序
            if (keys.NotNullOrEmpty())
            {
                if (_runningDic.Any())
                {
                    string formatHotkey = keys.First().Name;
                    foreach(var key in keys.Skip(1))
                    {
                        formatHotkey += '+';
                        formatHotkey += key.Name;
                    }
                    if(PreviewSetting.SwitchHotkey_Forward.Equals(formatHotkey, StringComparison.OrdinalIgnoreCase))//向前进切换
                    {
                        if (_lastProcessGUID == null)
                        {
                            var firstRunning = Processes.FirstOrDefault(p => p.Running);
                            if (firstRunning != null)
                            {
                                if (_runningDic.TryGetValue(firstRunning.GUID, out var value))
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
                                    //上一次激活的窗口不是最后一个窗口，则依序激活下一个
                                    //是最后一个窗口则激活第一个
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
                    else if(PreviewSetting.SwitchHotkey_Backward.Equals(formatHotkey, StringComparison.OrdinalIgnoreCase))//向后退切换
                    {
                        if (_lastProcessGUID == null)
                        {
                            var firstRunning = Processes.FirstOrDefault(p => p.Running);
                            if (firstRunning != null)
                            {
                                if (_runningDic.TryGetValue(firstRunning.GUID, out var value))
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
                                    //上一次激活的窗口第一个窗口，则依序激活上一个
                                    //是第一个窗口则激活最后一个
                                    if (i != 0)
                                    {
                                        targetGUID = runnings[i - 1].GUID;
                                    }
                                    else
                                    {
                                        targetGUID = runnings.Last().GUID;
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
            Window?.ShowWaiting();
            Processes.CollectionChanged -= Processes_CollectionChanged;
            StopGameMonitor();
            var allProcesses = Process.GetProcesses();
            if(allProcesses.NotNullOrEmpty())
            {
                List<ProcessInfo> targetProcesses = new List<ProcessInfo>();
                var keywords = PreviewSetting.ProcessKeywords.Split(',');
                //获取所有目标进程
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
                if(targetProcesses.NotNullOrEmpty())
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
                    //排序
                    if(PreviewSetting.ProcessOrder.NotNullOrEmpty())
                    {
                        foreach(var item in targetProcessesForShow)
                        {
                            int index = 0;
                            for (; index < PreviewSetting.ProcessOrder.Length; index++)
                            {
                                if (PreviewSetting.ProcessOrder[index] == item.WindowTitle)
                                {
                                    break;
                                }
                            }
                            if(index < PreviewSetting.ProcessOrder.Length)
                            {
                                item.Sort = index;
                            }
                            else
                            {
                                item.Sort = int.MaxValue;
                            }
                        }
                        targetProcessesForShow = targetProcessesForShow.OrderBy(p => p.Sort).ToList();
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
                TryClearProcesses();
            }
            Processes.CollectionChanged += Processes_CollectionChanged;
            StartGameMonitor();
            Window?.HideWaiting();
        }

        private void Processes_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if(e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                SaveOrder();
            }
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
        }
        private IGamePreviewWindow _lastHighlightWindow;
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
                    if (_runningDic.TryGetValue(SelectedProcess.GUID,out var window))
                    {
                        window.Highlight();
                        _lastHighlightWindow = window;
                    }
                }
                else //进程未运行中
                {
                    var characterName = SelectedProcess.GetCharacterName();
                    if (!string.IsNullOrEmpty(characterName))
                    {
                        //从保存列表里找出第一个同名且不在运行中的设置
                        var targetSetting = Settings.FirstOrDefault(p => p.Name == characterName && p.ProcessInfo == null);
                        if (targetSetting != null)
                        {
                            Setting = targetSetting;
                            SelectedSetting = targetSetting;
                        }
                        else//没有找到则新建
                        {
                            Setting = new PreviewItem()
                            {
                                Name = characterName,
                                UserName = SelectedProcess.GetUserName()
                            };
                            SelectedSetting = null;
                        }
                    }
                    else
                    {
                        var username = SelectedProcess.GetUserName();
                        if(!string.IsNullOrEmpty(username))//没有角色名则找账号名
                        {
                            //从保存列表里找出第一个同名且不在运行中的设置
                            var targetSetting = Settings.FirstOrDefault(p => p.UserName == username && p.ProcessInfo == null);
                            if (targetSetting != null)
                            {
                                Setting = targetSetting;
                                SelectedSetting = targetSetting;
                            }
                            else//没有找到则新建
                            {
                                Setting = new PreviewItem()
                                {
                                    Name = username,
                                    UserName = username
                                };
                                SelectedSetting = null;
                            }
                        }
                        else//如果不设置名称，不会保存，直接新建
                        {
                            Setting = new PreviewItem();
                            SelectedSetting = null;
                        }
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
                    IGamePreviewWindow gamePreviewWindow = new GamePreviewWindow2(Setting, PreviewSetting);
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
            PreviewSetting.ProcessOrder = Processes.Select(p => p.WindowTitle).ToArray();
            string json = JsonConvert.SerializeObject(PreviewSetting);
            System.IO.File.WriteAllText(Path,json);
        }

        public void SaveOrder()
        {
            SaveSetting();
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
            var list = Processes?.Where(p => p.Running).ToList();
            if (list.NotNullOrEmpty())
            {
                foreach (var item in Processes.Where(p => p.Running).ToList())
                {
                    if (_runningDic.TryGetValue(item.GUID, out var window))
                    {
                        _runningDic.Remove(item.GUID);
                        item.Running = false;
                        item.Setting = null;
                        window.Stop();
                        if (_runningDic.Count == 0)
                        {
                            Running = false;
                        }
                    }
                }
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
                SaveSetting();
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

        /// <summary>
        /// 当前活动窗口变化
        /// </summary>
        /// <param name="hWnd"></param>
        private void Current_OnForegroundWindowChanged(IntPtr hWnd)
        {
            var targetProcess = Processes?.FirstOrDefault(p=>p.Running && p.MainWindowHandle == hWnd);
            foreach (var item in _runningDic)
            {
                if (targetProcess != null && item.Key == targetProcess.GUID)
                {
                    if (item.Value.IsHideOnForeground())
                    {
                        item.Value.HideWindow();
                    }
                    else if (item.Value.IsHighlight())
                    {
                        _lastHighlightWindow = item.Value;
                        item.Value.Highlight();
                        item.Value.ShowWindow();
                    }
                    _lastProcessGUID = targetProcess.GUID;
                }
                else
                {
                    item.Value.CancelHighlight();
                    item.Value.ShowWindow();
                }
            }
        }

        #region 自动窗口布局
        
        public ICommand SetAutoLayoutCommand => new RelayCommand(() =>
        {
            UpdateAutoLayout();
            SaveSetting();
        });
        private void UpdateAutoLayout()
        {
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
            if(_runningDic.Count < 2)
            {
                Window.ShowError("请激活至少两个预览窗口", true);
                return;
            }

            if (_runningDic.TryGetValue(Processes.First(p => p.Running).GUID, out var window))
            {
                switch (PreviewSetting.AutoLayout)
                {
                    case 0: SetAutoLayout1(window); break;
                    case 1: SetAutoLayout2(window); break;
                    case 2: SetAutoLayout3(window); break;
                    case 3: SetAutoLayout4(window); break;
                }
                Window.ShowSuccess("已应用对齐布局");
            }
            else
            {
                Window.ShowError("未找到第一个运行窗口");
            }
            
        }
        /// <summary>
        /// 左对齐
        /// </summary>
        private void SetAutoLayout1(IGamePreviewWindow targetWindow)
        {
            //换行仅适配主屏幕位于所有屏幕左上角位置的情况
            targetWindow.GetSizeAndPos(out int targetWinX, out int targetWinY, out int targetWinW, out int targetWinH);
            int startX = targetWinX + targetWinW + PreviewSetting.AutoLayoutSpan;
            int refY = targetWinY;//参考y，默认为targetWindow的y，如果换行后，则为换行后的行首个窗口y
            int yWrap = 1;//y换行方向，默认向下换行
            int lineCount = 1;
            foreach (var win in _runningDic.Values)
            {
                if (win == targetWindow)
                {
                    continue;
                }
                lineCount++;
                if (!Helpers.WindowHelper.IsInWindow(startX + win.GetWidth(), refY) || (PreviewSetting.AutoLayoutCount > 0 && lineCount > PreviewSetting.AutoLayoutCount))//x超出屏幕范围或达到最大个数，换行
                {
                    startX = targetWinX;//x回到起始位置
                    int newRefY = refY + (win.GetHeight() + PreviewSetting.AutoLayoutSpan) * yWrap;//按默认或上一次换行方向换行后的y
                    if(!Helpers.WindowHelper.IsInWindow(startX + win.GetWidth(), newRefY))
                    {
                        yWrap = -yWrap;//超出y范围，调整换行方向，不考虑第三次更换行方向的情况
                        newRefY = refY + (win.GetHeight() + PreviewSetting.AutoLayoutSpan) * yWrap;
                    }
                    refY = newRefY;
                    lineCount = 1;
                }
                int startY = 0;
                switch(PreviewSetting.AutoLayoutAnchor)
                {
                    case 0: startY = refY; break;//上对齐
                    case 1: startY = refY + targetWinH / 2 - win.GetHeight() / 2; break;//中对齐
                    case 2: startY = refY + targetWinH - win.GetHeight(); break;//下对齐
                }
                win.SetPos(startX, startY);
                startX += win.GetWidth() + PreviewSetting.AutoLayoutSpan;
            }
        }
        /// <summary>
        /// 右对齐
        /// </summary>
        private void SetAutoLayout2(IGamePreviewWindow targetWindow)
        {
            //换行仅适配主屏幕位于所有屏幕左上角位置的情况
            targetWindow.GetSizeAndPos(out int targetWinX, out int targetWinY, out _, out int targetWinH);
            int startX = targetWinX;
            int refY = targetWinY;//参考y，默认为targetWindow的y，如果换行后，则为换行后的行首个窗口y
            int yWrap = 1;//y换行方向，默认向下换行
            int lineCount = 1;
            foreach (var win in _runningDic.Values)
            {
                if (win == targetWindow)
                {
                    continue;
                }
                lineCount++;
                startX = startX - win.GetWidth() - PreviewSetting.AutoLayoutSpan;
                if (!Helpers.WindowHelper.IsInWindow(startX,refY) || (PreviewSetting.AutoLayoutCount > 0 && lineCount > PreviewSetting.AutoLayoutCount))//x超出屏幕范围，换行
                {
                    startX = targetWinX;//x回到起始位置
                    int newRefY = refY + (win.GetHeight() + PreviewSetting.AutoLayoutSpan) * yWrap;//按默认或上一次换行方向换行后的y
                    if (!Helpers.WindowHelper.IsInWindow(startX, newRefY))
                    {
                        yWrap = -yWrap;//超出y范围，调整换行方向，不考虑第三次更换行方向的情况
                        newRefY = refY + (win.GetHeight() + PreviewSetting.AutoLayoutSpan) * yWrap;
                    }
                    refY = newRefY;
                    lineCount = 1;
                }
                int startY = 0;
                switch (PreviewSetting.AutoLayoutAnchor)
                {
                    case 0: startY = refY; break;//上对齐
                    case 1: startY = refY + targetWinH / 2 - win.GetHeight() / 2; break;//中对齐
                    case 2: startY = refY + targetWinH - win.GetHeight(); break;//下对齐
                }
                win.SetPos(startX, startY);
            }
        }
        /// <summary>
        /// 上对齐
        /// </summary>
        private void SetAutoLayout3(IGamePreviewWindow targetWindow)
        {
            //换行仅适配主屏幕位于所有屏幕左上角位置的情况
            targetWindow.GetSizeAndPos(out int targetWinX, out int targetWinY, out int targetWinW, out int targetWinH);
            int startY = targetWinY + targetWinH + PreviewSetting.AutoLayoutSpan;
            int refX = targetWinX;//参考x，默认为targetWindow的x，如果换行后，则为换行后的行首个窗口x
            int xWrap = 1;//x换行方向，默认向右换行
            int lineCount = 1;
            foreach (var win in _runningDic.Values)
            {
                if (win == targetWindow)
                {
                    continue;
                }
                lineCount++;
                if (!Helpers.WindowHelper.IsInWindow(refX, startY + win.GetHeight() + PreviewSetting.AutoLayoutSpan) || (PreviewSetting.AutoLayoutCount > 0 && lineCount > PreviewSetting.AutoLayoutCount))//y超出屏幕范围，换行
                {
                    startY = targetWinY;//y回到起始位置
                    int newRefX = refX + (win.GetWidth() + PreviewSetting.AutoLayoutSpan) * xWrap;//按默认或上一次换行方向换行后的x
                    if (!Helpers.WindowHelper.IsInWindow(newRefX, startY))
                    {
                        xWrap = -xWrap;//超出y范围，调整换行方向，不考虑第三次更换行方向的情况
                        newRefX = refX + (win.GetHeight() + PreviewSetting.AutoLayoutSpan) * xWrap;
                    }
                    refX = newRefX;
                    lineCount = 1;
                }
                int startX = 0;
                switch (PreviewSetting.AutoLayoutAnchor)
                {
                    case 0: startX = refX; break;//左对齐
                    case 1: startX = (refX + targetWinW / 2) - win.GetWidth() / 2; break;//中对齐
                    case 2: startX = (refX + targetWinW) - win.GetWidth(); break;//右对齐
                }
                win.SetPos(startX, startY);
                startY += win.GetHeight() + PreviewSetting.AutoLayoutSpan;
            }
        }
        /// <summary>
        /// 下对齐
        /// </summary>
        private void SetAutoLayout4(IGamePreviewWindow targetWindow)
        {
            //换行仅适配主屏幕位于所有屏幕左上角位置的情况
            targetWindow.GetSizeAndPos(out int targetWinX, out int targetWinY, out int targetWinW, out _);
            int startY = targetWinY;
            int refX = targetWinX;//参考x，默认为targetWindow的x，如果换行后，则为换行后的行首个窗口x
            int xWrap = 1;//x换行方向，默认向右换行
            int lineCount = 1;
            foreach (var win in _runningDic.Values)
            {
                if (win == targetWindow)
                {
                    continue;
                }
                lineCount++;
                startY = startY - win.GetHeight() - PreviewSetting.AutoLayoutSpan;
                if (!Helpers.WindowHelper.IsInWindow(refX, startY) || (PreviewSetting.AutoLayoutCount > 0 && lineCount > PreviewSetting.AutoLayoutCount))//y超出屏幕范围，换行
                {
                    startY = targetWinY;//y回到起始位置
                    int newRefX = refX + (win.GetWidth() + PreviewSetting.AutoLayoutSpan) * xWrap;//按默认或上一次换行方向换行后的x
                    if (!Helpers.WindowHelper.IsInWindow(newRefX, startY))
                    {
                        xWrap = -xWrap;//超出y范围，调整换行方向，不考虑第三次更换行方向的情况
                        newRefX = refX + (win.GetHeight() + PreviewSetting.AutoLayoutSpan) * xWrap;
                    }
                    refX = newRefX;
                    lineCount = 1;
                }
                int startX = 0;
                switch (PreviewSetting.AutoLayoutAnchor)
                {
                    case 0: startX = refX; break;//左对齐
                    case 1: startX = (refX + targetWinW / 2) - win.GetWidth() / 2; break;//中对齐
                    case 2: startX = (refX + targetWinW) - win.GetWidth(); break;//右对齐
                }
                win.SetPos(startX, startY);
            }
        }
        #endregion

        #region 开始全部、暂停全部
        public ICommand StartAllCommand => new RelayCommand(() =>
        {
            SelectedProcess = null;
            List<PreviewItem> needRunItems = new List<PreviewItem>();
            #region 模拟选择进程步骤
            foreach (var pro in Processes)
            {
                if(!pro.Running)
                {
                    PreviewItem setting = null;
                    var characterName = pro.GetCharacterName();
                    if (!string.IsNullOrEmpty(characterName))
                    {
                        //从保存列表里找出第一个同名且不在运行中的设置
                        var targetSetting = Settings.FirstOrDefault(p => p.Name == characterName && p.ProcessInfo == null);
                        if (targetSetting != null)
                        {
                            setting = targetSetting;
                        }
                        else//没有找到则按加载方式选择
                        {
                            LoadDefaultSetting(out setting, pro, characterName);
                        }
                    }
                    else//无法找到名称，如中文下
                    {
                        var username = pro.GetUserName();
                        if (!string.IsNullOrEmpty(username))//没有角色名则找账号名
                        {
                            //从保存列表里找出第一个同名且不在运行中的设置
                            var targetSetting = Settings.FirstOrDefault(p => p.UserName == username && p.ProcessInfo == null);
                            if (targetSetting != null)
                            {
                                setting = targetSetting;
                            }
                            else//没有找到则新建
                            {
                                LoadDefaultSetting(out setting, pro, characterName);
                            }
                        }
                        else//如果不设置名称，不会保存，直接新建
                        {
                            LoadDefaultSetting(out setting, pro, characterName);
                        }
                    }
                    if(setting != null)
                    {
                        setting.ProcessInfo = pro;
                        needRunItems.Add(setting);
                    }
                }
            }
            #endregion

            #region 模拟开始步骤
            bool save = false;
            foreach(var item in needRunItems)
            {
                try
                {
                    IGamePreviewWindow gamePreviewWindow = new GamePreviewWindow2(item, PreviewSetting);
                    if (_runningDic.TryAdd(item.ProcessInfo.GUID, gamePreviewWindow))
                    {
                        gamePreviewWindow.OnSettingChanged += GamePreviewWindow_OnSettingChanged;
                        gamePreviewWindow.OnStop += GamePreviewWindow_OnStop;
                        gamePreviewWindow.Start(item.ProcessInfo.MainWindowHandle);
                        item.ProcessInfo.Setting = item;
                        item.ProcessInfo.Running = true;
                        item.ProcessInfo.SettingName = item.Name;
                        Running = true;
                        if (!string.IsNullOrEmpty(item.Name))
                        {
                            if (!PreviewSetting.PreviewItems.Contains(item))
                            {
                                PreviewSetting.PreviewItems.Add(item);
                            }
                            if (!Settings.Contains(item))
                            {
                                Settings.Add(item);
                            }
                            save = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    Window.ShowError(ex.Message, false);
                }
            }
            if(save)
            {
                SaveSetting();
            }
            #endregion
        });
        void LoadDefaultSetting(out PreviewItem setting, ProcessInfo processInfo, string name)
        {
            if (PreviewSetting.StartAllDefaultLoadType == 0)//依序使用已保存列表
            {
                //加载第一个不在运行中的配置
                var firstNoRunning = Settings.FirstOrDefault(p => p.ProcessInfo == null);
                if (firstNoRunning != null)
                {
                    setting = firstNoRunning;
                }
                else
                {
                    //没有不在运行中的配置，新建
                    setting = new PreviewItem()
                    {
                        Name = string.IsNullOrEmpty(name) ? processInfo.GetUserName() : name,
                        UserName = processInfo.GetUserName()
                    };
                }
            }
            else//新建默认
            {
                setting = new PreviewItem()
                {
                    Name = string.IsNullOrEmpty(name) ? processInfo.GetUserName() : name,
                    UserName = processInfo.GetUserName()
                };
            }
        }
        public ICommand StopAllCommand => new RelayCommand(() =>
        {
            SelectedProcess = null;
            foreach (var pro in Processes)
            {
                if (pro.Running)
                {
                    if (_runningDic.TryGetValue(pro.GUID, out var window))
                    {
                        _runningDic.Remove(pro.GUID);
                        pro.Running = false;
                        pro.Setting.ProcessInfo = null;
                        pro.Setting = null;
                        window.Stop();
                    }
                }
            }
            if (_runningDic.Count == 0)
            {
                Running = false;
            }
        });
        #endregion

        public void Dispose()
        {
            StopAll();
            Processes.CollectionChanged -= Processes_CollectionChanged;
            KeyboardService.OnKeyboardClicked -= HotkeyService_OnKeyboardClicked;
            ForegroundWindowService.Current.OnForegroundWindowChanged -= Current_OnForegroundWindowChanged;
            if(_forwardHotkeyRegisterId > 0)
            {
                HotkeyService.GetHotkeyService(Window.GetWindowHandle()).Unregister(_forwardHotkeyRegisterId);
            }
            if (_backwardHotkeyRegisterId > 0)
            {
                HotkeyService.GetHotkeyService(Window.GetWindowHandle()).Unregister(_backwardHotkeyRegisterId);
            }
        }
    }
}
