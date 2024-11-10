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
                if(SetProperty(ref selectedProcess, value))
                {
                    var setting = GetProcessSetting(value);
                    if (setting != null)
                    {
                        if (_lastHighlightWindow != null)
                        {
                            _lastHighlightWindow.CancelHighlight();
                            _lastHighlightWindow = null;
                        }
                        if (_runningDic.TryGetValue(value.GUID, out var window))
                        {
                            window.Highlight();
                            _lastHighlightWindow = window;
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
            var targetProcesses = Processes.Where(p => p.Running && p.Setting.RespondGlobalHotKey).ToList();
            if (_lastActiveProcessGUID == null)
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
            var targetProcesses = Processes.Where(p => p.Running && p.Setting.RespondGlobalHotKey).ToList();
            if (_lastActiveProcessGUID == null)
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
                foreach (var process in Processes)
                {
                    if(allProcessDict.TryGetValue(process.MainWindowHandle,out var targetProcess))
                    {
                        //进程还存在
                        allProcessDict.Remove(process.MainWindowHandle);
                        //判断进程名称是否需要更新，刚进入游戏时尚无角色名称
                        if(process.WindowTitle != targetProcess.WindowTitle)
                        {
                            process.WindowTitle = targetProcess.WindowTitle;
                            newList ??= new List<ProcessInfo>();
                            newList.Add(process);
                            if (process.Setting != null)
                            {
                                if (process.Running)
                                {
                                    Stop(process.Setting);
                                }
                                process.Setting.Name = targetProcess.GetCharacterName();
                            }
                        }
                    }
                    else
                    {
                        //进程已关闭
                        exitedList.Add(process);
                    }
                }
                foreach(var process in exitedList)//停止已退出进程预览
                {
                    Stop(process.Setting);
                    Processes.Remove(process);
                }
                if(allProcessDict.Any())//此时还剩下的allProcessDict进程均为新加
                {
                    foreach (var newProc in allProcessDict)
                    {
                        //int sortOfNew = PreviewSetting.ProcessOrder.IndexOf(newProc.Value.WindowTitle);
                        //if (sortOfNew > -1)//保存过，需要判断保存顺序
                        //{
                        //    int nowSortOfNew = _processes.Count;
                        //    for(int i = 0; i < _processes.Count; i++)
                        //    {
                        //        int sort = PreviewSetting.ProcessOrder.IndexOf(_processes[i].WindowTitle);
                        //        if(sort > sortOfNew)
                        //        {
                        //            nowSortOfNew = i;
                        //            break;
                        //        }
                        //    }
                        //    Processes.Insert(nowSortOfNew, newProc.Value);
                        //}
                        //else//未保存过，直接加到末尾
                        //{
                        //    Processes.Add(newProc.Value);
                        //}
                        Processes.Add(newProc.Value);
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
                    switch (setting.ShowPreviewWindowMode)
                    {
                        case 0: gamePreviewWindow = new GamePreviewWindow1(setting, previewSetting); break;
                        case 1: gamePreviewWindow = new GamePreviewWindow2(setting, previewSetting); break;
                        case 2: gamePreviewWindow = new GamePreviewWindow3(setting, previewSetting); break;
                        default: throw new NotImplementedException();
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
            SaveOrder();
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
            PreviewSetting.ProcessOrder = Processes.Select(p => p.WindowTitle).ToList();
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
                if(_lastHighlightWindow == window)
                {
                    _lastHighlightWindow = null;
                }
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
            _lastHighlightWindow = null;
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
            var newList = await RefreshProcesses();
            if (PreviewSetting.AutoStartNewProcess)
            {
                if(newList.NotNullOrEmpty())
                {
                    foreach (var item in newList)
                    {
                        if (!item.Running && item.GetCharacterName() != null)
                        {
                            item.Setting = GetProcessSetting(item);
                            Start(item, item.Setting, PreviewSetting);
                        }
                    }
                }
            }
        }

        private void StopGameMonitor()
        {
            _processMonitor?.Stop();
        }
        #endregion
        private IGamePreviewWindow _lastHideWindow;
        /// <summary>
        /// 当前活动窗口变化
        /// </summary>
        /// <param name="hWnd"></param>
        private void Current_OnForegroundWindowChanged(IntPtr hWnd)
        {
            if(_lastHighlightWindow != null)
            {
                _lastHighlightWindow.CancelHighlight();
                _lastHighlightWindow = null;
            }
            if(_lastHideWindow != null)
            {
                _lastHideWindow.ShowWindow();
                _lastHideWindow = null;
            }
            var targetProcess = Processes?.FirstOrDefault(p=>p.Running && p.MainWindowHandle == hWnd);
            if(targetProcess != null)
            {
                if(_runningDic.TryGetValue(targetProcess.GUID, out var item))
                {
                    _lastActiveProcessGUID = targetProcess.GUID;
                    if (item.IsHideOnForeground())
                    {
                        item.HideWindow();
                        _lastHideWindow = item;
                    }
                    else if (item.IsHighlight())
                    {
                        _lastHighlightWindow = item;
                        item.ShowWindow(true);
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
            }
            catch(Exception ex)
            {
                Log.Error(ex);
                Window?.ShowError(ex.Message);
            }
        });
        void LoadDefaultSetting(out PreviewItem setting, ProcessInfo processInfo, string name)
        {
            if (PreviewSetting.StartAllWithNoneSetting)
            {
                setting = new PreviewItem()
                {
                    Name = string.IsNullOrEmpty(name) ? processInfo.GetUserName() : name
                };
            }
            else
            {
                setting = null;
            }
        }
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
