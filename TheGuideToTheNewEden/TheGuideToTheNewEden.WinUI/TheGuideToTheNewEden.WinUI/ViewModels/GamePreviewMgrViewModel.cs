﻿using Microsoft.UI.Xaml.Media;
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
using TheGuideToTheNewEden.Core.Extensions;

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
        private ObservableCollection<ProcessInfo> _processes = new ObservableCollection<ProcessInfo>();
        public ObservableCollection<ProcessInfo> Processes
        {
            get => _processes;
            set => SetProperty(ref _processes, value);
        }
        private ProcessInfo selectedProcess;
        public ProcessInfo SelectedProcess
        {
            get => selectedProcess;
            set
            {
                var last = selectedProcess;
                if(last != null &&  _runningDic.TryGetValue(last.GUID, out var lastWindow))
                {
                    lastWindow.CancelHighlight();
                }
                if (SetProperty(ref selectedProcess, value))
                {
                    var setting = GetProcessSetting(value);
                    if (setting != null)
                    {
                        if (_runningDic.TryGetValue(value.GUID, out var window))
                        {
                            window.Highlight();
                        }
                    }
                    Setting = setting;
                }
            }
        }

        private bool running;
        public bool Running
        {
            get => running;
            set => SetProperty(ref running, value);
        }

        public string OrderStr
        {
            get => _orderStr;
            set => SetProperty(ref _orderStr, value);
        }
        private string _orderStr;

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
            }
            else
            {
                Settings = new ObservableCollection<PreviewItem>();
            }
            Init();
            RegisterHotkey();
        }
        #region 切换快捷键
        private int _forwardHotkeyRegisterId;
        private int _backwardHotkeyRegisterId;
        private void RegisterHotkey()
        {
            bool result1 = RegisterForwardHotkey();
            bool result2 = RegisterBackwardHotkey();
            if(result1 && result2)
            {
                return;
            }
            else if(!result1 && !result2)
            {
                Window.ShowError(Helpers.ResourcesHelper.GetString("GamePreviewMgrPage_RegisterHotkeyFailed"));
            }
            else if(result1)
            {
                Window.ShowError(Helpers.ResourcesHelper.GetString("GamePreviewMgrPage_RegisterBackwardHotkeyFailed"));
            }
            else if(result2)
            {
                Window.ShowError(Helpers.ResourcesHelper.GetString("GamePreviewMgrPage_RegisterForwardHotkeyFailed"));
            }
        }
        private bool RegisterForwardHotkey()
        {
            Core.Log.Info($"向前全局切换快捷键{PreviewSetting.SwitchHotkey_Forward}");
            if(!UnregisterForwardHotkey())
            {
                return false;
            }
            if (string.IsNullOrEmpty(PreviewSetting.SwitchHotkey_Forward))
            {
                return true;
            }
            else
            {
                return RegisterHotkey(PreviewSetting.SwitchHotkey_Forward, out _forwardHotkeyRegisterId);
            }
        }
        private bool RegisterBackwardHotkey()
        {
            Core.Log.Info($"向后全局切换快捷键{PreviewSetting.SwitchHotkey_Backward}");
            if (!UnregisterBackwardHotkey())
            {
                return false;
            }
            if (string.IsNullOrEmpty(PreviewSetting.SwitchHotkey_Backward))
            {
                return true;
            }
            else
            {
                return RegisterHotkey(PreviewSetting.SwitchHotkey_Backward, out _backwardHotkeyRegisterId);
            }
        }
        private bool RegisterHotkey(string hotkey, out int hotkeyRegisterId)
        {
            hotkeyRegisterId = -1;
            if(string.IsNullOrEmpty(hotkey))
            {
                return true;
            }
            else
            {
                if (HotkeyService.GetHotkeyService(Window.GetWindowHandle()).Register(hotkey, out hotkeyRegisterId))
                {
                    HotkeyService.GetHotkeyService(Window.GetWindowHandle()).HotkeyActived -= GamePreviewMgrViewModel_HotkeyActived;
                    HotkeyService.GetHotkeyService(Window.GetWindowHandle()).HotkeyActived += GamePreviewMgrViewModel_HotkeyActived;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
        private bool UnregisterForwardHotkey()
        {
            if (_forwardHotkeyRegisterId > 0)
            {
                if(HotkeyService.GetHotkeyService(Window.GetWindowHandle()).Unregister(_forwardHotkeyRegisterId))
                {
                    _forwardHotkeyRegisterId = -1;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return true;
            }
        }
        private bool UnregisterBackwardHotkey()
        {
            if (_backwardHotkeyRegisterId > 0)
            {
                if (HotkeyService.GetHotkeyService(Window.GetWindowHandle()).Unregister(_backwardHotkeyRegisterId))
                {
                    _backwardHotkeyRegisterId = -1;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return true;
            }
        }
        public ICommand SetForwardHotkeyCommand => new RelayCommand(() =>
        {
            SaveSetting();
            if(RegisterForwardHotkey())
            {
                Window.ShowSuccess($"{Helpers.ResourcesHelper.GetString("GamePreviewMgrPage_RegisterForwardHotkeySuccessful")}:{PreviewSetting.SwitchHotkey_Forward}");
            }
            else
            {
                Window.ShowError($"{ Helpers.ResourcesHelper.GetString("GamePreviewMgrPage_RegisterForwardHotkeyFailed")}:{ PreviewSetting.SwitchHotkey_Forward}");
            }
        });
        public ICommand SetBackwardHotkeyCommand => new RelayCommand(() =>
        {
            SaveSetting();
            if (RegisterBackwardHotkey())
            {
                Window.ShowSuccess($"{Helpers.ResourcesHelper.GetString("GamePreviewMgrPage_RegisterBackwardHotkeySuccessful")}:{PreviewSetting.SwitchHotkey_Backward}");
            }
            else
            {
                Window.ShowError($"{Helpers.ResourcesHelper.GetString("GamePreviewMgrPage_RegisterBackwardHotkeyFailed")}:{PreviewSetting.SwitchHotkey_Backward}");
            }
        });

        private void GamePreviewMgrViewModel_HotkeyActived(int hotkeyId)
        {
            if (_runningDic.Any())
            {
                if(hotkeyId == _forwardHotkeyRegisterId)
                {
                    SwitchForward();
                }
                else if(hotkeyId == _backwardHotkeyRegisterId)
                {
                    SwitchBackward();
                }
            }
        }
        private string _lastActiveProcessGUID;
        private void SwitchForward()
        {
            //筛选出正在运行中的和响应全局快捷键的
            var targetProcesses = Processes.Where(p => p.Running && p.Setting != null && p.Setting.RespondGlobalHotKey).ToList();
            var lastActiveProcess = Processes.FirstOrDefault(p=>p.GUID == _lastActiveProcessGUID);
            if (lastActiveProcess == null)
            {
                var firstRunning = targetProcesses.FirstOrDefault();
                if (firstRunning != null)
                {
                    if (_runningDic.TryGetValue(firstRunning.GUID, out var value))
                    {
                        value.ActiveSourceWindow();
                        _lastActiveProcessGUID = firstRunning.GUID;
                    }
                }
            }
            else
            {
                var runnings = targetProcesses;
                for (int i = 0; i < runnings.Count; i++)
                {
                    var item = runnings[i];
                    if (item.GUID == _lastActiveProcessGUID)
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
                                _lastActiveProcessGUID = targetGUID;
                            }
                        }
                        break;
                    }
                }
            }
        }

        private void SwitchBackward()
        {
            //筛选出正在运行中的和响应全局快捷键的
            var targetProcesses = Processes.Where(p => p.Running && p.Setting != null && p.Setting.RespondGlobalHotKey).ToList();
            var lastActiveProcess = Processes.FirstOrDefault(p => p.GUID == _lastActiveProcessGUID);
            if (lastActiveProcess == null)
            {
                var firstRunning = targetProcesses.FirstOrDefault();
                if (firstRunning != null)
                {
                    if (_runningDic.TryGetValue(firstRunning.GUID, out var value))
                    {
                        value.ActiveSourceWindow();
                        _lastActiveProcessGUID = firstRunning.GUID;
                    }
                }
            }
            else
            {
                var runnings = targetProcesses;
                for (int i = 0; i < runnings.Count; i++)
                {
                    var item = runnings[i];
                    if (item.GUID == _lastActiveProcessGUID)
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
                                _lastActiveProcessGUID = targetGUID;
                            }
                        }
                        break;
                    }
                }
            }
        }
        #endregion
        private async void Init()
        {
            Window?.ShowWaiting();
            StopGameMonitor();
            await RefreshProcesses();
            SelectedProcess = null;
            StartGameMonitor();
            Window?.HideWaiting();
        }
        private async Task<List<ProcessInfo>> RefreshProcesses()
        {
            List<ProcessInfo> newList = null;
            Processes.CollectionChanged -= Processes_CollectionChanged;
            var targetProcesses = await GetTargetProcessInfos();
            if (targetProcesses.NotNullOrEmpty())
            {
                var allProcessDict = targetProcesses.ToDictionary(p => p.MainWindowHandle);
                List<ProcessInfo> exitedList = new List<ProcessInfo>();//已退出的进程
                List<ProcessInfo> renameList = new List<ProcessInfo>();
                foreach (var process in Processes)
                {
                    if(allProcessDict.TryGetValue(process.MainWindowHandle,out var targetProcess))
                    {
                        //进程还存在
                        //判断进程名称是否需要更新，刚进入游戏时尚无角色名称，若名称改变，当作新进程处理
                        if(process.WindowTitle != targetProcess.WindowTitle)
                        {
                            renameList.Add(process);
                        }
                        else
                        {
                            allProcessDict.Remove(process.MainWindowHandle);
                        }
                    }
                    else
                    {
                        //进程已关闭
                        exitedList.Add(process);
                    }
                }
                foreach(var process in renameList)
                {
                    Processes.Remove(process);
                    //若开启还需关闭
                    if(process.Running && process.Setting != null)
                    {
                        Stop(process.Setting);
                        process.Setting.ProcessInfo = null;
                        process.Setting = null;
                    }
                }
                foreach(var process in exitedList)//停止已退出进程预览
                {
                    Stop(process.Setting);
                    process.Setting.ProcessInfo = null;
                    process.Setting = null;
                    Processes.Remove(process);
                }
                if(allProcessDict.Any())//此时还剩下的allProcessDict进程均为新加
                {
                    foreach (var newProc in allProcessDict)
                    {
                        if(!string.IsNullOrEmpty(newProc.Value.GetCharacterName()))//仅当带角色名称的需要考虑排序
                        {
                            int orderOfNewProcess = PreviewSetting.ProcessOrder.IndexOf(newProc.Value.WindowTitle);
                            if (orderOfNewProcess == -1)
                            {
                                Processes.Add(newProc.Value);//新增直接加到末尾
                                PreviewSetting.ProcessOrder.Add(newProc.Value.WindowTitle);
                            }
                            else//非新增，与当前列表对比，插在排序在新进程后面的进程前面
                            {
                                bool found = false;
                                foreach (var p in Processes)
                                {
                                    int orderOfProc = PreviewSetting.ProcessOrder.IndexOf(p.WindowTitle);
                                    if (orderOfProc > orderOfNewProcess)//当前列表进程排序在当前新增进程后面，将新进程插入到当前列表进程前面
                                    {
                                        int curShowingIndex = Processes.IndexOf(p);
                                        Processes.Insert(curShowingIndex, newProc.Value);
                                        found = true;
                                        break;
                                    }
                                }
                                if (!found)//说明列表所有进程都排在该新增进程前面，新增进程只能插在末尾
                                {
                                    Processes.Add(newProc.Value);
                                }
                            }
                        }
                        else
                        {
                            Processes.Add(newProc.Value);
                        }
                    }
                    newList ??= new List<ProcessInfo>();
                    newList.AddRange(allProcessDict.Values);
                }
            }
            else
            {
                TryClearProcesses();
            }
            Processes.CollectionChanged += Processes_CollectionChanged;
            return newList;
        }
        private async Task<List<ProcessInfo>> GetTargetProcessInfos()
        {
            var keywords = PreviewSetting.ProcessKeywords.Split(',');
            var allProcesses = Process.GetProcesses();
            if (allProcesses.NotNullOrEmpty())
            {
                List<ProcessInfo> targetProcesses = new List<ProcessInfo>();
                //获取所有目标进程
                await Task.Run(() =>
                {
                    foreach (var process in allProcesses)
                    {
                        foreach (var keyword in keywords)
                        {
                            if (process.ProcessName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                            {
                                if (process.MainWindowHandle != IntPtr.Zero)
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
                return targetProcesses;
            }
            else
            {
                return null;
            }
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
                    foreach (var process in runnings)//停止已退出进程预览
                    {
                        Stop(process.Setting);
                    }
                }
                Processes.Clear();
            }
        }


        /// <summary>
        /// 加载进程已保存设置，若不存在则创建默认设置
        /// </summary>
        private PreviewItem GetProcessSetting(ProcessInfo processInfo)
        {
            if (processInfo != null)
            {
                PreviewItem setting = null;
                if (processInfo.Setting != null)//运行中、运行过
                {
                    setting = processInfo.Setting;
                }
                else //未创建过设置
                {
                    var characterName = processInfo.GetCharacterName();
                    if (!string.IsNullOrEmpty(characterName))
                    {
                        //从保存列表里找出第一个同名且不在运行中的设置
                        var targetSetting = Settings.FirstOrDefault(p => p.Name == characterName && p.ProcessInfo == null);
                        if (targetSetting != null)
                        {
                            setting = targetSetting;
                        }
                        else//没有找到则新建
                        {
                            setting = new PreviewItem()
                            {
                                Name = characterName
                            };
                        }
                    }
                    else
                    {
                        setting = new PreviewItem();//不赋予配置名称则不会保存
                    }
                    setting.ProcessInfo = processInfo;
                    processInfo.Setting = setting;
                }
                return setting;
            }
            else
            {
                return null;
            }
        }

        private bool Start(ProcessInfo processInfo, PreviewItem setting, PreviewSetting previewSetting)
        {
            if (processInfo != null && setting != null)
            {
                try
                {
                    IGamePreviewWindow gamePreviewWindow;
                    if(setting.ShowPreviewWindow)
                    {
                        switch (setting.ShowPreviewWindowMode)
                        {
                            case 0: gamePreviewWindow = new GamePreviewWindow1(setting, previewSetting); break;
                            case 1: gamePreviewWindow = new GamePreviewWindow2(setting, previewSetting); break;
                            case 2: gamePreviewWindow = new GamePreviewWindow3(setting, previewSetting); break;
                            default: throw new NotImplementedException();
                        }
                    }
                    else
                    {
                        gamePreviewWindow = new InvisibleGamePreviewWindow(setting, previewSetting);
                    }
                    
                    if (_runningDic.TryAdd(processInfo.GUID, gamePreviewWindow))
                    {
                        gamePreviewWindow.OnSettingChanged += GamePreviewWindow_OnSettingChanged;
                        gamePreviewWindow.OnStop += GamePreviewWindow_OnStop;
                        gamePreviewWindow.Start(processInfo.MainWindowHandle);
                        processInfo.Running = true;
                        //保存
                        if (!string.IsNullOrEmpty(setting.Name))
                        {
                            if (!PreviewSetting.PreviewItems.Contains(setting))
                            {
                                PreviewSetting.PreviewItems.Add(setting);
                            }
                            if (!Settings.Contains(setting))
                            {
                                Settings.Add(setting);
                            }
                            SaveSetting();
                        }
                        else
                        {
                            Window.ShowMsg(Helpers.ResourcesHelper.GetString("GamePreviewMgrPage_EmptyCharacterName"));
                        }
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    Window.ShowError(ex.Message, false);
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public ICommand RefreshProcessListCommand => new RelayCommand(() =>
        {
            Init();
            SaveSetting();
        });

        public ICommand StartCommand => new RelayCommand(() =>
        {
            if(Start(SelectedProcess, Setting, PreviewSetting))
            {
                Running = true;
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

        public void SaveOrder()
        {
            // 调换相对顺序
            var processes = Processes.Where(p => !string.IsNullOrEmpty(p.GetCharacterName())).ToList();
            for (int i = 0; i < processes.Count - 1; i++)
            {
                var p1 = processes[i];
                var p2 = processes[i + 1];
                int p1Order = PreviewSetting.ProcessOrder.IndexOf(p1.WindowTitle);
                int p2Order = PreviewSetting.ProcessOrder.IndexOf(p2.WindowTitle);
                if(p1Order == -1 && p2Order == -1)//均为新增
                {
                    PreviewSetting.ProcessOrder.Insert(0, p1.WindowTitle);
                    PreviewSetting.ProcessOrder.Insert(1, p2.WindowTitle);
                }
                else if(p1Order == -1)//p1新增
                {
                    PreviewSetting.ProcessOrder.Insert(p2Order, p1.WindowTitle);//p1插在p2前面
                }
                else if(p2Order == -1)//p2新增
                {
                    PreviewSetting.ProcessOrder.Insert(p1Order + 1, p2.WindowTitle);//p2插在p1后面
                }
                else//均不为新增
                {
                    if(p1Order > p2Order)//仅当索引p1大于p2才表示相对顺序发生了改变，p1从原本在p2后面变成了在p2前面
                    {
                        //将p2顺序移动到p1后面
                        PreviewSetting.ProcessOrder.Remove(p2.WindowTitle);
                        int newP1Order = PreviewSetting.ProcessOrder.IndexOf(p1.WindowTitle);
                        PreviewSetting.ProcessOrder.Insert(newP1Order + 1, p2.WindowTitle);
                    }
                }
            }
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
                window.Stop();
                if (_runningDic.Count == 0)
                {
                    Running = false;
                }
            }
            else
            {
                Window.ShowError(Helpers.ResourcesHelper.GetString("GamePreviewMgrPage_CannotFindTargetWindow"));
            }
        }

        public void StopAll()
        {
            //_lastHighlightWindow = null;
            var list = Processes?.Where(p => p.Running).ToList();
            if (list.NotNullOrEmpty())
            {
                foreach (var item in list)
                {
                    Stop(item.Setting);
                }
            }
        }
        public ICommand SetUniformSizeCommand => new RelayCommand(() =>
        {
            if(!_runningDic.NotNullOrEmpty())
            {
                Window.ShowError(Helpers.ResourcesHelper.GetString("GamePreviewMgrPage_NoRunning"), true);
            }
            else
            {
                foreach (var window in _runningDic.Values)
                {
                    window.SetSize(PreviewSetting.UniformWidth, PreviewSetting.UniformHeight);
                }
                Window.ShowSuccess(Helpers.ResourcesHelper.GetString("GamePreviewMgrPage_SetUniformSizeSuccessful"));
                SaveSetting();
            }
        });
        #region 监控进程
        private DispatcherTimer _processMonitor;
        private void StartGameMonitor()
        {
            if(_processMonitor == null)
            {
                _processMonitor = new DispatcherTimer()
                {
                    Interval = TimeSpan.FromSeconds(1),
                };
                _processMonitor.Tick += ProcessMonitor_Tick;
            }
            _processMonitor.Start();
        }

        private async void ProcessMonitor_Tick(object sender, object e)
        {
            try
            {
                var newList = await RefreshProcesses();
                if (PreviewSetting.AutoStartNewProcess)
                {
                    if (newList.NotNullOrEmpty())
                    {
                        foreach (var item in newList)
                        {
                            if (!item.Running && item.GetCharacterName() != null)
                            {
                                item.Setting = GetProcessSetting(item);
                                if (Start(item, item.Setting, PreviewSetting))
                                {
                                    Running = true;
                                }
                            }
                        }
                        SaveSetting();
                    }
                }
            }
            catch(Exception ex)
            {
                Core.Log.Error(ex);
            }
        }

        private void StopGameMonitor()
        {
            _processMonitor?.Stop();
        }
        #endregion

        /// <summary>
        /// 当前活动窗口变化
        /// </summary>
        /// <param name="hWnd"></param>
        private void Current_OnForegroundWindowChanged(IntPtr hWnd)
        {
            foreach(var process in Processes)
            {
                if(process.Running)
                {
                    if (_runningDic.TryGetValue(process.GUID, out var previewWindow))
                    {
                        if (process.MainWindowHandle == hWnd)
                        {
                            _lastActiveProcessGUID = process.GUID;
                            if (previewWindow.IsHideOnForeground())
                            {
                                previewWindow.HideWindow();
                            }
                            else
                            {
                                previewWindow.ShowWindow();
                                if (previewWindow.IsHighlight())
                                {
                                    previewWindow.Highlight();
                                }
                            }
                        }
                        else
                        {
                            previewWindow.CancelHighlight();
                            previewWindow.ShowWindow();
                        }
                    }
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

            if (_runningDic.TryGetValue(Processes.First(p => p.Running && p.Setting.ShowPreviewWindow).GUID, out var window))
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
                if (win == targetWindow || !win.GetSetting().ShowPreviewWindow)
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
                if (win == targetWindow || !win.GetSetting().ShowPreviewWindow)
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
                if (win == targetWindow || !win.GetSetting().ShowPreviewWindow)
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
                if (win == targetWindow || !win.GetSetting().ShowPreviewWindow)
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
            try
            {
                SelectedProcess = null;
                List<PreviewItem> needRunItems = new List<PreviewItem>();
                foreach (var pro in Processes)
                {
                    if (!pro.Running)
                    {
                        var setting = GetProcessSetting(pro);
                        if (setting != null)
                        {
                            Start(pro, setting, PreviewSetting);
                        }
                    }
                }
                if(_runningDic.Any())
                {
                    Running = true;
                }
            }
            catch(Exception ex)
            {
                Log.Error(ex);
                Window?.ShowError(ex.Message);
            }
        });
        public ICommand StopAllCommand => new RelayCommand(() =>
        {
            StopAll();
        });
        #endregion

        public ICommand RestorePosCommand => new RelayCommand(() =>
        {
            if(Setting != null)
            {
                if (_runningDic.TryGetValue(Setting.ProcessInfo.GUID, out var window))
                {
                    if(Setting.ShowPreviewWindowMode == 2)
                    {
                        window.ShowWindow();
                        System.Threading.Thread.Sleep(50);
                        window.SetPos(100, 100);
                        System.Threading.Thread.Sleep(50);
                        window.SetSize(533, 300);
                    }
                    else
                    {
                        window.ShowWindow();
                        window.SetPos(100, 100);
                        window.SetSize(533, 300);
                    }
                }
            }
        });

        #region 排序设置
        public ICommand UpdateOrderCommand => new RelayCommand(() =>
        {
            if(PreviewSetting.ProcessOrder.NotNullOrEmpty())
            {
                StringBuilder stringBuilder = new StringBuilder();
                foreach(var p in PreviewSetting.ProcessOrder)
                {
                    stringBuilder.AppendLine(p);
                }
                OrderStr = stringBuilder.ToString();
            }
        });
        public ICommand UpdateOrderByProcessListCommand => new RelayCommand(() =>
        {
            HashSet<string> strings = new HashSet<string>();
            foreach(var p in Processes)
            {
                strings.Add(p.WindowTitle);
            }
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var p in strings)
            {
                stringBuilder.AppendLine(p);
            }
            OrderStr = stringBuilder.ToString();
        });
        public ICommand SaveOrderByListCommand => new RelayCommand(() =>
        {
            PreviewSetting.ProcessOrder.Clear();
            var names = OrderStr.Split("\r");
            foreach(var p in names)
            {
                string name = p.Replace("\n", "");
                if (!string.IsNullOrEmpty(name))
                {
                    PreviewSetting.ProcessOrder.Add(name);
                }
            }
            SaveSetting();
            Window?.ShowSuccess(Helpers.ResourcesHelper.GetString("GamePreviewMgrPage_OrderSetting_SaveSuccess"));
        });
        #endregion


        public event EventHandler HideThumbRequsted;
        public event EventHandler ShowThumbRequsted;
        public ICommand ApplySettingToAllCommand => new RelayCommand(async () =>
        {
            HideThumbRequsted?.Invoke(null, EventArgs.Empty);
            Microsoft.UI.Xaml.Controls.ContentDialog contentDialog = new Microsoft.UI.Xaml.Controls.ContentDialog()
            {
                XamlRoot = Window.Content.XamlRoot,
                Title = Helpers.ResourcesHelper.GetString("GamePreviewMgrPage_ApplySettingToAll"),
                Content = new Microsoft.UI.Xaml.Controls.TextBlock()
                {
                    Text = Helpers.ResourcesHelper.GetString("GamePreviewMgrPage_ApplySettingToAll_Tip")
                },
                PrimaryButtonText = Helpers.ResourcesHelper.GetString("General_OK"),
                CloseButtonText = Helpers.ResourcesHelper.GetString("General_Cancel"),
            };
            if (await contentDialog.ShowAsync() == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
            {
                StopAllCommand.Execute(null);
                foreach(var pro in Processes)
                {
                    var setting = GetProcessSetting(pro);
                    if (setting != null && setting != Setting)
                    {
                        setting.OverlapOpacity = Setting.OverlapOpacity;
                        setting.HideOnForeground = Setting.HideOnForeground;
                        setting.Highlight = Setting.Highlight;
                        setting.HighlightColor = System.Drawing.Color.FromArgb(Setting.HighlightColor.ToArgb());
                        setting.TitleHighlightColor = System.Drawing.Color.FromArgb(Setting.TitleHighlightColor.ToArgb());
                        setting.TitleNormalColor = System.Drawing.Color.FromArgb(Setting.TitleNormalColor.ToArgb());
                        setting.HighlightMarginLeft = Setting.HighlightMarginLeft;
                        setting.HighlightMarginTop = Setting.HighlightMarginTop;
                        setting.HighlightMarginRight = Setting.HighlightMarginRight;
                        setting.HighlightMarginBottom = Setting.HighlightMarginBottom;
                        setting.RespondGlobalHotKey = Setting.RespondGlobalHotKey;
                        setting.ShowPreviewWindow = Setting.ShowPreviewWindow;
                        setting.ShowPreviewWindowMode = Setting.ShowPreviewWindowMode;
                    }
                }
                SaveSetting();
                Window?.ShowSuccess(Helpers.ResourcesHelper.GetString("GamePreviewMgrPage_ApplySettingToAll_Succes"));
            }
            ShowThumbRequsted.Invoke(null, EventArgs.Empty);
        });
        public void Dispose()
        {
            StopAll();
            Processes.CollectionChanged -= Processes_CollectionChanged;
            HotkeyService.GetHotkeyService(Window.GetWindowHandle()).HotkeyActived -= GamePreviewMgrViewModel_HotkeyActived;
            ForegroundWindowService.Current.OnForegroundWindowChanged -= Current_OnForegroundWindowChanged;
            UnregisterForwardHotkey();
            UnregisterBackwardHotkey();
            StopGameMonitor();
        }
    }
}
