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
        public BaseWindow(string head = null)
        {
            this.InitializeComponent();
            TitleBarHeight = WindowHelper.GetTitleBarHeight(WindowHelper.GetWindowHandle(this));
            this.Title = "新伊甸漫游指南";
            Helpers.WindowHelper.TrackWindow(this);
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            if (Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = ThemeSelectorService.Theme;
            }
            if (!TrySetMicaBackdrop())
            {
                //不启用需要自行修改主背景色
                ThemeSelectorService.OnChangedTheme += ThemeSelectorService_OnChangedTheme;
                ThemeSelectorService_OnChangedTheme(ThemeSelectorService.Theme);
            }
            SetHeadText(head);
            Helpers.WindowHelper.CenterToScreen(this);
            WindowHelper.GetAppWindow(this).SetIcon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo_32.ico"));
        }
        public object MainContent
        {
            get => ContentFrame.Content;

            set => ContentFrame.Content = value;
        }
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
        private void ThemeSelectorService_OnChangedTheme(ElementTheme theme)
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

        Helpers.WindowsSystemDispatcherQueueHelper m_wsdqHelper; // See separate sample below for implementation
        Microsoft.UI.Composition.SystemBackdrops.MicaController m_micaController;
        Microsoft.UI.Composition.SystemBackdrops.SystemBackdropConfiguration m_configurationSource;

        bool TrySetMicaBackdrop()
        {
            if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
            {
                m_wsdqHelper = new Helpers.WindowsSystemDispatcherQueueHelper();
                m_wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

                // Hooking up the policy object
                m_configurationSource = new Microsoft.UI.Composition.SystemBackdrops.SystemBackdropConfiguration();
                this.Activated += Window_Activated;
                this.Closed += Window_Closed;
                ((FrameworkElement)this.Content).ActualThemeChanged += Window_ThemeChanged;

                // Initial configuration state.
                m_configurationSource.IsInputActive = true;
                SetConfigurationSourceTheme();

                m_micaController = new Microsoft.UI.Composition.SystemBackdrops.MicaController();

                // Enable the system backdrop.
                // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
                m_micaController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                m_micaController.SetSystemBackdropConfiguration(m_configurationSource);
                return true; // succeeded
            }

            return false; // Mica is not supported on this system
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            m_configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // Make sure any Mica/Acrylic controller is disposed so it doesn't try to
            // use this closed window.
            if (m_micaController != null)
            {
                m_micaController.Dispose();
                m_micaController = null;
            }
            this.Activated -= Window_Activated;
            m_configurationSource = null;
        }

        private void Window_ThemeChanged(FrameworkElement sender, object args)
        {
            if (m_configurationSource != null)
            {
                SetConfigurationSourceTheme();
            }
        }

        private void SetConfigurationSourceTheme()
        {
            switch (((FrameworkElement)this.Content).ActualTheme)
            {
                case ElementTheme.Dark: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Dark; break;
                case ElementTheme.Light: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Light; break;
                case ElementTheme.Default: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Default; break;
            }
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
            if(string.IsNullOrEmpty(tip))
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
        }
        public void HideWaiting()
        {
            WaitingGrid.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            WaitingProgressRing.IsActive = false;
        }
    }
}
