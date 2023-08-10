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
            Core.Log.Info($"全局切换快捷键{PreviewSetting.SwitchHotkey}");
            KeyboardService.OnKeyboardClicked += HotkeyService_OnKeyboardClicked;
        }
        #region 切换快捷键
        private string _lastProcessGUID;
        private void HotkeyService_OnKeyboardClicked(List<Common.KeyboardHook.KeyboardInfo> keys)
        {
            if (keys.NotNullOrEmpty())
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
        private GamePreviewWindow _lastHighlightWindow;
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
                    GamePreviewWindow gamePreviewWindow = new GamePreviewWindow(Setting, PreviewSetting);
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
                    if (item.Value.Setting.HideOnForeground)
                    {
                        item.Value.Hide2();
                    }
                    else if (item.Value.Setting.Highlight)
                    {
                        _lastHighlightWindow = item.Value;
                        item.Value.Highlight();
                        item.Value.Show2();
                    }
                    _lastProcessGUID = targetProcess.GUID;
                }
                else
                {
                    item.Value.CancelHighlight();
                    item.Value.Show2();
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
            if (Setting == null || Setting.ProcessInfo == null)
            {
                Window.ShowError("请选择参考进程", true);
                return;
            }
            if (!Running)
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
            if(_runningDic.Count < 2)
            {
                Window.ShowError("请激活至少两个预览窗口", true);
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
            //换行仅适配主屏幕位于所有屏幕左上角位置的情况
            targetWindow.GetSizeAndPos(out int targetWinX, out int targetWinY, out int targetWinW, out int targetWinH);
            int startX = targetWinX + targetWinW + PreviewSetting.AutoLayoutSpan;
            int refY = targetWinY;//参考y，默认为targetWindow的y，如果换行后，则为换行后的行首个窗口y
            int yWrap = 1;//y换行方向，默认向下换行
            foreach (var win in _runningDic.Values)
            {
                if(win == targetWindow)
                {
                    continue;
                }
                if(!Helpers.WindowHelper.IsInWindow(startX + win.GetWidth(), refY))//x超出屏幕范围，换行
                {
                    startX = targetWinX;//x回到起始位置
                    int newRefY = refY + (win.GetHeight() + PreviewSetting.AutoLayoutSpan) * yWrap;//按默认或上一次换行方向换行后的y
                    if(!Helpers.WindowHelper.IsInWindow(startX + win.GetWidth(), newRefY))
                    {
                        yWrap = -yWrap;//超出y范围，调整换行方向，不考虑第三次更换行方向的情况
                        newRefY = refY + (win.GetHeight() + PreviewSetting.AutoLayoutSpan) * yWrap;
                    }
                    refY = newRefY;
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
        private void SetAutoLayout2(GamePreviewWindow targetWindow)
        {
            //换行仅适配主屏幕位于所有屏幕左上角位置的情况
            targetWindow.GetSizeAndPos(out int targetWinX, out int targetWinY, out _, out int targetWinH);
            int startX = targetWinX;
            int refY = targetWinY;//参考y，默认为targetWindow的y，如果换行后，则为换行后的行首个窗口y
            int yWrap = 1;//y换行方向，默认向下换行
            foreach (var win in _runningDic.Values)
            {
                if (win == targetWindow)
                {
                    continue;
                }
                startX = startX - win.GetWidth() - PreviewSetting.AutoLayoutSpan;
                if (!Helpers.WindowHelper.IsInWindow(startX,refY))//x超出屏幕范围，换行
                {
                    startX = targetWinX;//x回到起始位置
                    int newRefY = refY + (win.GetHeight() + PreviewSetting.AutoLayoutSpan) * yWrap;//按默认或上一次换行方向换行后的y
                    if (!Helpers.WindowHelper.IsInWindow(startX, newRefY))
                    {
                        yWrap = -yWrap;//超出y范围，调整换行方向，不考虑第三次更换行方向的情况
                        newRefY = refY + (win.GetHeight() + PreviewSetting.AutoLayoutSpan) * yWrap;
                    }
                    refY = newRefY;
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
        private void SetAutoLayout3(GamePreviewWindow targetWindow)
        {
            //换行仅适配主屏幕位于所有屏幕左上角位置的情况
            targetWindow.GetSizeAndPos(out int targetWinX, out int targetWinY, out int targetWinW, out int targetWinH);
            int startY = targetWinY + targetWinH + PreviewSetting.AutoLayoutSpan;
            int refX = targetWinX;//参考x，默认为targetWindow的x，如果换行后，则为换行后的行首个窗口x
            int xWrap = 1;//x换行方向，默认向右换行
            foreach (var win in _runningDic.Values)
            {
                if (win == targetWindow)
                {
                    continue;
                }
                if (!Helpers.WindowHelper.IsInWindow(refX, startY + win.GetHeight() + PreviewSetting.AutoLayoutSpan))//y超出屏幕范围，换行
                {
                    startY = targetWinY;//y回到起始位置
                    int newRefX = refX + (win.GetWidth() + PreviewSetting.AutoLayoutSpan) * xWrap;//按默认或上一次换行方向换行后的x
                    if (!Helpers.WindowHelper.IsInWindow(newRefX, startY))
                    {
                        xWrap = -xWrap;//超出y范围，调整换行方向，不考虑第三次更换行方向的情况
                        newRefX = refX + (win.GetHeight() + PreviewSetting.AutoLayoutSpan) * xWrap;
                    }
                    refX = newRefX;
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
        private void SetAutoLayout4(GamePreviewWindow targetWindow)
        {
            //换行仅适配主屏幕位于所有屏幕左上角位置的情况
            targetWindow.GetSizeAndPos(out int targetWinX, out int targetWinY, out int targetWinW, out _);
            int startY = targetWinY;
            int refX = targetWinX;//参考x，默认为targetWindow的x，如果换行后，则为换行后的行首个窗口x
            int xWrap = 1;//x换行方向，默认向右换行
            foreach (var win in _runningDic.Values)
            {
                if (win == targetWindow)
                {
                    continue;
                }
                startY = startY - win.GetHeight() - PreviewSetting.AutoLayoutSpan;
                if (!Helpers.WindowHelper.IsInWindow(refX, startY))//y超出屏幕范围，换行
                {
                    startY = targetWinY;//y回到起始位置
                    int newRefX = refX + (win.GetWidth() + PreviewSetting.AutoLayoutSpan) * xWrap;//按默认或上一次换行方向换行后的x
                    if (!Helpers.WindowHelper.IsInWindow(newRefX, startY))
                    {
                        xWrap = -xWrap;//超出y范围，调整换行方向，不考虑第三次更换行方向的情况
                        newRefX = refX + (win.GetHeight() + PreviewSetting.AutoLayoutSpan) * xWrap;
                    }
                    refX = newRefX;
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
                    GamePreviewWindow gamePreviewWindow = new GamePreviewWindow(item, PreviewSetting);
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
            KeyboardService.OnKeyboardClicked -= HotkeyService_OnKeyboardClicked;
        }
    }
}
