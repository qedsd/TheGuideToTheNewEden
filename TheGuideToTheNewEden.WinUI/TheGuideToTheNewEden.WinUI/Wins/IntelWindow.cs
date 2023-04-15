// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.Core.Extensions;
using Microsoft.UI.Windowing;
using Windows.UI.ViewManagement;
using TheGuideToTheNewEden.Core.Models;
using System.Timers;
using System.Runtime.InteropServices;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Views;
using TheGuideToTheNewEden.WinUI.Services.Settings;
using Windows.UI;
using TheGuideToTheNewEden.WinUI.Views.IntelOverlapPages;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    public sealed partial class IntelWindow
    {
        private Core.Models.Map.IntelSolarSystemMap IntelMap;
        private Core.Models.EarlyWarningSetting Setting;
        private readonly BaseWindow Window = new BaseWindow();
        private DispatcherTimer autoIntelTimer;
        private AppWindow AppWindow;
        private Interfaces.IIntelOverlapPage _intelPage;
        private HashSet<int> _allSolarSystem = new HashSet<int>();
        public IntelWindow(Core.Models.EarlyWarningSetting setting, Core.Models.Map.IntelSolarSystemMap intelMap)
        {
            _intelPage = null;
            switch((Core.Enums.IntelOverlapStyle)setting.OverlapStyle)
            {
                case Core.Enums.IntelOverlapStyle.Neweden: _intelPage = new DefaultIntelOverlapPage();break;
            }
            Window.MainContent = _intelPage;
            Window.HideAppDisplayName();
            Window.SetSmallTitleBar();
            IntelMap = intelMap;
            Setting = setting;
            Window.Activated += IntelWindow_Activated;
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(Window);
            WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            AppWindow.Resize(new Windows.Graphics.SizeInt32(Setting.WinW, Setting.WinH));
            if(Setting.WinX != -1 && Setting.WinY != -1)
            {
                Helpers.WindowHelper.MoveToScreen(Window, Setting.WinX, Setting.WinY);
            }
            Window.SizeChanged += Window_SizeChanged;
            AppWindow.Changed += AppWindow_Changed;
            AppWindow.Closing += AppWindow_Closing;
            AppWindow.IsShownInSwitchers = false;
            (AppWindow.Presenter as OverlappedPresenter).IsAlwaysOnTop = true;
            TransparentWindowHelper.TransparentWindow(Window, Setting.OverlapOpacity);
        }

        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if(args.DidPositionChange || args.DidSizeChange)
            {
                Setting.WinX = sender.Position.X;
                Setting.WinY = sender.Position.Y;
                Setting.WinW = sender.Size.Width;
                Setting.WinH = sender.Size.Height;
                IntelSettingService.SetValue(Setting);
            }
        }

        private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            args.Cancel = true;
            sender.Hide();
        }

        private void Window_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            Init(Setting, IntelMap);
        }

        public void Show()
        {
            Window.Activate();
        }
        private void IntelWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            Init(Setting, IntelMap);
            Window.Activated -= IntelWindow_Activated;
        }

        /// <summary>
        /// 调用会重新绘制ui
        /// </summary>
        /// <param name="setting"></param>
        /// <param name="intelMap"></param>
        public void Init(Core.Models.EarlyWarningSetting setting, Core.Models.Map.IntelSolarSystemMap intelMap)
        {
            _allSolarSystem.Clear();
            var all = intelMap.GetAllSolarSystem();
            if(all.NotNullOrEmpty())
            {
                foreach(var item in all)
                {
                    _allSolarSystem.Add(item.SolarSystemID);
                }
            }
            Window.Head = $"{Setting.Listener} - {Setting.IntelJumps}";
            _intelPage.Init(Window,setting,intelMap);
            InitTimer();
        }


        
        private Dictionary<int,DateTime> StartTimes = new Dictionary<int, DateTime>();
        public void Intel(EarlyWarningContent content)
        {
            _intelPage.Intel(content);
            if (_allSolarSystem.Contains(content.SolarSystemId))
            {
                if(content.IntelType == Core.Enums.IntelChatType.Intel)
                {
                    StartTimes.Remove(content.SolarSystemId);
                    StartTimes.Add(content.SolarSystemId, DateTime.Now);
                    Show();
                }
                else if(content.IntelType == Core.Enums.IntelChatType.Clear)
                {
                    StartTimes.Remove(content.SolarSystemId);
                    TyrHideWindow();
                }
            }
        }

        private void InitTimer()
        {
            if(autoIntelTimer != null)
            {
                autoIntelTimer.Stop();
            }
            if(Setting.AutoClear || Setting.AutoDowngrade)
            {
                autoIntelTimer = new DispatcherTimer();
                autoIntelTimer.Interval = TimeSpan.FromSeconds(10);
                autoIntelTimer.Tick += AutoIntelTimer_Tick; ;
                autoIntelTimer.Start();
            }
        }

        private void AutoIntelTimer_Tick(object sender, object e)
        {
            if (Setting.AutoClear)
                ClearElapsed();
            if (Setting.AutoDowngrade)
                DowngradeElapsed();
        }

        private void ClearElapsed()
        {
            DateTime now = DateTime.Now;
            List<int> remove = new List<int>();
            foreach(var item in StartTimes)
            {
                if((now - item.Value).TotalMinutes >= Setting.AutoClearMinute)
                {
                    remove.Add(item.Key);
                }
            }
            foreach (var item in remove)
            {
                StartTimes.Remove(item);
            }
            _intelPage.Clear(remove);
            TyrHideWindow();
        }
        private void DowngradeElapsed()
        {
            DateTime now = DateTime.Now;
            List<int> changed = new List<int>();
            foreach (var item in StartTimes)
            {
                if ((now - item.Value).TotalMinutes >= Setting.AutoDowngradeMinute)
                {
                    changed.Add(item.Key);
                }
            }
            _intelPage.Downgrade(changed);
        }
        private void TyrHideWindow()
        {
            if(Setting.OverlapType == 1)
            {
                if (StartTimes.Count == 0)
                {
                    //延迟30秒后关闭窗口
                    Timer timer = new Timer()
                    {
                        AutoReset = false,
                        Interval = 30000
                    };
                    timer.Elapsed += ((s, e) =>
                    {
                        //再次判断
                        if (StartTimes.Count == 0)
                        {
                            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(Window);
                            WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
                            Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                            appWindow?.Hide();
                        }
                    });
                    timer.Start();
                }
            }
        }
        public void Dispose()
        {
            _intelPage.Dispose();
            AppWindow.Closing -= AppWindow_Closing;
            Window?.Close();
            autoIntelTimer?.Stop();
            autoIntelTimer = null;
        }
    }
}
