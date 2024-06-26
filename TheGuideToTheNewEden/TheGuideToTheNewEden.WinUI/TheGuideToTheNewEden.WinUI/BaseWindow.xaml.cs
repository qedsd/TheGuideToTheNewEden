﻿using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Services.Settings;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT;

namespace TheGuideToTheNewEden.WinUI
{
    public partial class BaseWindow : Window
    {
        public WinUICommunity.IThemeService ThemeService { get; set; }
        public BaseWindow(bool useThemeService = true)
        {
            this.InitializeComponent();
            if(useThemeService)
            {
                ThemeService = new WinUICommunity.ThemeService();
                ThemeService.Initialize(this, false);
                ThemeService.ConfigElementTheme(ThemeSelectorService.Theme);
                ThemeService.ConfigBackdrop(BackdropSelectorService.Value);
                ThemeSelectorService.OnChangedTheme += ThemeSelectorService_OnChangedTheme;
                ThemeSelectorService_OnChangedTheme(ThemeSelectorService.Theme);
                BackdropSelectorService.OnBackdropTypeChanged += BackdropSelectorService_OnBackdropTypeChanged;
            }
            TitleBarHeight = (int)(WindowHelper.GetTitleBarHeight(WindowHelper.GetWindowHandle(this)) / Helpers.WindowHelper.GetDpiScale(this));//只能在ExtendsContentIntoTitleBar前获取，之后会变为0
            this.Title = Helpers.ResourcesHelper.GetString("AppDisplayName");
            Helpers.WindowHelper.TrackWindow(this);
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            Helpers.WindowHelper.CenterToScreen(this);
            WindowHelper.GetAppWindow(this).SetIcon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo_32.ico"));
        }

        public object MainContent
        {
            get => ContentFrame.Content;

            set => ContentFrame.Content = value;
        }
        public UIElement MainUIElement
        {
            get => MainWindowGrid;
        }

        /// <summary>
        /// 无缩放下的原始标题栏高度
        /// </summary>
        public int TitleBarHeight { get;private set; }
        public string Head
        {
            get => HeadTextBlock.Text;

            set
            {
                SetHeadText(value);
            }
        }
        public void SetHeadText(string head)
        {
            if (string.IsNullOrEmpty(head))
            {
                HeadPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                HeadPanel.Visibility = Visibility.Visible;
                HeadTextLine.Visibility = AppDisplayNameTextBlock.Visibility;
                HeadTextBlock.Text = head;
            }
        }
        public void HideAppDisplayName()
        {
            AppDisplayNameTextBlock.Visibility = Visibility.Collapsed;
            HeadTextLine.Visibility = Visibility.Collapsed;
        }
        public void ShowAppDisplayName()
        {
            AppDisplayNameTextBlock.Visibility = Visibility.Visible;
            HeadTextLine.Visibility = HeadPanel.Visibility;
        }
        public double GetTitleBarDesignHeight()
        {
            return AppTitleBar.Height;
        }
        public void SetLargeTitleBar()
        {
            SetTitleBarHeight(42);
            SetAppTitleLeftBorder(20);
        }
        public void SetSmallTitleBar()
        {
            SetTitleBarHeight(32);
            SetAppTitleLeftBorder(10);
        }
        public void SetTitleBarHeight(int h)
        {
            AppTitleBarGrid.Height = h;
            //AppTitleBar.Height = h;
        }
        public void SetAppTitleLeftBorder(int w)
        {
            AppTitleLeftBorder.Width = w;
        }
        public void SetAppTitleBarLeft(int w)
        {
            AppTitleBar.Margin = new Thickness(w, AppTitleBar.Margin.Top, AppTitleBar.Margin.Right, AppTitleBar.Margin.Bottom);
        }
        /// <summary>
        /// 隐藏标题，不隐藏logo
        /// </summary>
        public void HideAppTitleContentArea()
        {
            AppDisplayNameTextBlock.Visibility = Visibility.Collapsed;
            HeadPanel.Visibility = Visibility.Collapsed;
            InfoBar.Margin = new Thickness(0, 32, 0, 0);
        }
        /// <summary>
        /// 主页模式
        /// 隐藏标题，显示内容区域上移到标题栏位置
        /// </summary>
        public void SetTavViewHomeMode()
        {
            ContentArea.SetValue(Grid.RowSpanProperty, 2);
            ContentArea.SetValue(Grid.RowProperty, 0);
            HideAppTitleContentArea();
        }

        private System.Timers.Timer Timer;
        private void StartTimer()
        {
            if (Timer == null)
            {
                Timer = new System.Timers.Timer(3000);
                Timer.AutoReset = false;
                Timer.Elapsed += Timer_Elapsed;
            }
            Timer.Start();
        }
        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Helpers.WindowHelper.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                InfoBar.Message = string.Empty;
                InfoBar.IsOpen = false;
            });
        }

        public void ShowMsg(string msg, bool autoClose = true)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                InfoBar.Severity = InfoBarSeverity.Informational;
                InfoBar.Message = msg;
                InfoBar.IsOpen = true;
                if (autoClose)
                {
                    StartTimer();
                }
            });
        }
        public void ShowError(string msg, bool autoClose = false)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                InfoBar.Severity = InfoBarSeverity.Error;
                InfoBar.Message = msg;
                InfoBar.IsOpen = true;
                if (autoClose)
                {
                    StartTimer();
                }
            });
        }
        public void ShowSuccess(string msg, bool autoClose = true)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                InfoBar.Severity = InfoBarSeverity.Success;
                InfoBar.Message = msg;
                InfoBar.IsOpen = true;
                if (autoClose)
                {
                    StartTimer();
                }
            });
        }
        public void ShowWaiting(string tip = null)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (string.IsNullOrEmpty(tip))
                {
                    WaitingText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    WaitingText.Visibility = Visibility.Visible;
                    WaitingText.Text = tip;
                }
                WaitingGrid.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                WaitingProgressRing.IsActive = true;
            });
        }
        public void HideWaiting()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                WaitingGrid.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                WaitingProgressRing.IsActive = false;
            });
        }

        public void Hide()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                WindowHelper.GetAppWindow(this)?.Hide();
            });
        }

        private void ThemeSelectorService_OnChangedTheme(ElementTheme theme)
        {
            if (BackdropSelectorService.Value == WinUICommunity.BackdropType.None)
            {
                switch (theme)
                {
                    case ElementTheme.Dark: MainWindowGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 32, 32)); break;
                    case ElementTheme.Light: MainWindowGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 243, 243, 243)); break;
                    case ElementTheme.Default:
                        {
                            if (Application.Current.RequestedTheme == ApplicationTheme.Dark)
                            {
                                MainWindowGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 32, 32));
                            }
                            else
                            {
                                MainWindowGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 243, 243, 243));
                            }
                        }
                        break;
                }
            }
        }

        private void BackdropSelectorService_OnBackdropTypeChanged(object sender, WinUICommunity.BackdropType e)
        {
            if(e == WinUICommunity.BackdropType.None)
            {
                ThemeSelectorService_OnChangedTheme(ThemeSelectorService.Theme);
            }
            else
            {
                MainWindowGrid.Background = new SolidColorBrush(Colors.Transparent);
            }
        }
    }
}
