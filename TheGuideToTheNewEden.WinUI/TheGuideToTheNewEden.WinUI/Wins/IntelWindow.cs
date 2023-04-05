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

namespace TheGuideToTheNewEden.WinUI.Wins
{
    public sealed partial class IntelWindow
    {
        private Core.Models.Map.IntelSolarSystemMap IntelMap;
        private Core.Models.EarlyWarningSetting Setting;
        private Canvas ContentCanvas;
        private Canvas LineCanvas;
        private Canvas TempCanvas;
        private readonly BaseWindow Window = new BaseWindow();
        private readonly SolidColorBrush defaultBrush = new SolidColorBrush(Colors.DarkGray);
        private readonly SolidColorBrush homeBrush = new SolidColorBrush(Colors.MediumSeaGreen);
        private readonly SolidColorBrush intelBrush = new SolidColorBrush(Colors.OrangeRed);
        private readonly SolidColorBrush downgradeBrush = new SolidColorBrush(Colors.Yellow);
        private readonly SolidColorBrush defaultLineBrush = new SolidColorBrush(Colors.DarkGray);
        private readonly SolidColorBrush tempLineBrush = new SolidColorBrush(Color.FromArgb(255, 135, 227, 205));
        private const int defaultWidth = 8;
        private const int homeWidth = 12;
        private readonly System.Numerics.Vector3 intelScale = new System.Numerics.Vector3(1.5f, 1.5f, 1);
        private DispatcherTimer autoIntelTimer;
        private TextBlock TipTextBlock;
        private TextBlock InfoTextBlock;
        public Grid MapGrid;
        private Ellipse LastPointerToEllipse;

        private AppWindow AppWindow;
        public IntelWindow(Core.Models.EarlyWarningSetting setting, Core.Models.Map.IntelSolarSystemMap intelMap)
        {
            var intelPage = new IntelOverlapPage();
            ContentCanvas = intelPage.MapCanvas;
            TipTextBlock = intelPage.TipTextBlock;
            InfoTextBlock = intelPage.InfoTextBlock;
            MapGrid = intelPage.MapGrid;
            LineCanvas = intelPage.LineCanvas;
            TempCanvas = intelPage.TempCanvas;
            Window.MainContent = intelPage;
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

        private Dictionary<int, Ellipse> EllipseDic;

        private List<Core.Models.Map.IntelSolarSystemMap> AllSolarSystem;
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
            LineCanvas.Children.Clear();
            double width = Window.Bounds.Width;
            double height = Window.Bounds.Height - 42;
            EllipseDic = new Dictionary<int, Ellipse>();
            AllSolarSystem = intelMap.GetAllSolarSystem();
            foreach (var item in AllSolarSystem)
            {
                Ellipse ellipse = new Ellipse()
                {
                    StrokeThickness = 0,
                    Tag = item
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
                ellipse.PointerEntered += Ellipse_PointerEntered;
                ellipse.PointerExited += Ellipse_PointerExited;
                Canvas.SetLeft(ellipse, width * item.X);
                Canvas.SetTop(ellipse, height * item.Y);
            }
            //line
            HashSet<int> drawn = new HashSet<int>();
            foreach (var item in AllSolarSystem)
            {
                if(item.Jumps.NotNullOrEmpty())
                {
                    if(EllipseDic.TryGetValue(item.SolarSystemID, out Ellipse itemEllipse))
                    {
                        foreach (var jumpTo in item.Jumps)
                        {
                            int min = Math.Min(item.SolarSystemID, jumpTo.SolarSystemID) - 30000000;
                            int max = Math.Max(item.SolarSystemID, jumpTo.SolarSystemID) - 30000000;
                            int mark = min * 10000 + max;//最大星系个数不可能超过10000，故以此组合作为线标记
                            if (!drawn.Contains(mark))
                            {
                                if (EllipseDic.TryGetValue(jumpTo.SolarSystemID, out Ellipse jumpToEllipse))
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
                                        Stroke = defaultLineBrush
                                    };
                                    LineCanvas.Children.Add(line);
                                }
                            }
                        }
                    }
                    
                }
            }

            InitTimer();
        }


        #region 星系提示
        private void Ellipse_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            TempCanvas.Children.Clear();
            CancelSelected();
        }
        private void Ellipse_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            var ellipse = sender as Ellipse;
            if (LastPointerToEllipse == ellipse)
            {
                return;
            }
            TempCanvas.Children.Clear();
            CancelSelected();
            var map = ellipse.Tag as Core.Models.Map.IntelSolarSystemMap;
            if (map != null)
            {
                #region 临近星系连线
                if (map.JumpTo.NotNullOrEmpty())
                {
                    foreach (var jump in map.JumpTo)
                    {
                        if (EllipseDic.TryGetValue(jump, out Ellipse jumpToEllipse))
                        {
                            double startX = ellipse.ActualOffset.X + ellipse.ActualSize.X / 2;
                            double startY = ellipse.ActualOffset.Y + ellipse.ActualSize.Y / 2;
                            double endX = jumpToEllipse.ActualOffset.X + jumpToEllipse.ActualSize.X / 2;
                            double endY = jumpToEllipse.ActualOffset.Y + jumpToEllipse.ActualSize.Y / 2;
                            Line line = new Line()
                            {
                                X1 = startX,
                                Y1 = startY,
                                X2 = endX,
                                Y2 = endY,
                                StrokeThickness = 2,
                                Stroke = tempLineBrush
                            };
                            TempCanvas.Children.Add(line);
                        }
                    }
                }
                #endregion

                #region 选择星系突出显示
                LastPointerToEllipse = ellipse;
                ellipse.Scale = new System.Numerics.Vector3(1.6f, 1.6f, 1);

                Canvas.SetLeft(ellipse, ellipse.ActualOffset.X - ellipse.Width * 0.6 / 2);
                Canvas.SetTop(ellipse, ellipse.ActualOffset.Y - ellipse.Height * 0.6 / 2);
                TipTextBlock.Text = map.SolarSystemName;
                TipTextBlock.Visibility = Visibility.Visible;
                TipTextBlock.Translation = new System.Numerics.Vector3(ellipse.ActualOffset.X + 8, ellipse.ActualOffset.Y - 20, 1);

                #endregion
            }
        }
        private void CancelSelected()
        {
            if (LastPointerToEllipse != null)
            {
                LastPointerToEllipse.Scale = new System.Numerics.Vector3(1f, 1f, 1f);
                Canvas.SetLeft(LastPointerToEllipse, LastPointerToEllipse.ActualOffset.X + LastPointerToEllipse.Width * 0.6 / 2);
                Canvas.SetTop(LastPointerToEllipse, LastPointerToEllipse.ActualOffset.Y + LastPointerToEllipse.Height * 0.6 / 2);
            }
            LastPointerToEllipse = null;
            TipTextBlock.Visibility = Visibility.Collapsed;
        }
        #endregion
        private Dictionary<int,DateTime> StartTimes = new Dictionary<int, DateTime>();
        public void Intel(EarlyWarningContent content)
        {
            InfoTextBlock.Text = $"{content.SolarSystemName}({content.Jumps} Jumps):{content.Content}";
            if (EllipseDic.TryGetValue(content.SolarSystemId, out var value))
            {
                if(content.IntelType == Core.Enums.IntelChatType.Intel)
                {
                    if(value.Fill != intelBrush)
                    {
                        value.Fill = intelBrush;
                        value.Scale = intelScale;
                        Canvas.SetLeft(value, value.ActualOffset.X - value.Width * (intelScale.X - 1) / 2);
                        Canvas.SetTop(value, value.ActualOffset.Y - value.Height * (intelScale.Y - 1) / 2);
                        StartTimes.Remove(content.SolarSystemId);
                        StartTimes.Add(content.SolarSystemId, DateTime.Now);
                    }
                    Show();
                }
                else if(content.IntelType == Core.Enums.IntelChatType.Clear)
                {
                    if(value.Scale.X != 1)
                    {
                        value.Scale = new System.Numerics.Vector3(1, 1, 1);
                        Canvas.SetLeft(value, value.ActualOffset.X + value.Width * (intelScale.X - 1) / 2);
                        Canvas.SetTop(value, value.ActualOffset.Y + value.Height * (intelScale.Y - 1) / 2);
                        StartTimes.Remove(content.SolarSystemId);
                    }
                    if (content.SolarSystemId == Setting.LocationID)
                    {
                        value.Fill = homeBrush;
                    }
                    else
                    {
                        value.Fill = defaultBrush;
                    }
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
                if (EllipseDic.TryGetValue(item, out var value))
                {
                    value.Scale = new System.Numerics.Vector3(1, 1, 1);
                    Canvas.SetLeft(value, value.ActualOffset.X + value.Width * (intelScale.X - 1) / 2);
                    Canvas.SetTop(value, value.ActualOffset.Y + value.Height * (intelScale.Y - 1) / 2);
                    if (item == Setting.LocationID)
                    {
                        value.Fill = homeBrush;
                    }
                    else
                    {
                        value.Fill = defaultBrush;
                    }
                }
            }
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
            foreach (var item in changed)
            {
                if (EllipseDic.TryGetValue(item, out var value))
                {
                    value.Fill = downgradeBrush;
                }
            }
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
            ContentCanvas = null;
            LineCanvas = null;
            AppWindow.Closing -= AppWindow_Closing;
            Window?.Close();
            EllipseDic = null;
            autoIntelTimer?.Stop();
            //autoIntelTimer?.Dispose();
            autoIntelTimer = null;
        }
    }
}
