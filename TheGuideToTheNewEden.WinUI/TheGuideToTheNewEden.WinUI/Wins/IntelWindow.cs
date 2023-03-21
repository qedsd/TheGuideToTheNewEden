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

namespace TheGuideToTheNewEden.WinUI.Wins
{
    public sealed partial class IntelWindow
    {
        private Core.Models.Map.IntelSolarSystemMap IntelMap;
        private Core.Models.EarlyWarningSetting Setting;
        private Canvas ContentCanvas;
        private readonly BaseWindow Window = new BaseWindow();
        private readonly SolidColorBrush defaultBrush = new SolidColorBrush(Colors.DarkGray);
        private readonly SolidColorBrush homeBrush = new SolidColorBrush(Colors.MediumSeaGreen);
        private readonly SolidColorBrush intelBrush = new SolidColorBrush(Colors.OrangeRed);
        private readonly SolidColorBrush downgradeBrush = new SolidColorBrush(Colors.Yellow);
        private const int defaultWidth = 6;
        private const int homeWidth = 12;
        private const int intelWidth = 10;
        private Timer autoIntelTimer;
        public IntelWindow(Core.Models.EarlyWarningSetting setting, Core.Models.Map.IntelSolarSystemMap intelMap)
        {
            ContentCanvas = new Canvas();
            //TODO:是否需要放大缩小
            //ScrollViewer scrollViewer = new ScrollViewer();
            //scrollViewer.Content = ContentCanvas;
            Window.MainContent = ContentCanvas;
            ContentCanvas.MinWidth = 500;
            ContentCanvas.MinHeight = 500;
            IntelMap = intelMap;
            Setting = setting;
            Window.Activated += IntelWindow_Activated;
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(Window);
            WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new Windows.Graphics.SizeInt32(500, 500));
            Window.SizeChanged += Window_SizeChanged;
            appWindow.Closing += AppWindow_Closing;
            appWindow.IsShownInSwitchers = false;
            (appWindow.Presenter as OverlappedPresenter).IsAlwaysOnTop = true;
            //(appWindow.Presenter as OverlappedPresenter).SetBorderAndTitleBar(false, false);
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

        private Dictionary<int, Ellipse> EllipseDic;

        /// <summary>
        /// 调用会重新绘制ui
        /// </summary>
        /// <param name="setting"></param>
        /// <param name="intelMap"></param>
        public void Init(Core.Models.EarlyWarningSetting setting, Core.Models.Map.IntelSolarSystemMap intelMap)
        {
            IntelMap = intelMap;
            Setting = setting;
            Window.Head = $"{Setting.Listener} - {Setting.IntelJumps}";
            ContentCanvas.Children.Clear();
            double width = Window.Bounds.Width;
            double height = Window.Bounds.Height - 42;
            EllipseDic = new Dictionary<int, Ellipse>();
            var allSolarSystem = intelMap.GetAllSolarSystem();
            foreach (var item in allSolarSystem)
            {
                Ellipse ellipse = new Ellipse()
                {
                    StrokeThickness = 0
                };
                if (item.SolarSystemID == intelMap.SolarSystemID)
                {
                    ellipse.Fill = homeBrush;
                    ellipse.Width = homeWidth;
                    ellipse.Height = homeWidth;
                }
                else
                {
                    ellipse.Fill = defaultBrush;
                    ellipse.Width = defaultWidth;
                    ellipse.Height = defaultWidth;
                }
                EllipseDic.Add(item.SolarSystemID, ellipse);
                ContentCanvas.Children.Add(ellipse);
                Canvas.SetLeft(ellipse, width * item.X);
                Canvas.SetTop(ellipse, height * item.Y);
            }
            //line
            HashSet<int> drawn = new HashSet<int>();
            foreach (var item in allSolarSystem)
            {
                if(item.JumpTo.NotNullOrEmpty())
                {
                    if(EllipseDic.TryGetValue(item.SolarSystemID, out Ellipse itemEllipse))
                    {
                        foreach (var jumpTo in item.JumpTo)
                        {
                            int min = Math.Min(item.SolarSystemID, jumpTo) - 30000000;
                            int max = Math.Max(item.SolarSystemID, jumpTo) - 30000000;
                            int mark = min * 10000 + max;//最大星系个数不可能超过10000，故以此组合作为线标记
                            if (!drawn.Contains(mark))
                            {
                                if (EllipseDic.TryGetValue(jumpTo, out Ellipse jumpToEllipse))
                                {
                                    drawn.Add(mark);
                                    double startX = itemEllipse.ActualOffset.X + itemEllipse.ActualSize.X / 2;
                                    double startY = itemEllipse.ActualOffset.Y + itemEllipse.ActualSize.Y / 2;
                                    double endX = jumpToEllipse.ActualOffset.X + jumpToEllipse.ActualSize.X / 2;
                                    double endY = jumpToEllipse.ActualOffset.Y + jumpToEllipse.ActualSize.Y / 2;
                                    Line line = new Line()
                                    {
                                        X1 = startX,
                                        Y1 = startY,
                                        X2 = endX,
                                        Y2 = endY,
                                        StrokeThickness = 1,
                                        Stroke = new SolidColorBrush(Colors.DarkGray)
                                    };
                                    ContentCanvas.Children.Add(line);
                                }
                            }
                        }
                    }
                    
                }
            }

            InitTimer();
        }
        private Dictionary<int,DateTime> StartTimes = new Dictionary<int, DateTime>();
        public void Intel(EarlyWarningContent content)
        {
            if(EllipseDic.TryGetValue(content.SolarSystemId, out var value))
            {
                if(content.IntelType == Core.Enums.IntelChatType.Intel)
                {
                    value.Fill = intelBrush;
                    value.Height = intelWidth;
                    value.Width = intelWidth;
                    StartTimes.Remove(content.SolarSystemId);
                    StartTimes.Add(content.SolarSystemId, DateTime.Now);
                }
                else if(content.IntelType == Core.Enums.IntelChatType.Clear)
                {
                    if (content.SolarSystemId == Setting.LocationID)
                    {
                        value.Fill = homeBrush;
                        value.Width = homeWidth;
                        value.Height = homeWidth;
                    }
                    else
                    {
                        value.Fill = defaultBrush;
                        value.Width = defaultWidth;
                        value.Height = defaultWidth;
                    }
                    StartTimes.Remove(content.SolarSystemId);
                }
            }
        }

        private void InitTimer()
        {
            if(autoIntelTimer != null)
            {
                autoIntelTimer.Stop();
                autoIntelTimer.Dispose();
            }
            if(Setting.AutoClear || Setting.AutoDowngrade)
            {
                autoIntelTimer = new Timer()
                {
                    AutoReset = true,
                    Interval = 30000
                };
                autoIntelTimer.Elapsed += AutoIntelTimer_Elapsed;
                autoIntelTimer.Start();
            }
        }

        private void AutoIntelTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if(Setting.AutoClear)
                ClearElapsed();
            if(Setting.AutoDowngrade)
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
            Window.DispatcherQueue.TryEnqueue(() =>
            {
                foreach (var item in remove)
                {
                    StartTimes.Remove(item);
                    if (EllipseDic.TryGetValue(item, out var value))
                    {
                        if(item == Setting.LocationID)
                        {
                            value.Fill = homeBrush;
                            value.Width = homeWidth;
                            value.Height = homeWidth;
                        }
                        else
                        {
                            value.Fill = defaultBrush;
                            value.Width = defaultWidth;
                            value.Height = defaultWidth;
                        }
                    }
                }
            });
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
            Window.DispatcherQueue.TryEnqueue(() =>
            {
                foreach (var item in changed)
                {
                    if (EllipseDic.TryGetValue(item, out var value))
                    {
                        value.Fill = downgradeBrush;
                    }
                }
            });
        }

        public void Dispose()
        {
            ContentCanvas = null;
            Window?.Close();
            EllipseDic = null;
            autoIntelTimer?.Stop();
            autoIntelTimer?.Dispose();
            autoIntelTimer = null;
        }
    }
}
